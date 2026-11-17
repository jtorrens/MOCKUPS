using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell;

internal sealed class BackupHubApplicationLifecycle
    : IApplicationBackupLifecycle
{
    private readonly BackupHubBackupService _backups;
    private readonly object _baselineGate = new();
    private string _baselineSha256;

    public BackupHubApplicationLifecycle(
        BackupHubBackupService backups,
        string startupSnapshotSha256)
    {
        _backups = backups;
        _baselineSha256 = startupSnapshotSha256;
    }

    public async Task<ApplicationBackupResult> PublishManualAsync(
        EditorOperationCoordinator operations)
    {
        var publication = await operations.ExecuteAsync(
            () => _backups.Publish(BackupReason.Manual));
        if (publication is null)
        {
            throw new InvalidOperationException(
                "A manual backup cannot be deduplicated.");
        }
        SetBaseline(publication.SnapshotSha256);
        return new ApplicationBackupResult(
            ApplicationBackupOutcome.Published,
            System.IO.Path.GetFileName(
                publication.PackagePath));
    }

    public async Task<ApplicationBackupResult> PublishCleanExitAsync(
        EditorOperationCoordinator operations)
    {
        var publication = await operations.ExecuteShutdownAsync(
            () => _backups.Publish(
                BackupReason.CleanExit,
                GetBaseline()));
        if (publication is null)
        {
            return new ApplicationBackupResult(
                ApplicationBackupOutcome.Unchanged);
        }
        SetBaseline(publication.SnapshotSha256);
        return new ApplicationBackupResult(
            ApplicationBackupOutcome.Published,
            System.IO.Path.GetFileName(
                publication.PackagePath));
    }

    private string GetBaseline()
    {
        lock (_baselineGate)
        {
            return _baselineSha256;
        }
    }

    private void SetBaseline(string sha256)
    {
        lock (_baselineGate)
        {
            _baselineSha256 = sha256;
        }
    }
}
