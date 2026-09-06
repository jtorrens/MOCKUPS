using Avalonia;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.SequenceEqual(["--build-identity"], StringComparer.Ordinal))
        {
            Console.WriteLine(EditorBuildIdentity.Commit);
            return;
        }

        if (BackupHubMaintenanceCommand.TryRun(args))
        {
            return;
        }

        if (CurrentDatabaseMaintenance.TryRun(args))
        {
            return;
        }

        var avaloniaArgs = DesktopEditorLaunchOptions.Configure(args);
        if (args.Contains("--svg-tint-smoke", StringComparer.Ordinal))
        {
            SvgMarkupNormalizerSmoke.Validate();
            Console.WriteLine("SVG tint normalization smoke validated.");
            return;
        }

        if (!DesktopVisualInstanceLease.TryRun(
                () => RunVisualLifetime(
                    avaloniaArgs)))
        {
            Console.Error.WriteLine(
                "MOCKUPS is already open.");
        }
    }

    private static void RunVisualLifetime(
        string[] avaloniaArgs)
    {
        var databasePath = DesktopEditorLaunchOptions.DatabasePath
            ?? DesktopHost.DefaultDatabasePath();
        using var workstationAccess =
            WorkstationUpdateMaintenance
                .TryAcquireApplicationAccess(
                    databasePath);
        if (workstationAccess is null)
        {
            Console.Error.WriteLine(
                "MOCKUPS is closed while a repository update is active.");
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(
                avaloniaArgs);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HostedApp>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

}
