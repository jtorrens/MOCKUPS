using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell;

internal sealed record PendingRestore(
    Guid RequestId,
    Guid PackageId,
    RestoreBackupSummary Summary);

internal sealed record RestoreNotification(
    string Title,
    string Message,
    bool IsError);

internal sealed class BackupHubRestoreService
{
    private readonly string _databasePath;
    private readonly BackupHubBackupService _backups;

    public BackupHubRestoreService(
        string databasePath,
        BackupHubBackupService backups)
    {
        _databasePath = Path.GetFullPath(databasePath);
        _backups = backups;
    }

    public async Task<IReadOnlyList<RestoreNotification>>
        ProcessPendingAsync(
            Func<PendingRestore, Task<bool>> confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        if (!TryResolveVault(out var vault))
        {
            return [];
        }

        var locations = new RestoreLocations(vault);
        locations.PrepareOwnership();
        var notifications = new List<RestoreNotification>();
        RecoverInterruptedTransactions(
            locations,
            notifications);
        ClaimPreparedRequests(locations);
        foreach (var claimed in RestoreDirectories(
                     locations.Processing))
        {
            var requestId = RequestIdFromDirectory(
                claimed);
            if (File.Exists(
                    locations.ResultPath(requestId)))
            {
                FinalizeExistingResult(
                    claimed,
                    locations,
                    requestId);
                continue;
            }

            await ProcessClaimedAsync(
                claimed,
                requestId,
                locations,
                confirm,
                notifications);
        }
        return notifications;
    }

    private async Task ProcessClaimedAsync(
        string claimed,
        Guid requestId,
        RestoreLocations locations,
        Func<PendingRestore, Task<bool>> confirm,
        List<RestoreNotification> notifications)
    {
        RestoreRequest? request = null;
        BackupManifest? manifest = null;
        try
        {
            (request, manifest) = ValidateClaimed(
                claimed,
                requestId);
        }
        catch (RestoreContractException exception)
        {
            var rejected = RestoreResult.Rejected(
                requestId,
                exception.PackageId,
                exception.Code,
                exception.Message);
            PublishTerminal(
                rejected,
                claimed,
                locations);
            notifications.Add(new RestoreNotification(
                "Backup incompatible",
                exception.Message,
                IsError: true));
            return;
        }
        catch (Exception exception)
        {
            var rejected = RestoreResult.Rejected(
                requestId,
                packageId: null,
                "request-invalid",
                exception.Message);
            PublishTerminal(
                rejected,
                claimed,
                locations);
            notifications.Add(new RestoreNotification(
                "Solicitud de restauración inválida",
                exception.Message,
                IsError: true));
            return;
        }

        bool confirmed;
        try
        {
            confirmed = await confirm(
                new PendingRestore(
                    requestId,
                    Guid.Parse(request.PackageId),
                    request.BackupSummary));
        }
        catch (Exception exception)
        {
            PublishTerminal(
                RestoreResult.Failed(
                    requestId,
                    Guid.Parse(request.PackageId),
                    preRestorePackageId: null,
                    "not-presented",
                    "confirmation-failed",
                    exception.Message),
                claimed,
                locations);
            notifications.Add(new RestoreNotification(
                "No se pudo confirmar la restauración",
                exception.Message,
                IsError: true));
            return;
        }
        if (!confirmed)
        {
            PublishTerminal(
                RestoreResult.Cancelled(
                    requestId,
                    Guid.Parse(request.PackageId)),
                claimed,
                locations);
            return;
        }

        BackupPublication preRestore;
        try
        {
            preRestore = _backups.Publish(
                    BackupReason.PreRestore)
                ?? throw new InvalidOperationException(
                    "A pre-restore backup cannot be deduplicated.");
        }
        catch (Exception exception)
        {
            PublishTerminal(
                RestoreResult.Failed(
                    requestId,
                    Guid.Parse(request.PackageId),
                    preRestorePackageId: null,
                    "confirmed",
                    "pre-restore-backup-failed",
                    exception.Message),
                claimed,
                locations);
            notifications.Add(new RestoreNotification(
                "No se pudo proteger la versión actual",
                exception.Message,
                IsError: true));
            return;
        }

        string? transaction = null;
        try
        {
            transaction = ApplyReplacement(
                requestId,
                Guid.Parse(request.PackageId),
                preRestore.PackageId,
                Path.Combine(
                    claimed,
                    "package",
                    "payload",
                    BackupHubBackupService.PayloadPath));
            PublishTerminal(
                RestoreResult.Applied(
                    requestId,
                    Guid.Parse(request.PackageId),
                    preRestore.PackageId),
                claimed,
                locations,
                deferClaimFinalization: true);
            Directory.Delete(
                transaction,
                recursive: true);
            FinalizeClaim(
                claimed,
                locations,
                appliedOrCancelled: true);
            notifications.Add(new RestoreNotification(
                "Backup restaurado",
                "MOCKUPS restauró y verificó la base de datos. La versión reemplazada está guardada en Backup Hub.",
                IsError: false));
        }
        catch (Exception exception)
        {
            var errorCode = transaction is null
                ? "replacement-failed"
                : "verification-failed";
            try
            {
                if (transaction is not null
                    && Directory.Exists(transaction))
                {
                    RollBackReplacement(transaction);
                }
                PublishTerminal(
                    RestoreResult.Failed(
                        requestId,
                        Guid.Parse(request.PackageId),
                        preRestore.PackageId,
                        "confirmed",
                        errorCode,
                        exception.Message),
                    claimed,
                    locations);
            }
            catch (Exception recoveryException)
            {
                throw new InvalidOperationException(
                    "MOCKUPS could not complete or safely roll back the restore transaction.",
                    new AggregateException(
                        exception,
                        recoveryException));
            }
            notifications.Add(new RestoreNotification(
                "La restauración no se aplicó",
                exception.Message,
                IsError: true));
        }
    }

    private (RestoreRequest Request, BackupManifest Manifest)
        ValidateClaimed(
            string claimed,
            Guid requestId)
    {
        BackupHubContract.RequireRegularDirectory(
            claimed,
            "restore request");
        var entries = Directory
            .EnumerateFileSystemEntries(claimed)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!entries.SequenceEqual(
                ["package", "request.json"],
                StringComparer.Ordinal))
        {
            throw new RestoreContractException(
                "request-invalid",
                null,
                "Restore Handoff v2 must contain only request.json and package.");
        }

        var requestPath = Path.Combine(
            claimed,
            "request.json");
        BackupHubContract.RequireRegularFile(
            requestPath,
            "request.json");
        var requestBytes = File.ReadAllBytes(
            requestPath);
        BackupHubContract.RequireExactJsonShape(
            requestBytes,
            [
                "handoffVersion", "requestId", "applicationId", "packageId",
                "preparedAt", "vaultObjectSha256", "manifestSha256",
                "backupSummary", "state",
            ],
            ("backupSummary",
            [
                "createdAt", "reason", "snapshotFormat",
                "snapshotSchemaVersion", "fileCount", "totalBytes",
            ]));
        RestoreRequest request;
        try
        {
            request = JsonSerializer.Deserialize<RestoreRequest>(
                    requestBytes,
                    BackupHubContract.JsonOptions)
                ?? throw new InvalidDataException(
                    "request.json is empty.");
        }
        catch (Exception exception)
        {
            throw new RestoreContractException(
                "request-invalid",
                null,
                exception.Message);
        }

        if (request.HandoffVersion != 2
            || request.RequestId
                != BackupHubContract.Canonical(requestId)
            || request.State != "prepared"
            || !Guid.TryParseExact(
                request.PackageId,
                "D",
                out var packageId)
            || request.PackageId
                != BackupHubContract.Canonical(packageId)
            || !BackupHubContract.IsLowercaseSha256(
                request.VaultObjectSha256)
            || !BackupHubContract.IsLowercaseSha256(
                request.ManifestSha256)
            || !DateTimeOffset.TryParse(
                request.PreparedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            throw new RestoreContractException(
                "request-invalid",
                null,
                "request.json does not satisfy Restore Handoff v2.");
        }
        if (request.ApplicationId
            != BackupHubBackupService.ApplicationId)
        {
            throw new RestoreContractException(
                "identity-mismatch",
                packageId,
                "The restore request belongs to another application.");
        }

        var packagePath = Path.Combine(
            claimed,
            "package");
        BackupManifest manifest;
        try
        {
            manifest = BackupPackageValidator.Validate(
                packagePath,
                packageId);
        }
        catch (Exception exception)
        {
            throw new RestoreContractException(
                "snapshot-invalid",
                packageId,
                exception.Message);
        }
        var manifestHash = BackupHubContract.HashFile(
            Path.Combine(
                packagePath,
                "manifest.json"));
        if (manifestHash != request.ManifestSha256)
        {
            throw new RestoreContractException(
                "manifest-hash-mismatch",
                packageId,
                "The restore manifest hash does not match request.json.");
        }

        var summary = request.BackupSummary;
        var totalBytes = manifest.Files.Sum(
            file => file.ByteLength);
        if (summary.CreatedAt != manifest.CreatedAt
            || summary.Reason != manifest.Reason
            || summary.SnapshotFormat
                != manifest.Snapshot.Format
            || summary.SnapshotSchemaVersion
                != manifest.Snapshot.SchemaVersion
            || summary.FileCount != manifest.Files.Length
            || summary.TotalBytes != totalBytes)
        {
            throw new RestoreContractException(
                "contract-mismatch",
                packageId,
                "The restore summary does not match the selected backup manifest.");
        }
        return (request, manifest);
    }

    private string ApplyReplacement(
        Guid requestId,
        Guid packageId,
        Guid preRestorePackageId,
        string sourceSnapshot)
    {
        var databaseDirectory = Path.GetDirectoryName(
            _databasePath)!;
        var maintenance = WorkstationUpdateMaintenance
            .LockFilePath(_databasePath);
        if (File.Exists(maintenance))
        {
            throw new InvalidOperationException(
                "Repository maintenance is active.");
        }
        RequireNoSqliteSidecars();

        var identity = BackupHubContract.Canonical(
            requestId);
        var staging = Path.Combine(
            databaseDirectory,
            $".{identity}.restore-tmp");
        var transaction = Path.Combine(
            databaseDirectory,
            $".mockups-restore-{identity}.txn");
        if (Directory.Exists(staging)
            || Directory.Exists(transaction))
        {
            throw new IOException(
                "A restore transaction with this identity already exists.");
        }

        Directory.CreateDirectory(staging);
        var transactionPublished = false;
        try
        {
            var replacement = Path.Combine(
                staging,
                "replacement.sqlite");
            File.Copy(
                sourceSnapshot,
                replacement,
                overwrite: false);
            FlushFile(replacement);
            _ = SqliteDatabaseSnapshotService.Validate(
                replacement);
            var journal = new RestoreJournal(
                BackupHubContract.Canonical(requestId),
                BackupHubContract.Canonical(packageId),
                BackupHubContract.Canonical(preRestorePackageId),
                BackupHubContract.HashFile(_databasePath),
                BackupHubContract.HashFile(replacement));
            BackupHubContract.WriteJsonDurably(
                Path.Combine(staging, "journal.json"),
                journal);
            Directory.Move(
                staging,
                transaction);
            transactionPublished = true;
            BackupHubContract.FlushDirectory(
                databaseDirectory);

            replacement = Path.Combine(
                transaction,
                "replacement.sqlite");
            var rollback = Path.Combine(
                transaction,
                "previous.sqlite");
            File.Replace(
                replacement,
                _databasePath,
                rollback,
                ignoreMetadataErrors: true);
            BackupHubContract.FlushDirectory(
                databaseDirectory);
            _ = SqliteDatabaseSnapshotService.Validate(
                _databasePath);
            if (BackupHubContract.HashFile(_databasePath)
                != journal.CandidateSha256)
            {
                throw new InvalidDataException(
                    "The restored database differs from the validated candidate.");
            }
            return transaction;
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
            if (transactionPublished
                && Directory.Exists(transaction))
            {
                RollBackReplacement(transaction);
            }
            throw;
        }
    }

    private void RollBackReplacement(
        string transaction)
    {
        var journal = ReadJournal(transaction);
        var previous = Path.Combine(
            transaction,
            "previous.sqlite");
        if (File.Exists(previous))
        {
            var recovery = Path.Combine(
                transaction,
                "recovery.sqlite");
            File.Copy(previous, recovery, overwrite: false);
            FlushFile(recovery);
            File.Replace(
                recovery,
                _databasePath,
                destinationBackupFileName: null,
                ignoreMetadataErrors: true);
            BackupHubContract.FlushDirectory(
                Path.GetDirectoryName(_databasePath)!);
        }
        _ = SqliteDatabaseSnapshotService.Validate(
            _databasePath);
        if (BackupHubContract.HashFile(_databasePath)
            != journal.PreviousSha256)
        {
            throw new InvalidDataException(
                "The previous database could not be restored after a failed replacement.");
        }
        Directory.Delete(transaction, recursive: true);
    }

    private void RecoverInterruptedTransactions(
        RestoreLocations locations,
        List<RestoreNotification> notifications)
    {
        var databaseDirectory = Path.GetDirectoryName(
            _databasePath)!;
        foreach (var transaction in Directory
                     .EnumerateDirectories(
                         databaseDirectory,
                         ".mockups-restore-*.txn")
                     .Order(StringComparer.Ordinal))
        {
            var journal = ReadJournal(transaction);
            var requestId = Guid.Parse(
                journal.RequestId);
            var resultPath = locations.ResultPath(
                requestId);
            var claimed = Path.Combine(
                locations.Processing,
                $"{journal.RequestId}.bhrestore");
            if (File.Exists(resultPath))
            {
                _ = SqliteDatabaseSnapshotService.Validate(
                    _databasePath);
                if (BackupHubContract.HashFile(_databasePath)
                    != journal.CandidateSha256)
                {
                    throw new InvalidDataException(
                        "A completed restore result does not match the live database.");
                }
                Directory.Delete(transaction, recursive: true);
                if (Directory.Exists(claimed))
                {
                    FinalizeExistingResult(
                        claimed,
                        locations,
                        requestId);
                }
                continue;
            }

            RollBackReplacement(transaction);
            if (!Directory.Exists(claimed))
            {
                throw new InvalidDataException(
                    "An interrupted restore has no claimed request to finalize.");
            }
            PublishTerminal(
                RestoreResult.Failed(
                    requestId,
                    Guid.Parse(journal.PackageId),
                    Guid.Parse(journal.PreRestorePackageId),
                    "confirmed",
                    "post-pre-restore-internal-error",
                    "MOCKUPS recovered the previous database after an interrupted restore."),
                claimed,
                locations);
            notifications.Add(new RestoreNotification(
                "Restauración interrumpida recuperada",
                "MOCKUPS recuperó y verificó la base anterior. El backup seleccionado no se aplicó.",
                IsError: true));
        }
    }

    private static RestoreJournal ReadJournal(
        string transaction)
    {
        var path = Path.Combine(
            transaction,
            "journal.json");
        BackupHubContract.RequireRegularFile(
            path,
            "restore journal");
        var journal = JsonSerializer.Deserialize<RestoreJournal>(
                File.ReadAllBytes(path),
                BackupHubContract.JsonOptions)
            ?? throw new InvalidDataException(
                "The restore journal is empty.");
        if (!Guid.TryParseExact(
                journal.RequestId,
                "D",
                out _)
            || !Guid.TryParseExact(
                journal.PackageId,
                "D",
                out _)
            || !Guid.TryParseExact(
                journal.PreRestorePackageId,
                "D",
                out _)
            || !BackupHubContract.IsLowercaseSha256(
                journal.PreviousSha256)
            || !BackupHubContract.IsLowercaseSha256(
                journal.CandidateSha256))
        {
            throw new InvalidDataException(
                "The restore journal is invalid.");
        }
        return journal;
    }

    private void RequireNoSqliteSidecars()
    {
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecar = $"{_databasePath}{suffix}";
            if (File.Exists(sidecar)
                && new FileInfo(sidecar).Length > 0)
            {
                throw new InvalidOperationException(
                    $"The current database has an active SQLite sidecar: {sidecar}");
            }
        }
    }

    private static void FlushFile(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read);
        stream.Flush(flushToDisk: true);
    }

    private static void ClaimPreparedRequests(
        RestoreLocations locations)
    {
        foreach (var source in RestoreDirectories(
                     locations.Outbox))
        {
            var destination = Path.Combine(
                locations.Processing,
                Path.GetFileName(source));
            if (!Directory.Exists(destination))
            {
                Directory.Move(source, destination);
            }
        }
    }

    private static IReadOnlyList<string> RestoreDirectories(
        string directory) =>
        Directory.EnumerateDirectories(
                directory,
                "*.bhrestore")
            .Where(path =>
                Guid.TryParseExact(
                    Path.GetFileNameWithoutExtension(path),
                    "D",
                    out var id)
                && Path.GetFileNameWithoutExtension(path)
                    == BackupHubContract.Canonical(id)
                && new DirectoryInfo(path).LinkTarget is null)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static Guid RequestIdFromDirectory(
        string directory)
    {
        var identity = Path.GetFileNameWithoutExtension(
            directory);
        if (!Guid.TryParseExact(identity, "D", out var id)
            || identity != BackupHubContract.Canonical(id))
        {
            throw new InvalidDataException(
                "A restore request directory does not have a canonical UUID.");
        }
        return id;
    }

    private static void PublishTerminal(
        RestoreResult result,
        string claimed,
        RestoreLocations locations,
        bool deferClaimFinalization = false)
    {
        result.Validate();
        var destination = locations.ResultPath(
            Guid.Parse(result.RequestId));
        if (File.Exists(destination))
        {
            throw new IOException(
                "A terminal restore result already exists.");
        }
        var temporary = Path.Combine(
            locations.Results,
            $".{result.RequestId}.tmp");
        BackupHubContract.WriteJsonDurably(
            temporary,
            result);
        File.Move(temporary, destination);
        BackupHubContract.FlushDirectory(
            locations.Results);
        if (!deferClaimFinalization)
        {
            FinalizeClaim(
                claimed,
                locations,
                result.State is "applied" or "cancelled");
        }
    }

    private static void FinalizeExistingResult(
        string claimed,
        RestoreLocations locations,
        Guid requestId)
    {
        var result = JsonSerializer.Deserialize<RestoreResult>(
                File.ReadAllBytes(
                    locations.ResultPath(requestId)),
                BackupHubContract.JsonOptions)
            ?? throw new InvalidDataException(
                "An existing restore result is empty.");
        result.Validate();
        FinalizeClaim(
            claimed,
            locations,
            result.State is "applied" or "cancelled");
    }

    private static void FinalizeClaim(
        string claimed,
        RestoreLocations locations,
        bool appliedOrCancelled)
    {
        if (appliedOrCancelled)
        {
            Directory.Delete(claimed, recursive: true);
            return;
        }
        var destination = Path.Combine(
            locations.Quarantine,
            Path.GetFileName(claimed));
        if (Directory.Exists(destination))
        {
            throw new IOException(
                "A restore quarantine entry already exists.");
        }
        Directory.Move(claimed, destination);
    }

    private static bool TryResolveVault(
        out string vault)
    {
        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);
        var candidate = Path.Combine(
            applicationData,
            BackupHubVaultLocation.VaultIdentifier,
            "vault");
        if (!Directory.Exists(candidate))
        {
            vault = "";
            return false;
        }
        vault = BackupHubVaultLocation.RequireVault();
        return true;
    }
}

internal sealed record RestoreLocations(string Vault)
{
    public string Outbox => Path.Combine(
        Vault,
        "restore-outbox",
        BackupHubBackupService.ApplicationId);

    public string Processing => Path.Combine(
        Vault,
        "restore-processing",
        BackupHubBackupService.ApplicationId);

    public string Results => Path.Combine(
        Vault,
        "restore-results",
        BackupHubBackupService.ApplicationId);

    public string Quarantine => Path.Combine(
        Vault,
        "restore-quarantine",
        BackupHubBackupService.ApplicationId);

    public string Retired => Path.Combine(
        Vault,
        "restore-retired-v1-processing",
        BackupHubBackupService.ApplicationId);

    public string OwnerMarker => Path.Combine(
        Vault,
        "restore-owner-protocol",
        $"{BackupHubBackupService.ApplicationId}.version");

    public string ResultPath(Guid requestId) =>
        Path.Combine(
            Results,
            $"{BackupHubContract.Canonical(requestId)}.json");

    public void PrepareOwnership()
    {
        var ownerDirectory = Path.GetDirectoryName(
            OwnerMarker)!;
        Directory.CreateDirectory(ownerDirectory);
        BackupHubContract.RequireRegularDirectory(
            ownerDirectory,
            "restore owner protocol directory");
        if (File.Exists(OwnerMarker))
        {
            BackupHubContract.RequireRegularFile(
                OwnerMarker,
                "restore owner protocol marker");
            if (!File.ReadAllBytes(OwnerMarker)
                    .SequenceEqual("2\n"u8.ToArray()))
            {
                throw new InvalidDataException(
                    "The MOCKUPS restore owner protocol marker is invalid.");
            }
        }
        else
        {
            if (Directory.Exists(Processing))
            {
                BackupHubContract.RequireRegularDirectory(
                    Processing,
                    "restore processing directory");
            }
            if (Directory.Exists(Retired))
            {
                BackupHubContract.RequireRegularDirectory(
                    Retired,
                    "retired restore processing directory");
            }
            if (Directory.Exists(Processing)
                && Directory.Exists(Retired))
            {
                throw new InvalidDataException(
                    "Both active and retired MOCKUPS restore processing directories exist.");
            }
            if (Directory.Exists(Processing))
            {
                Directory.CreateDirectory(
                    Path.GetDirectoryName(Retired)!);
                Directory.Move(Processing, Retired);
            }
            var temporaryMarker =
                $"{OwnerMarker}.{Guid.NewGuid():D}.tmp";
            BackupHubContract.WriteBytesDurably(
                temporaryMarker,
                "2\n"u8.ToArray());
            File.Move(temporaryMarker, OwnerMarker);
            BackupHubContract.FlushDirectory(
                Path.GetDirectoryName(OwnerMarker)!);
        }

        foreach (var directory in new[]
                 {
                     Outbox,
                     Processing,
                     Results,
                     Quarantine,
                 })
        {
            Directory.CreateDirectory(directory);
            BackupHubContract.RequireRegularDirectory(
                directory,
                "Backup Hub restore directory");
        }
    }
}

internal sealed class RestoreContractException(
    string code,
    Guid? packageId,
    string message) : Exception(message)
{
    public string Code { get; } = code;

    public Guid? PackageId { get; } = packageId;
}

internal sealed record RestoreRequest(
    int HandoffVersion,
    string RequestId,
    string ApplicationId,
    string PackageId,
    string PreparedAt,
    string VaultObjectSha256,
    string ManifestSha256,
    RestoreBackupSummary BackupSummary,
    string State);

internal sealed record RestoreBackupSummary(
    string CreatedAt,
    string Reason,
    string SnapshotFormat,
    string SnapshotSchemaVersion,
    long FileCount,
    long TotalBytes);

internal sealed record RestoreJournal(
    string RequestId,
    string PackageId,
    string PreRestorePackageId,
    string PreviousSha256,
    string CandidateSha256);

internal sealed record RestoreError(
    string Code,
    string Message);

internal sealed record RestoreResult(
    int HandoffVersion,
    string RequestId,
    string ApplicationId,
    [property: JsonIgnore(
        Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? PackageId,
    [property: JsonIgnore(
        Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? PreRestorePackageId,
    string CompletedAt,
    string State,
    string UserDecision,
    [property: JsonIgnore(
        Condition = JsonIgnoreCondition.WhenWritingNull)]
    RestoreError? Error)
{
    public static RestoreResult Applied(
        Guid requestId,
        Guid packageId,
        Guid preRestorePackageId) =>
        Create(
            requestId,
            packageId,
            preRestorePackageId,
            "applied",
            "confirmed",
            error: null);

    public static RestoreResult Cancelled(
        Guid requestId,
        Guid packageId) =>
        Create(
            requestId,
            packageId,
            preRestorePackageId: null,
            "cancelled",
            "cancelled",
            error: null);

    public static RestoreResult Rejected(
        Guid requestId,
        Guid? packageId,
        string code,
        string message) =>
        Create(
            requestId,
            packageId,
            preRestorePackageId: null,
            "rejected",
            "not-presented",
            new RestoreError(code, NonEmpty(message)));

    public static RestoreResult Failed(
        Guid requestId,
        Guid packageId,
        Guid? preRestorePackageId,
        string userDecision,
        string code,
        string message) =>
        Create(
            requestId,
            packageId,
            preRestorePackageId,
            "failed",
            userDecision,
            new RestoreError(code, NonEmpty(message)));

    public void Validate()
    {
        if (HandoffVersion != 2
            || !Guid.TryParseExact(
                RequestId,
                "D",
                out var requestId)
            || RequestId
                != BackupHubContract.Canonical(requestId)
            || (PackageId is not null
                && !IsCanonicalUuid(PackageId))
            || (PreRestorePackageId is not null
                && !IsCanonicalUuid(PreRestorePackageId))
            || ApplicationId
                != BackupHubBackupService.ApplicationId
            || !DateTimeOffset.TryParse(
                CompletedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            throw new InvalidDataException(
                "Restore Result v2 identity is invalid.");
        }
        var valid = State switch
        {
            "applied" =>
                UserDecision == "confirmed"
                && PackageId is not null
                && PreRestorePackageId is not null
                && Error is null,
            "cancelled" =>
                UserDecision == "cancelled"
                && PackageId is not null
                && PreRestorePackageId is null
                && Error is null,
            "rejected" when Error?.Code
                == "request-invalid" =>
                UserDecision == "not-presented"
                && PackageId is null
                && PreRestorePackageId is null,
            "rejected" =>
                UserDecision == "not-presented"
                && PackageId is not null
                && PreRestorePackageId is null
                && Error?.Code is
                    "identity-mismatch"
                    or "contract-mismatch"
                    or "manifest-hash-mismatch"
                    or "payload-incomplete"
                    or "payload-hash-mismatch"
                    or "snapshot-invalid",
            "failed" when Error?.Code
                == "confirmation-failed" =>
                UserDecision == "not-presented"
                && PackageId is not null
                && PreRestorePackageId is null,
            "failed" when Error?.Code
                == "pre-restore-backup-failed" =>
                UserDecision == "confirmed"
                && PackageId is not null
                && PreRestorePackageId is null,
            "failed" =>
                UserDecision == "confirmed"
                && PackageId is not null
                && PreRestorePackageId is not null
                && Error?.Code is
                    "replacement-failed"
                    or "verification-failed"
                    or "post-pre-restore-internal-error",
            _ => false,
        };
        if (!valid)
        {
            throw new InvalidDataException(
                "Restore Result v2 state invariants are invalid.");
        }
    }

    private static bool IsCanonicalUuid(string value) =>
        Guid.TryParseExact(value, "D", out var identity)
        && value == BackupHubContract.Canonical(identity);

    private static RestoreResult Create(
        Guid requestId,
        Guid? packageId,
        Guid? preRestorePackageId,
        string state,
        string userDecision,
        RestoreError? error) =>
        new(
            2,
            BackupHubContract.Canonical(requestId),
            BackupHubBackupService.ApplicationId,
            packageId is null
                ? null
                : BackupHubContract.Canonical(
                    packageId.Value),
            preRestorePackageId is null
                ? null
                : BackupHubContract.Canonical(
                    preRestorePackageId.Value),
            BackupHubContract.TimestampNow(),
            state,
            userDecision,
            error);

    private static string NonEmpty(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "Restore failed without additional detail."
            : value;
}
