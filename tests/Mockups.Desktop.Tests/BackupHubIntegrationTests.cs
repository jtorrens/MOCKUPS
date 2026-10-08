using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json;

internal static class BackupHubIntegrationTests
{
    public static void CloseWaitsForWrite(string source) =>
        Task.Run(() => CloseWaitsForWriteAsync(source)).GetAwaiter().GetResult();

    private static async Task CloseWaitsForWriteAsync(string source)
    {
        using var fixture = new Fixture(source);
        using var operations = new EditorOperationCoordinator();
        var lifecycle = new BackupHubApplicationLifecycle(fixture.Backups, fixture.Backups.CaptureDatabaseFingerprint());
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writer = operations.ExecuteAsync(() =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Test write was not released.");
            fixture.WriteApplicationId(456);
        });
        Task<ApplicationBackupResult>? closing = null;
        try
        {
            Equal(true, entered.Wait(TimeSpan.FromSeconds(10)));
            closing = lifecycle.PublishCleanExitAsync(operations);
            Equal(false, await Task.WhenAny(closing, Task.Delay(3500)) == closing);
        }
        finally
        {
            release.Set();
            await writer;
        }
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
        {
            Throws<PlatformNotSupportedException>(() => closing!.GetAwaiter().GetResult());
            return;
        }
        var result = await closing!.WaitAsync(TimeSpan.FromSeconds(60));
        Equal(ApplicationBackupOutcome.Published, result.Outcome);
        var packagePath = Path.Combine(fixture.Inbox, result.PackageName!);
        var id = Guid.Parse(Path.GetFileNameWithoutExtension(result.PackageName!));
        var manifest = BackupPackageValidator.Validate(packagePath, id);
        Equal("clean-exit", manifest.Reason);
        Equal(fixture.Backups.CaptureDatabaseFingerprint(), manifest.Files.Single().Sha256);
        Equal(1, Directory.GetFileSystemEntries(fixture.Inbox).Length);
    }

    public static void CloseFailureCanRetry(string source) =>
        Task.Run(() => CloseFailureCanRetryAsync(source)).GetAwaiter().GetResult();

    private static async Task CloseFailureCanRetryAsync(string source)
    {
        using var fixture = new Fixture(source);
        using var operations = new EditorOperationCoordinator();
        var lifecycle = new BackupHubApplicationLifecycle(fixture.Backups, fixture.Backups.CaptureDatabaseFingerprint());
        Directory.Delete(fixture.Inbox);
        try
        {
            await lifecycle.PublishCleanExitAsync(operations);
            throw new Exception("Missing inbox must reject publication.");
        }
        catch (InvalidDataException) { }
        await operations.ExecuteAsync(() => fixture.WriteApplicationId(789));
        Directory.CreateDirectory(fixture.Inbox);
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
        {
            Throws<PlatformNotSupportedException>(() => lifecycle.PublishCleanExitAsync(operations).GetAwaiter().GetResult());
            return;
        }
        var result = await lifecycle.PublishCleanExitAsync(operations);
        Equal(ApplicationBackupOutcome.Published, result.Outcome);
        var path = Path.Combine(fixture.Inbox, result.PackageName!);
        var manifest = BackupPackageValidator.Validate(path, Guid.Parse(Path.GetFileNameWithoutExtension(result.PackageName!)));
        Equal(fixture.Backups.CaptureDatabaseFingerprint(), manifest.Files.Single().Sha256);
        Equal(1, Directory.GetFileSystemEntries(fixture.Inbox).Length);
    }

    public static void VaultValidation(string source)
    {
        using var fixture = new Fixture(source);
        Equal(fixture.VaultPath, fixture.Backups.Vault.RequireVault());
        File.WriteAllText(Path.Combine(fixture.VaultPath, "vault-layout.json"), "{}");
        Throws<InvalidDataException>(() => fixture.Run(_ => true));
        Throws<InvalidDataException>(() => fixture.Backups.Publish(BackupReason.Manual));
        Equal(0, Directory.GetFileSystemEntries(fixture.Inbox).Length);
        var missing = Path.Combine(fixture.Root, "missing-vault");
        var backups = new BackupHubBackupService(fixture.Database, new BackupHubVaultLocation(missing));
        Equal(0, new BackupHubRestoreService(fixture.Database, backups)
            .ProcessPendingAsync(_ => throw new Exception("Must not confirm")).GetAwaiter().GetResult().Count);
        Equal(false, Directory.Exists(missing));
        Throws<InvalidDataException>(() => backups.Publish(BackupReason.Manual));
        Throws<ArgumentException>(() => new BackupHubVaultLocation("relative-vault"));
    }

    public static void Publication(string source)
    {
        using var fixture = new Fixture(source);
        var original = BackupHubContract.HashFile(fixture.Database);
        var fingerprint = fixture.Backups.CaptureDatabaseFingerprint();
        // Vault Location v1 publication supports macOS and Windows only. Linux CI
        // verifies the explicit rejection and cleanup, never invents a producer.
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
        {
            Throws<PlatformNotSupportedException>(() => fixture.Backups.Publish(BackupReason.Manual));
            Equal(0, Directory.GetFileSystemEntries(fixture.Inbox).Length);
            Equal(original, BackupHubContract.HashFile(fixture.Database));
            return;
        }
        var manual = fixture.Backups.Publish(BackupReason.Manual)!;
        Equal(fingerprint, manual.DatabaseSha256);
        Equal("manual", BackupPackageValidator.Validate(manual.PackagePath, manual.PackageId).Reason);
        Equal(null, fixture.Backups.Publish(BackupReason.CleanExit, fingerprint));
        Equal(1, Directory.GetFileSystemEntries(fixture.Inbox).Length);
        var forced = fixture.Backups.Publish(BackupReason.PreMigration, fingerprint)!;
        Equal("pre-migration", BackupPackageValidator.Validate(forced.PackagePath, forced.PackageId).Reason);
        Equal(2, Directory.GetFileSystemEntries(fixture.Inbox).Length);
        Equal(original, BackupHubContract.HashFile(fixture.Database));
    }

    public static void ConfirmedRestore(string source)
    {
        using var fixture = new Fixture(source);
        var request = fixture.Prepare();
        var previous = BackupHubContract.HashFile(fixture.Database);
        var calls = 0;
        var notifications = fixture.Run(pending =>
        {
            Equal(request, pending.RequestId);
            calls++;
            return true;
        });
        Equal(1, calls);
        var result = fixture.Result(request);
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
        {
            Equal("pre-restore-backup-failed", result.Error!.Code);
            Equal(previous, BackupHubContract.HashFile(fixture.Database));
            Equal(0, Directory.GetFileSystemEntries(fixture.Inbox).Length);
            return;
        }
        Equal("applied", result.State);
        Equal(fixture.CandidateHash, BackupHubContract.HashFile(fixture.Database));
        Equal(false, notifications.Single().IsError);
        var preRestore = Guid.Parse(result.PreRestorePackageId!);
        var prePath = Path.Combine(fixture.Inbox, $"{preRestore:D}.bhpkg");
        Equal("pre-restore", BackupPackageValidator.Validate(prePath, preRestore).Reason);
        using (var database = new SqliteConnection($"Data Source={Path.Combine(prePath, "payload", "mockups.sqlite")};Mode=ReadOnly;Pooling=False"))
        {
            database.Open();
            using var command = database.CreateCommand();
            command.CommandText = "PRAGMA application_id";
            Equal(123L, (long)command.ExecuteScalar()!);
        }
        Equal(0, Directory.GetFileSystemEntries(fixture.Locations.Processing).Length);
        Equal(0, Directory.GetDirectories(fixture.Root, ".mockups-restore-*.txn").Length);
        Equal(0, fixture.Run(_ => throw new Exception("Already processed")).Count);
        Equal(1, Directory.GetFileSystemEntries(fixture.Inbox).Length);
    }

    public static void CancelAndConfirmationFailure(string source)
    {
        foreach (var fail in new[] { false, true })
        {
            using var fixture = new Fixture(source);
            var request = fixture.Prepare();
            var previous = BackupHubContract.HashFile(fixture.Database);
            fixture.Run(_ => fail ? throw new IOException("Confirmation unavailable") : false);
            var result = fixture.Result(request);
            Equal(fail ? "failed" : "cancelled", result.State);
            Equal(fail ? "confirmation-failed" : null, result.Error?.Code);
            Equal(previous, BackupHubContract.HashFile(fixture.Database));
            Equal(0, Directory.GetFileSystemEntries(fixture.Inbox).Length);
            Equal(0, fixture.Run(_ => throw new Exception("Already processed")).Count);
        }
    }

    public static void RejectInvalidHandoffs(string source)
    {
        foreach (var damage in new[] { "request", "identity", "manifest", "payload", "schema", "summary" })
        {
            using var fixture = new Fixture(source);
            var requestId = fixture.Prepare();
            var path = Path.Combine(fixture.ClaimPath(requestId, fixture.Locations.Outbox), "request.json");
            var request = Read<RestoreRequest>(path);
            var expectedCode = damage switch
            {
                "request" => "request-invalid",
                "identity" => "identity-mismatch",
                "manifest" => "manifest-hash-mismatch",
                "summary" => "contract-mismatch",
                _ => "snapshot-invalid",
            };
            if (damage == "request") File.WriteAllText(path, "{}");
            if (damage == "identity") Write(path, request with { ApplicationId = "not-mockups" });
            if (damage == "manifest") Write(path, request with { ManifestSha256 = new string('0', 64) });
            if (damage == "summary") Write(path, request with { BackupSummary = request.BackupSummary with { TotalBytes = 0 } });
            if (damage == "payload") File.AppendAllText(fixture.CandidatePath(requestId), "corrupt");
            if (damage == "schema")
            {
                var manifestPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(fixture.CandidatePath(requestId)))!, "manifest.json");
                var manifest = Read<BackupManifest>(manifestPath);
                Write(manifestPath, manifest with { Snapshot = manifest.Snapshot with { SchemaVersion = "0" } });
            }
            var previous = BackupHubContract.HashFile(fixture.Database);
            fixture.Run(_ => throw new Exception("Invalid requests must not reach confirmation"));
            Equal("rejected", fixture.Result(requestId).State);
            Equal(expectedCode, fixture.Result(requestId).Error!.Code);
            Equal(previous, BackupHubContract.HashFile(fixture.Database));
            Equal(true, Directory.Exists(fixture.ClaimPath(requestId, fixture.Locations.Quarantine)));
            Equal(0, Directory.GetFileSystemEntries(fixture.Inbox).Length);
        }
    }

    public static void FailedPreBackup(string source)
    {
        using var fixture = new Fixture(source);
        var request = fixture.Prepare();
        var previous = BackupHubContract.HashFile(fixture.Database);
        fixture.Run(_ =>
        {
            Directory.Delete(fixture.Inbox);
            return true;
        });
        Equal("pre-restore-backup-failed", fixture.Result(request).Error!.Code);
        Equal(null, fixture.Result(request).PreRestorePackageId);
        Equal(previous, BackupHubContract.HashFile(fixture.Database));
    }

    public static void ReplacementBlocked(string source)
    {
        using var fixture = new Fixture(source);
        var request = fixture.Prepare();
        var previous = BackupHubContract.HashFile(fixture.Database);
        fixture.Run(_ =>
        {
            File.WriteAllText(WorkstationUpdateMaintenance.LockFilePath(fixture.Database), "test maintenance");
            return true;
        });
        var supported = OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();
        Equal(supported ? "replacement-failed" : "pre-restore-backup-failed", fixture.Result(request).Error!.Code);
        Equal(supported, fixture.Result(request).PreRestorePackageId is not null);
        Equal(previous, BackupHubContract.HashFile(fixture.Database));
        Equal(0, Directory.GetDirectories(fixture.Root, ".mockups-restore-*.txn").Length);
    }

    public static void InterruptedRestore(string source)
    {
        foreach (var phase in new[] { "prepared", "replaced", "applied" })
        {
            var completed = phase == "applied";
            using var fixture = new Fixture(source);
            var request = fixture.Prepare();
            var previous = BackupHubContract.HashFile(fixture.Database);
            var preId = Guid.NewGuid();
            var transaction = Path.Combine(fixture.Root, $".mockups-restore-{request:D}.txn");
            Directory.CreateDirectory(transaction);
            if (phase != "prepared")
            {
                File.Copy(fixture.Database, Path.Combine(transaction, "previous.sqlite"));
                File.Copy(fixture.CandidatePath(request), fixture.Database, overwrite: true);
            }
            else File.Copy(fixture.CandidatePath(request), Path.Combine(transaction, "replacement.sqlite"));
            BackupHubContract.WriteJsonDurably(Path.Combine(transaction, "journal.json"), new RestoreJournal(
                request.ToString("D"), fixture.PackageId.ToString("D"), preId.ToString("D"), previous, fixture.CandidateHash));
            Directory.Move(fixture.ClaimPath(request, fixture.Locations.Outbox), fixture.ClaimPath(request, fixture.Locations.Processing));
            if (completed)
            {
                var outcome = RestoreResult.Applied(request, fixture.PackageId, preId);
                BackupHubContract.WriteJsonDurably(Path.Combine(transaction, "outcome.json"), outcome);
                BackupHubContract.WriteJsonDurably(fixture.Locations.ResultPath(request), outcome);
            }
            fixture.Run(_ => throw new Exception("Crash recovery must not confirm again"));
            Equal(completed ? fixture.CandidateHash : previous, BackupHubContract.HashFile(fixture.Database));
            Equal(completed ? "applied" : "failed", fixture.Result(request).State);
            Equal(completed ? null : "post-pre-restore-internal-error", fixture.Result(request).Error?.Code);
            Equal(false, Directory.Exists(transaction));
            Equal(false, Directory.Exists(fixture.ClaimPath(request, fixture.Locations.Processing)));
            Equal(0, fixture.Run(_ => throw new Exception("Already recovered")).Count);
        }
    }

    public static void InterruptedDecisionDelivery(string source)
    {
        foreach (var blocked in new[] { false, true })
        {
            using var fixture = new Fixture(source);
            var (request, transaction, _, preId) = fixture.PrepareReplacement();
            var outcome = RestoreResult.Applied(request, fixture.PackageId, preId);
            BackupHubContract.WriteJsonDurably(Path.Combine(transaction, "outcome.json"), outcome);
            var temporary = Path.Combine(fixture.Locations.Results, $".{request:D}.tmp");
            if (blocked) Directory.CreateDirectory(temporary);
            else File.WriteAllText(temporary, "{incomplete result");
            if (blocked)
            {
                Throws<InvalidDataException>(() => fixture.Run(_ => throw new Exception("Must not reconfirm")));
                Equal(fixture.CandidateHash, BackupHubContract.HashFile(fixture.Database));
                Equal(outcome, Read<RestoreResult>(Path.Combine(transaction, "outcome.json")));
                Directory.Delete(temporary);
            }
            fixture.Run(_ => throw new Exception("A durable decision must not reconfirm"));
            Equal(outcome, fixture.Result(request));
            Equal(fixture.CandidateHash, BackupHubContract.HashFile(fixture.Database));
            Equal(false, File.Exists(temporary));
            Equal(false, Directory.Exists(transaction));
        }
    }

    public static void PublishedRestoreNeverRollsBack(string source)
    {
        using var fixture = new Fixture(source);
        var (request, transaction, _, preId) = fixture.PrepareReplacement();
        var outcome = RestoreResult.Applied(request, fixture.PackageId, preId);
        BackupHubContract.WriteJsonDurably(Path.Combine(transaction, "outcome.json"), outcome);
        var cleanup = Path.ChangeExtension(transaction, "cleanup");
        File.WriteAllText(cleanup, "block phase promotion");
        Throws<IOException>(() => fixture.Run(_ => throw new Exception("Must not reconfirm")));
        Equal(outcome, fixture.Result(request));
        Equal(fixture.CandidateHash, BackupHubContract.HashFile(fixture.Database));
        Equal(true, File.Exists(Path.Combine(transaction, "previous.sqlite")));
        File.Delete(cleanup);
        fixture.Run(_ => throw new Exception("Must only finish delivery"));
        Equal(outcome, fixture.Result(request));
        Equal(fixture.CandidateHash, BackupHubContract.HashFile(fixture.Database));
    }

    public static void FreshRestoreCleanupFailureKeepsDecision(string source)
    {
        using var fixture = new Fixture(source);
        var request = fixture.Prepare();
        var cleanup = Path.Combine(fixture.Root, $".mockups-restore-{request:D}.cleanup");
        Func<PendingRestore, bool> confirm = _ =>
        {
            File.WriteAllText(cleanup, "block cleanup promotion after confirmation");
            return true;
        };
        // Unsupported publication platforms stop at the mandatory pre-backup.
        // Their state recovery is independently covered by portable fixtures.
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
        {
            Throws<IOException>(() => fixture.Run(confirm));
            Equal("pre-restore-backup-failed", fixture.Result(request).Error!.Code);
            return;
        }
        Throws<IOException>(() => fixture.Run(confirm));
        var applied = fixture.Result(request);
        Equal("applied", applied.State);
        Equal(fixture.CandidateHash, BackupHubContract.HashFile(fixture.Database));
        File.Delete(cleanup);
        fixture.Run(_ => throw new Exception("The applied decision must not reconfirm"));
        Equal(applied, fixture.Result(request));
        Equal(fixture.CandidateHash, BackupHubContract.HashFile(fixture.Database));
    }

    public static void PartialRollbackIsRetryable(string source)
    {
        using var fixture = new Fixture(source);
        var (request, transaction, previous, _) = fixture.PrepareReplacement();
        File.WriteAllText(Path.Combine(transaction, "recovery.sqlite"), "partial rollback copy");
        // A complete rollback is kept until result delivery succeeds.
        var blockedResult = Path.Combine(fixture.Locations.Results, $".{request:D}.tmp");
        Directory.CreateDirectory(blockedResult);
        Throws<InvalidDataException>(() => fixture.Run(_ => throw new Exception("Must not reconfirm")));
        Equal(previous, BackupHubContract.HashFile(fixture.Database));
        Equal(true, File.Exists(Path.Combine(transaction, "journal.json")));
        var decision = Read<RestoreResult>(Path.Combine(transaction, "outcome.json"));
        Equal("failed", decision.State);
        Directory.Delete(blockedResult);
        fixture.Run(_ => throw new Exception("Must finish the same rollback"));
        Equal(previous, BackupHubContract.HashFile(fixture.Database));
        Equal(decision, fixture.Result(request));
        Equal(false, Directory.Exists(transaction));
    }

    public static void CleanupPreservesLaterEdits(string source)
    {
        using var fixture = new Fixture(source);
        var request = fixture.Prepare();
        var claimed = fixture.ClaimPath(request, fixture.Locations.Processing);
        Directory.Move(fixture.ClaimPath(request, fixture.Locations.Outbox), claimed);
        var outcome = RestoreResult.Failed(request, fixture.PackageId, null, "not-presented", "confirmation-failed", "test");
        var cleanup = Path.Combine(fixture.Root, $".mockups-restore-{request:D}.cleanup");
        Directory.CreateDirectory(cleanup);
        BackupHubContract.WriteJsonDurably(Path.Combine(cleanup, "outcome.json"), outcome);
        BackupHubContract.WriteJsonDurably(fixture.Locations.ResultPath(request), outcome);
        var occupied = fixture.ClaimPath(request, fixture.Locations.Quarantine);
        Directory.CreateDirectory(occupied);
        var notifications = fixture.Run(_ => throw new Exception("Cleanup must not reconfirm"));
        Equal(true, notifications.Single().IsError);
        Equal(true, Directory.Exists(cleanup));
        fixture.WriteApplicationId(890);
        var edited = BackupHubContract.HashFile(fixture.Database);
        Directory.Delete(occupied);
        fixture.Run(_ => throw new Exception("Cleanup must not reconfirm"));
        Equal(edited, BackupHubContract.HashFile(fixture.Database));
        Equal(outcome, fixture.Result(request));
        Equal(false, Directory.Exists(cleanup));

        // Simulate interruption while deleting a cleanup directory after claim
        // finalization: its journal/decision may already be gone.
        Directory.CreateDirectory(cleanup);
        File.WriteAllText(Path.Combine(cleanup, "remaining-staging-file"), "partial cleanup");
        fixture.Run(_ => throw new Exception("Cleanup must not replay a request"));
        Equal(edited, BackupHubContract.HashFile(fixture.Database));
        Equal(false, Directory.Exists(cleanup));
    }

    public static void ConflictingResultIsRejected(string source)
    {
        using var fixture = new Fixture(source);
        var (request, transaction, _, preId) = fixture.PrepareReplacement();
        var outcome = RestoreResult.Applied(request, fixture.PackageId, preId);
        BackupHubContract.WriteJsonDurably(Path.Combine(transaction, "outcome.json"), outcome);
        var conflicting = RestoreResult.Cancelled(request, fixture.PackageId);
        BackupHubContract.WriteJsonDurably(fixture.Locations.ResultPath(request), conflicting);
        Throws<InvalidDataException>(() => fixture.Run(_ => throw new Exception("Must not reconfirm")));
        Equal(fixture.CandidateHash, BackupHubContract.HashFile(fixture.Database));
        Equal(outcome, Read<RestoreResult>(Path.Combine(transaction, "outcome.json")));
        Equal(conflicting, fixture.Result(request));
    }

    public static void RestoreRunsOnWorker(string source)
    {
        using var fixture = new Fixture(source);
        fixture.Prepare();
        var caller = Environment.CurrentManagedThreadId;
        var callbackThread = caller;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var pending = new BackupHubRestoreService(fixture.Database, fixture.Backups).ProcessPendingAsync(_ =>
        {
            callbackThread = Environment.CurrentManagedThreadId;
            return Task.FromResult(false);
        });
        Equal(true, timer.Elapsed < TimeSpan.FromSeconds(1));
        pending.GetAwaiter().GetResult();
        Equal(false, caller == callbackThread);
    }

    public static void DecisionStagingIsRecoverable(string source)
    {
        foreach (var durable in new[] { false, true })
        {
            using var fixture = new Fixture(source);
            var request = fixture.Prepare();
            var previous = BackupHubContract.HashFile(fixture.Database);
            Directory.Move(fixture.ClaimPath(request, fixture.Locations.Outbox), fixture.ClaimPath(request, fixture.Locations.Processing));
            var staging = Path.Combine(fixture.Root, $".mockups-restore-{request:D}.decision");
            Directory.CreateDirectory(staging);
            var decision = RestoreResult.Cancelled(request, fixture.PackageId);
            if (durable) BackupHubContract.WriteJsonDurably(Path.Combine(staging, "outcome.json"), decision);
            else File.WriteAllText(Path.Combine(staging, "outcome.tmp"), "partial decision");
            var confirms = 0;
            fixture.Run(_ => { confirms++; return false; });
            Equal(durable ? 0 : 1, confirms);
            Equal(previous, BackupHubContract.HashFile(fixture.Database));
            Equal("cancelled", fixture.Result(request).State);
            if (durable) Equal(decision, fixture.Result(request));
            Equal(false, Directory.Exists(staging));
        }
    }

    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), BackupHubContract.JsonOptions)!;
    private static void Write<T>(string path, T value) => File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(value, BackupHubContract.JsonOptions));
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected '{expected}', got '{actual}'.");
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"mockups-backup-integration-{Guid.NewGuid():N}");
        public string Database => Path.Combine(Root, "live.sqlite");
        public string VaultPath => Path.Combine(Root, "vault");
        public string Inbox => Path.Combine(VaultPath, "inbox");
        public BackupHubBackupService Backups { get; }
        public RestoreLocations Locations { get; }
        public Guid PackageId { get; } = Guid.NewGuid();
        public string CandidateHash { get; private set; } = "";

        public Fixture(string source)
        {
            Directory.CreateDirectory(Inbox);
            File.Copy(source, Database);
            BackupHubContract.WriteJsonDurably(Path.Combine(VaultPath, "vault-layout.json"), new { layoutVersion = 1, vaultId = BackupHubVaultLocation.VaultIdentifier });
            Backups = new BackupHubBackupService(Database, new BackupHubVaultLocation(VaultPath));
            Locations = new RestoreLocations(VaultPath);
        }

        public Guid Prepare()
        {
            Locations.PrepareOwnership();
            var id = Guid.NewGuid();
            var package = Path.Combine(ClaimPath(id, Locations.Outbox), "package");
            Directory.CreateDirectory(Path.Combine(package, "payload"));
            var candidate = CandidatePath(id);
            var snapshot = SqliteDatabaseSnapshotService.CreateValidated(Database, candidate);
            CandidateHash = BackupHubContract.HashFile(candidate);
            // Portable external handoff fixture; publication itself is tested separately.
            var manifest = new BackupManifest(1, PackageId.ToString("D"), "mockups", BackupHubContract.TimestampNow(), "manual",
                new BackupProducer("integration-fixture", "macos"), new BackupSnapshot("mockups-production", snapshot.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                [new BackupFile("mockups.sqlite", new FileInfo(candidate).Length, CandidateHash)]);
            var manifestPath = Path.Combine(package, "manifest.json");
            BackupHubContract.WriteJsonDurably(manifestPath, manifest);
            _ = BackupPackageValidator.Validate(package, PackageId);
            BackupHubContract.WriteJsonDurably(Path.Combine(ClaimPath(id, Locations.Outbox), "request.json"), new RestoreRequest(
                2, id.ToString("D"), "mockups", PackageId.ToString("D"), BackupHubContract.TimestampNow(), new string('a', 64), BackupHubContract.HashFile(manifestPath),
                new RestoreBackupSummary(manifest.CreatedAt, manifest.Reason, manifest.Snapshot.Format, manifest.Snapshot.SchemaVersion, 1, manifest.Files[0].ByteLength), "prepared"));
            WriteApplicationId(123);
            return id;
        }

        public void WriteApplicationId(int value)
        {
            using var database = new SqliteConnection($"Data Source={Database};Pooling=False");
            database.Open();
            using var command = database.CreateCommand();
            command.CommandText = $"PRAGMA application_id = {value}";
            command.ExecuteNonQuery();
        }

        public (Guid Request, string Transaction, string Previous, Guid PreId) PrepareReplacement()
        {
            var request = Prepare();
            var previous = BackupHubContract.HashFile(Database);
            var preId = Guid.NewGuid();
            var transaction = Path.Combine(Root, $".mockups-restore-{request:D}.txn");
            Directory.CreateDirectory(transaction);
            File.Copy(Database, Path.Combine(transaction, "previous.sqlite"));
            File.Copy(CandidatePath(request), Database, overwrite: true);
            BackupHubContract.WriteJsonDurably(Path.Combine(transaction, "journal.json"), new RestoreJournal(
                request.ToString("D"), PackageId.ToString("D"), preId.ToString("D"), previous, CandidateHash));
            Directory.Move(ClaimPath(request, Locations.Outbox), ClaimPath(request, Locations.Processing));
            return (request, transaction, previous, preId);
        }

        public string ClaimPath(Guid id, string parent) => Path.Combine(parent, $"{id:D}.bhrestore");
        public string CandidatePath(Guid id) => Path.Combine(ClaimPath(id, Locations.Outbox), "package", "payload", "mockups.sqlite");
        public IReadOnlyList<RestoreNotification> Run(Func<PendingRestore, bool> confirm) =>
            new BackupHubRestoreService(Database, Backups).ProcessPendingAsync(pending => Task.FromResult(confirm(pending))).GetAwaiter().GetResult();
        public RestoreResult Result(Guid request)
        {
            var result = Read<RestoreResult>(Locations.ResultPath(request));
            result.Validate();
            return result;
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
