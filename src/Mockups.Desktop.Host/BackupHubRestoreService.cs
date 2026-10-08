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

    public Task<IReadOnlyList<RestoreNotification>>
        ProcessPendingAsync(
            Func<PendingRestore, Task<bool>> confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        return Task.Run(() => ProcessPendingCoreAsync(confirm));
    }

    private async Task<IReadOnlyList<RestoreNotification>> ProcessPendingCoreAsync(
        Func<PendingRestore, Task<bool>> confirm)
    {
        if (!_backups.Vault.TryRequireVault(out var vault))
        {
            return [];
        }

        var locations = new RestoreLocations(vault);
        locations.PrepareOwnership();
        var notifications = new List<RestoreNotification>();
        RecoverStaging();
        foreach (var cleanup in TransactionDirectories("cleanup"))
        {
            TryCleanup(cleanup, locations, notifications);
        }
        RecoverInterruptedTransactions(
            locations,
            notifications);
        ClaimPreparedRequests(locations);
        foreach (var claimed in RestoreDirectories(
                     locations.Processing))
        {
            var requestId = RequestIdFromDirectory(
                claimed);
            if (Directory.Exists(CleanupPath(requestId)))
            {
                continue;
            }
            if (File.Exists(
                    locations.ResultPath(requestId)))
            {
                throw new InvalidDataException(
                    "A restore request has a terminal result but no owned cleanup phase.");
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
                locations, notifications);
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
                locations, notifications);
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
                locations, notifications);
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
                locations, notifications);
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
                locations, notifications);
            notifications.Add(new RestoreNotification(
                "No se pudo proteger la versión actual",
                exception.Message,
                IsError: true));
            return;
        }

        RestoreResult result;
        try
        {
            _ = ApplyReplacement(
                requestId,
                Guid.Parse(request.PackageId),
                preRestore.PackageId,
                Path.Combine(
                    claimed,
                    "package",
                    "payload",
                    BackupHubBackupService.PayloadPath));
            result = RestoreResult.Applied(requestId, Guid.Parse(request.PackageId), preRestore.PackageId);
        }
        catch (Exception exception)
        {
            result = RestoreResult.Failed(requestId, Guid.Parse(request.PackageId),
                preRestore.PackageId, "confirmed", "replacement-failed", exception.Message);
        }
        // The decision is outside the replacement catch. Once it is persisted,
        // delivery and cleanup failures can never choose a different outcome.
        PublishTerminal(result, claimed, locations, notifications);
        notifications.Add(new RestoreNotification(
            result.State == "applied" ? "Backup restaurado" : "La restauración no se aplicó",
            result.Error?.Message ?? "MOCKUPS restauró y verificó la base de datos. La versión reemplazada está guardada en Backup Hub.",
            IsError: result.State != "applied"));
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
            BackupHubContract.RequireRegularFile(previous, "restore rollback source");
            if (BackupHubContract.HashFile(previous) != journal.PreviousSha256)
            {
                throw new InvalidDataException("The rollback source differs from its journal.");
            }
            var recovery = Path.Combine(
                transaction,
                "recovery.sqlite");
            if (Path.Exists(recovery)) BackupHubContract.RequireRegularFile(recovery, "rollback staging file");
            File.Copy(previous, recovery, overwrite: true);
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
        // Keep the journal until result publication has been durably completed.
    }

    private void RecoverInterruptedTransactions(
        RestoreLocations locations,
        List<RestoreNotification> notifications)
    {
        foreach (var transaction in TransactionDirectories("txn"))
        {
            var requestId = TransactionId(transaction);
            var outcome = Path.Combine(transaction, "outcome.json");
            if (!File.Exists(outcome))
            {
                var journal = ReadJournal(transaction);
                if (File.Exists(locations.ResultPath(requestId)))
                {
                    throw new InvalidDataException("A published result has no local durable decision.");
                }
                CommitOutcome(transaction, RestoreResult.Failed(requestId,
                    Guid.Parse(journal.PackageId), Guid.Parse(journal.PreRestorePackageId),
                    "confirmed", "post-pre-restore-internal-error",
                    "MOCKUPS recovered the previous database after an interrupted restore."));
            }
            var result = ReadOutcome(transaction);
            CompleteDecision(transaction, result, locations, notifications);
            notifications.Add(new RestoreNotification(
                "Restauración pendiente finalizada",
                result.State == "applied" ? "Se completó la entrega del resultado de la restauración confirmada."
                    : result.Error?.Message ?? "Se completó la solicitud pendiente sin aplicar el backup.",
                IsError: result.State is "failed" or "rejected"));
        }
    }

    private static RestoreJournal ReadJournal(
        string transaction)
    {
        BackupHubContract.RequireRegularDirectory(transaction, "restore transaction");
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
        if (journal.RequestId != BackupHubContract.Canonical(TransactionId(transaction))
            || !Guid.TryParseExact(
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
                BackupHubContract.FlushDirectory(locations.Outbox);
                BackupHubContract.FlushDirectory(locations.Processing);
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

    private void PublishTerminal(
        RestoreResult result,
        string claimed,
        RestoreLocations locations,
        List<RestoreNotification> notifications)
    {
        result.Validate();
        var requestId = RequestIdFromDirectory(claimed);
        if (result.RequestId != BackupHubContract.Canonical(requestId))
        {
            throw new InvalidDataException("The restore decision belongs to another request.");
        }
        var transaction = TransactionPath(requestId);
        if (!Directory.Exists(transaction))
        {
            // Non-replacement outcomes use the same durable decision owner.
            // Publishing the complete directory avoids an empty live journal.
            var staging = TransactionPath(requestId, "decision");
            Directory.CreateDirectory(staging);
            BackupHubContract.RequireRegularDirectory(staging, "restore decision staging");
            CommitOutcome(staging, result);
            Directory.Move(staging, transaction);
            BackupHubContract.FlushDirectory(Path.GetDirectoryName(transaction)!);
        }
        else CommitOutcome(transaction, result);
        CompleteDecision(transaction, result, locations, notifications);
    }

    private void CompleteDecision(
        string transaction,
        RestoreResult result,
        RestoreLocations locations,
        List<RestoreNotification> notifications)
    {
        var requestId = TransactionId(transaction);
        var journalPath = Path.Combine(transaction, "journal.json");
        if (Path.Exists(journalPath))
        {
            var journal = ReadJournal(transaction);
            if (result.PackageId != journal.PackageId || result.PreRestorePackageId != journal.PreRestorePackageId
                || result.State is not ("applied" or "failed"))
            {
                throw new InvalidDataException("The restore decision does not match its replacement journal.");
            }
            if (result.State == "applied")
            {
                _ = SqliteDatabaseSnapshotService.Validate(_databasePath);
                if (BackupHubContract.HashFile(_databasePath) != journal.CandidateSha256)
                    throw new InvalidDataException("The committed restore does not match the live database.");
            }
            else RollBackReplacement(transaction);
        }
        else if (result.State == "applied")
        {
            throw new InvalidDataException("An applied decision requires its replacement journal.");
        }

        var bytes = File.ReadAllBytes(Path.Combine(transaction, "outcome.json"));
        var destination = locations.ResultPath(requestId);
        if (Path.Exists(destination))
        {
            BackupHubContract.RequireRegularFile(destination, "restore terminal result");
            if (!File.ReadAllBytes(destination).SequenceEqual(bytes))
                throw new InvalidDataException("The published restore result differs from its durable decision.");
        }
        else WriteAtomicDocument(destination, bytes,
            Path.Combine(locations.Results, $".{result.RequestId}.tmp"));
        BackupHubContract.FlushDirectory(locations.Results);

        // Directory identity marks cleanup-only work. Once promoted, even later
        // authoring changes must never cause database verification or rollback.
        var cleanup = CleanupPath(requestId);
        Directory.Move(transaction, cleanup);
        BackupHubContract.FlushDirectory(Path.GetDirectoryName(cleanup)!);
        TryCleanup(cleanup, locations, notifications);
    }

    private static void CommitOutcome(string transaction, RestoreResult result)
    {
        BackupHubContract.RequireRegularDirectory(transaction, "restore transaction");
        result.Validate();
        if (result.RequestId != BackupHubContract.Canonical(TransactionId(transaction)))
            throw new InvalidDataException("The decision identity does not match its transaction.");
        var path = Path.Combine(transaction, "outcome.json");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(result, BackupHubContract.JsonOptions);
        if (Path.Exists(path))
        {
            _ = ReadOutcome(transaction);
            if (!File.ReadAllBytes(path).SequenceEqual(bytes))
                throw new InvalidDataException("A restore decision cannot be changed after commitment.");
        }
        else WriteAtomicDocument(path, bytes, Path.Combine(transaction, "outcome.tmp"));
        BackupHubContract.FlushDirectory(transaction);
    }

    private static RestoreResult ReadOutcome(string transaction)
    {
        BackupHubContract.RequireRegularDirectory(transaction, "restore transaction");
        var path = Path.Combine(transaction, "outcome.json");
        BackupHubContract.RequireRegularFile(path, "restore decision");
        var result = JsonSerializer.Deserialize<RestoreResult>(File.ReadAllBytes(path), BackupHubContract.JsonOptions)
            ?? throw new InvalidDataException("The restore decision is empty.");
        result.Validate();
        if (result.RequestId != BackupHubContract.Canonical(TransactionId(transaction)))
            throw new InvalidDataException("The decision identity does not match its transaction.");
        return result;
    }

    private static void WriteAtomicDocument(string destination, byte[] bytes, string temporary)
    {
        // This exact scratch file is owned by the pending write, not an alternate
        // source. A partial previous write is recreated from the durable decision.
        if (Path.Exists(temporary)) BackupHubContract.RequireRegularFile(temporary, "restore document staging");
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, destination);
        BackupHubContract.FlushDirectory(Path.GetDirectoryName(destination)!);
    }

    private static void TryCleanup(string cleanup, RestoreLocations locations, List<RestoreNotification> notifications)
    {
        BackupHubContract.RequireRegularDirectory(cleanup, "restore cleanup");
        var claimed = Path.Combine(locations.Processing, $"{TransactionId(cleanup):D}.bhrestore");
        try
        {
            if (Path.Exists(claimed))
            {
                BackupHubContract.RequireRegularDirectory(claimed, "restore claimed request");
                var result = ReadOutcome(cleanup);
                FinalizeClaim(claimed, locations, result.State is "applied" or "cancelled");
                BackupHubContract.FlushDirectory(locations.Processing);
                if (result.State is not ("applied" or "cancelled")) BackupHubContract.FlushDirectory(locations.Quarantine);
            }
            // Claim finalization precedes removal of the local decision. Partial
            // directory deletion therefore needs no database or decision replay.
            Directory.Delete(cleanup, recursive: true);
            BackupHubContract.FlushDirectory(Path.GetDirectoryName(cleanup)!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            notifications.Add(new RestoreNotification("Limpieza de restauración pendiente",
                $"El resultado está guardado y no se modificará. Se reintentará la limpieza al abrir MOCKUPS. {exception.Message}", true));
        }
    }

    private string TransactionPath(Guid requestId, string phase = "txn") =>
        Path.Combine(Path.GetDirectoryName(_databasePath)!, $".mockups-restore-{requestId:D}.{phase}");

    private string CleanupPath(Guid requestId) => TransactionPath(requestId, "cleanup");

    private IReadOnlyList<string> TransactionDirectories(string phase) =>
        Directory.EnumerateDirectories(Path.GetDirectoryName(_databasePath)!, $".mockups-restore-*.{phase}")
            .Order(StringComparer.Ordinal).ToArray();

    private static Guid TransactionId(string path)
    {
        const string prefix = ".mockups-restore-";
        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(name[prefix.Length..], "D", out var id)
            || name[prefix.Length..] != BackupHubContract.Canonical(id))
            throw new InvalidDataException("Invalid restore transaction directory identity.");
        return id;
    }

    private void RecoverStaging()
    {
        foreach (var staging in TransactionDirectories("decision"))
        {
            BackupHubContract.RequireRegularDirectory(staging, "restore decision staging");
            if (File.Exists(Path.Combine(staging, "outcome.json")))
            {
                var result = ReadOutcome(staging);
                Directory.Move(staging, TransactionPath(Guid.Parse(result.RequestId)));
            }
            else Directory.Delete(staging, recursive: true);
            BackupHubContract.FlushDirectory(Path.GetDirectoryName(staging)!);
        }
        foreach (var staging in Directory.EnumerateDirectories(Path.GetDirectoryName(_databasePath)!, ".*.restore-tmp"))
        {
            var name = Path.GetFileNameWithoutExtension(staging)[1..];
            if (!Guid.TryParseExact(name, "D", out var id) || name != BackupHubContract.Canonical(id)) continue;
            BackupHubContract.RequireRegularDirectory(staging, "restore replacement staging");
            // Replacement cannot start before this staging directory is promoted.
            Directory.Delete(staging, recursive: true);
            BackupHubContract.FlushDirectory(Path.GetDirectoryName(staging)!);
        }
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
            var existingRestoreDirectories = new[]
            {
                Outbox,
                Processing,
                Results,
                Quarantine,
            }.Where(Directory.Exists).ToArray();
            if (existingRestoreDirectories.Length > 0)
            {
                throw new InvalidDataException(
                    "The MOCKUPS Restore Handoff owner marker is missing while restore directories already exist. Run explicit Backup Hub maintenance before opening MOCKUPS.");
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
