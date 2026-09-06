using Mockups.DesktopEditorShell.Data;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell;

internal static class BackupHubMaintenanceCommand
{
    public static bool TryRun(string[] args)
    {
        var commandIndex = Array.FindIndex(
            args,
            value => string.Equals(
                value,
                "--publish-backup",
                StringComparison.Ordinal));
        if (commandIndex < 0)
        {
            return false;
        }
        if (commandIndex + 1 >= args.Length
            || !string.Equals(
                args[commandIndex + 1],
                "pre-migration",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Maintenance backup publication supports only --publish-backup pre-migration.");
        }
        var source = OptionValue(args, "--source")
            ?? SqlitePersistence.DefaultDatabasePath();
        var databasePath = Path.GetFullPath(source);
        var maintenance = WorkstationUpdateMaintenance
            .LockFilePath(databasePath);
        if (!File.Exists(maintenance))
        {
            throw new InvalidOperationException(
                "A pre-migration backup requires active MOCKUPS workstation maintenance.");
        }
        var publication = new BackupHubBackupService(
                databasePath)
            .Publish(BackupReason.PreMigration)
            ?? throw new InvalidOperationException(
                "A pre-migration backup cannot be deduplicated.");
        Console.WriteLine(
            $"Pre-migration backup delivered to Backup Hub: {publication.PackagePath}");
        return true;
    }

    private static string? OptionValue(
        string[] args,
        string option)
    {
        var index = Array.FindIndex(
            args,
            value => string.Equals(
                value,
                option,
                StringComparison.Ordinal));
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }
}
