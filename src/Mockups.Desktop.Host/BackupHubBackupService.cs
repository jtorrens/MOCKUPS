using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell;

internal enum BackupReason
{
    CleanExit,
    Manual,
    PreMigration,
    PreRestore,
}

internal sealed record BackupPublication(
    Guid PackageId,
    string PackagePath,
    string DatabaseSha256);

internal sealed class BackupHubBackupService
{
    internal const string ApplicationId = "mockups";
    internal const string SnapshotFormat = "mockups-production";
    internal const string PayloadPath = "mockups.sqlite";

    private readonly string _databasePath;

    public BackupHubBackupService(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
    }

    public string CaptureDatabaseFingerprint()
    {
        var temporary = Path.Combine(
            Path.GetTempPath(),
            $"mockups-backup-fingerprint-{Guid.NewGuid():D}.sqlite");
        try
        {
            _ = SqliteDatabaseSnapshotService.CreateValidated(
                _databasePath,
                temporary);
            return BackupHubContract.HashFile(temporary);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public BackupPublication? Publish(
        BackupReason reason,
        string? unchangedDatabaseSha256 = null)
    {
        var inbox = BackupHubVaultLocation.RequireInbox();
        var packageId = Guid.NewGuid();
        var identity = BackupHubContract.Canonical(packageId);
        var staging = Path.Combine(inbox, $".{identity}.tmp");
        var published = Path.Combine(inbox, $"{identity}.bhpkg");
        if (Directory.Exists(staging)
            || Directory.Exists(published)
            || File.Exists(staging)
            || File.Exists(published))
        {
            throw new IOException(
                $"Backup package identity already exists: {identity}");
        }

        try
        {
            var payload = Path.Combine(staging, "payload");
            Directory.CreateDirectory(payload);
            var snapshotPath = Path.Combine(payload, PayloadPath);
            var snapshot =
                SqliteDatabaseSnapshotService.CreateValidated(
                    _databasePath,
                    snapshotPath);
            var databaseHash = BackupHubContract.HashFile(
                snapshotPath);
            if (reason == BackupReason.CleanExit
                && string.Equals(
                    databaseHash,
                    unchangedDatabaseSha256,
                    StringComparison.Ordinal))
            {
                Directory.Delete(staging, recursive: true);
                return null;
            }

            var manifest = new BackupManifest(
                ContractVersion: 1,
                PackageId: identity,
                ApplicationId: ApplicationId,
                CreatedAt: BackupHubContract.TimestampNow(),
                Reason: BackupHubContract.Reason(reason),
                Producer: new BackupProducer(
                    EditorBuildIdentity.Commit,
                    BackupHubContract.Platform),
                Snapshot: new BackupSnapshot(
                    SnapshotFormat,
                    snapshot.SchemaVersion.ToString(
                        CultureInfo.InvariantCulture)),
                Files:
                [
                    new BackupFile(
                        PayloadPath,
                        new FileInfo(snapshotPath).Length,
                        databaseHash),
                ]);
            var manifestPath = Path.Combine(
                staging,
                "manifest.json");
            BackupHubContract.WriteJsonDurably(
                manifestPath,
                manifest);
            BackupHubContract.FlushDirectory(payload);
            BackupHubContract.FlushDirectory(staging);
            _ = BackupPackageValidator.Validate(
                staging,
                packageId);
            Directory.Move(staging, published);
            BackupHubContract.FlushDirectory(inbox);
            return new BackupPublication(
                packageId,
                published,
                databaseHash);
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
}

internal static class BackupHubVaultLocation
{
    internal const string VaultIdentifier =
        "com.jtorrens.backup-hub";

    public static string RequireVault()
    {
        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData))
        {
            throw new InvalidOperationException(
                "The operating system did not provide its application-data directory.");
        }
        var vault = Path.Combine(
            applicationData,
            VaultIdentifier,
            "vault");
        BackupHubContract.RequireRegularDirectory(
            vault,
            "Backup Hub vault");
        var marker = Path.Combine(
            vault,
            "vault-layout.json");
        BackupHubContract.RequireRegularFile(
            marker,
            "Backup Hub vault-layout.json");
        using var document = JsonDocument.Parse(
            File.ReadAllBytes(marker));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || root.EnumerateObject().Count() != 2
            || !root.TryGetProperty(
                "layoutVersion",
                out var layoutVersion)
            || layoutVersion.ValueKind != JsonValueKind.Number
            || !layoutVersion.TryGetInt32(out var version)
            || version != 1
            || !root.TryGetProperty(
                "vaultId",
                out var vaultId)
            || vaultId.ValueKind != JsonValueKind.String
            || !string.Equals(
                vaultId.GetString(),
                VaultIdentifier,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Backup Hub vault-layout.json does not satisfy Vault Location v1.");
        }
        return vault;
    }

    public static string RequireInbox()
    {
        var inbox = Path.Combine(
            RequireVault(),
            "inbox");
        BackupHubContract.RequireRegularDirectory(
            inbox,
            "Backup Hub inbox");
        return inbox;
    }
}

internal static class BackupPackageValidator
{
    public static BackupManifest Validate(
        string packagePath,
        Guid expectedPackageId)
    {
        BackupHubContract.RequireRegularDirectory(
            packagePath,
            "backup package");
        var packageEntries = Directory
            .EnumerateFileSystemEntries(packagePath)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!packageEntries.SequenceEqual(
                ["manifest.json", "payload"],
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Backup Package v1 must contain only manifest.json and payload.");
        }

        var manifestPath = Path.Combine(
            packagePath,
            "manifest.json");
        BackupHubContract.RequireRegularFile(
            manifestPath,
            "manifest.json");
        var manifestBytes = File.ReadAllBytes(
            manifestPath);
        BackupHubContract.RequireExactJsonShape(
            manifestBytes,
            [
                "contractVersion", "packageId", "applicationId", "createdAt",
                "reason", "producer", "snapshot", "files",
            ],
            ("producer", ["version", "platform"]),
            ("snapshot", ["format", "schemaVersion"]));
        var manifest = JsonSerializer.Deserialize<BackupManifest>(
                manifestBytes,
                BackupHubContract.JsonOptions)
            ?? throw new InvalidDataException(
                "manifest.json is empty.");

        var expectedId = BackupHubContract.Canonical(
            expectedPackageId);
        var packageName = Path.GetFileName(packagePath);
        if (manifest.ContractVersion != 1
            || manifest.PackageId != expectedId
            || (packageName != $".{expectedId}.tmp"
                && packageName != $"{expectedId}.bhpkg"
                && packageName != "package")
            || manifest.ApplicationId
                != BackupHubBackupService.ApplicationId
            || manifest.Snapshot.Format
                != BackupHubBackupService.SnapshotFormat
            || string.IsNullOrWhiteSpace(
                manifest.Snapshot.SchemaVersion)
            || !DateTimeOffset.TryParse(
                manifest.CreatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _)
            || !BackupHubContract.ValidReason(
                manifest.Reason)
            || string.IsNullOrWhiteSpace(
                manifest.Producer.Version)
            || manifest.Producer.Platform
                is not ("macos" or "windows")
            || manifest.Files.Length != 1)
        {
            throw new InvalidDataException(
                "manifest.json does not satisfy the MOCKUPS Backup Package v1 contract.");
        }

        var payload = Path.Combine(
            packagePath,
            "payload");
        BackupHubContract.RequireRegularDirectory(
            payload,
            "payload");
        var payloadEntries = Directory
            .EnumerateFileSystemEntries(
                payload,
                "*",
                SearchOption.AllDirectories)
            .ToArray();
        if (payloadEntries.Length != 1)
        {
            throw new InvalidDataException(
                "The MOCKUPS payload must contain exactly one file.");
        }
        var file = manifest.Files[0];
        if (file.Path != BackupHubBackupService.PayloadPath
            || !BackupHubContract.IsLowercaseSha256(
                file.Sha256)
            || file.ByteLength < 0)
        {
            throw new InvalidDataException(
                "The MOCKUPS payload declaration is invalid.");
        }
        var snapshotPath = Path.Combine(
            payload,
            BackupHubBackupService.PayloadPath);
        BackupHubContract.RequireRegularFile(
            snapshotPath,
            BackupHubBackupService.PayloadPath);
        var snapshotInfo = new FileInfo(snapshotPath);
        if (snapshotInfo.Length != file.ByteLength)
        {
            throw new InvalidDataException(
                "The MOCKUPS payload byte length does not match its manifest.");
        }
        if (!string.Equals(
                BackupHubContract.HashFile(snapshotPath),
                file.Sha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The MOCKUPS payload hash does not match its manifest.");
        }
        var snapshot = SqliteDatabaseSnapshotService.Validate(
            snapshotPath);
        if (!string.Equals(
                snapshot.SchemaVersion.ToString(
                    CultureInfo.InvariantCulture),
                manifest.Snapshot.SchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The declared snapshot schema does not match the SQLite database.");
        }
        return manifest;
    }
}

internal static class BackupHubContract
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public static string Platform =>
        OperatingSystem.IsMacOS()
            ? "macos"
            : OperatingSystem.IsWindows()
                ? "windows"
                : throw new PlatformNotSupportedException(
                    "Backup Hub supports MOCKUPS backups on macOS and Windows.");

    public static string Canonical(Guid value) =>
        value.ToString("D", CultureInfo.InvariantCulture)
            .ToLowerInvariant();

    public static string TimestampNow() =>
        DateTimeOffset.UtcNow.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture);

    public static string Reason(BackupReason reason) =>
        reason switch
        {
            BackupReason.CleanExit => "clean-exit",
            BackupReason.Manual => "manual",
            BackupReason.PreMigration => "pre-migration",
            BackupReason.PreRestore => "pre-restore",
            _ => throw new ArgumentOutOfRangeException(
                nameof(reason)),
        };

    public static bool ValidReason(string reason) =>
        reason is "clean-exit"
            or "manual"
            or "pre-migration"
            or "pre-restore";

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(
                SHA256.HashData(stream))
            .ToLowerInvariant();
    }

    public static string HashBytes(byte[] bytes) =>
        Convert.ToHexString(
                SHA256.HashData(bytes))
            .ToLowerInvariant();

    public static bool IsLowercaseSha256(string value) =>
        value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
                or >= 'a' and <= 'f');

    public static void WriteJsonDurably<T>(
        string path,
        T value)
    {
        WriteBytesDurably(
            path,
            JsonSerializer.SerializeToUtf8Bytes(
                value,
                JsonOptions));
    }

    public static void WriteBytesDurably(
        string path,
        byte[] bytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    public static void RequireRegularFile(
        string path,
        string label)
    {
        var info = new FileInfo(path);
        if (!info.Exists
            || info.LinkTarget is not null
            || info.Attributes.HasFlag(
                FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException(
                $"{label} must be a regular, non-symbolic file: {path}");
        }
    }

    public static void RequireRegularDirectory(
        string path,
        string label)
    {
        var info = new DirectoryInfo(path);
        if (!info.Exists
            || info.LinkTarget is not null
            || info.Attributes.HasFlag(
                FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException(
                $"{label} must be a regular, non-symbolic directory: {path}");
        }
    }

    public static void RequireExactJsonShape(
        byte[] bytes,
        string[] rootProperties,
        params (string Property, string[] Children)[]
            childObjects)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(
                    rootProperties.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "JSON document does not have its exact required root shape.");
        }
        foreach (var (property, children) in childObjects)
        {
            if (!root.TryGetProperty(property, out var child)
                || child.ValueKind != JsonValueKind.Object
                || !child.EnumerateObject()
                    .Select(value => value.Name)
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(
                        children.Order(StringComparer.Ordinal),
                        StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"JSON property '{property}' does not have its exact required shape.");
            }
        }
    }

    public static void FlushDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        UnixDirectorySync.Flush(path);
    }
}

internal static class UnixDirectorySync
{
    private const int ReadOnly = 0;

    public static void Flush(string path)
    {
        var descriptor = Open(path, ReadOnly);
        if (descriptor < 0)
        {
            throw new IOException(
                $"Could not open directory for durable synchronization: {path}");
        }
        try
        {
            if (Fsync(descriptor) != 0)
            {
                throw new IOException(
                    $"Could not durably synchronize directory: {path}");
            }
        }
        finally
        {
            _ = Close(descriptor);
        }
    }

    [System.Runtime.InteropServices.DllImport(
        "libc",
        EntryPoint = "open",
        CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
    private static extern int Open(
        string path,
        int flags);

    [System.Runtime.InteropServices.DllImport(
        "libc",
        EntryPoint = "fsync")]
    private static extern int Fsync(int descriptor);

    [System.Runtime.InteropServices.DllImport(
        "libc",
        EntryPoint = "close")]
    private static extern int Close(int descriptor);
}

internal sealed record BackupManifest(
    int ContractVersion,
    string PackageId,
    string ApplicationId,
    string CreatedAt,
    string Reason,
    BackupProducer Producer,
    BackupSnapshot Snapshot,
    BackupFile[] Files);

internal sealed record BackupProducer(
    string Version,
    string Platform);

internal sealed record BackupSnapshot(
    string Format,
    string SchemaVersion);

internal sealed record BackupFile(
    string Path,
    long ByteLength,
    string Sha256);
