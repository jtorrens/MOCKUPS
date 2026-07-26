using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading;

namespace Mockups.DesktopEditorShell;

internal sealed class HostedApp : App
{
    protected override void ConfigureDesktopLifetime(
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        var databasePath = DesktopEditorLaunchOptions.DatabasePath
            ?? DesktopHost.DefaultDatabasePath();
        var startup = new StartupStatusWindow();
        var cancellation = new CancellationTokenSource();
        var completed = false;
        startup.Closing += (_, _) =>
        {
            if (!completed)
            {
                cancellation.Cancel();
            }
        };
        startup.Opened += async (_, _) =>
        {
            var coordinator = new ApplicationStartupCoordinator(
                System.IO.Path.Combine(
                    System.AppContext.BaseDirectory,
                    "desktop-preview"));
            var result = await coordinator.StartAsync(
                databasePath,
                cancellation.Token);
            if (result is StartupResult.Canceled)
            {
                desktop.Shutdown();
                return;
            }

            Window next = result switch
            {
                StartupResult.Success success =>
                    success.Session.CreateWindow(),
                _ => new StartupRecoveryWindow(result),
            };
            completed = true;
            desktop.MainWindow = next;
            next.Show();
            startup.Close();
            cancellation.Dispose();
        };
        desktop.MainWindow = startup;
    }
}

internal sealed class VisualInstanceConflictApp : App
{
    protected override void ConfigureDesktopLifetime(
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow =
            new VisualInstanceConflictWindow();
    }
}
