using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
            var backups = new BackupHubBackupService(
                databasePath);
            IReadOnlyList<RestoreNotification> notifications;
            try
            {
                notifications = await new BackupHubRestoreService(
                        databasePath,
                        backups)
                    .ProcessPendingAsync(
                        pending =>
                            StartupRestoreDialogs.ConfirmAsync(
                                startup,
                                pending));
            }
            catch (Exception exception)
            {
                CompleteStartup(
                    new StartupResult.RecoveryRequired(
                        $"Backup Hub restore startup failed: {exception.Message}"));
                return;
            }

            var coordinator = new ApplicationStartupCoordinator(
                System.IO.Path.Combine(
                    System.AppContext.BaseDirectory,
                    "desktop-preview"),
                () => new BackupHubApplicationLifecycle(
                    backups,
                    backups.CaptureDatabaseFingerprint()));
            var result = await coordinator.StartAsync(
                databasePath,
                cancellation.Token);
            if (result is StartupResult.Canceled)
            {
                desktop.Shutdown();
                return;
            }

            foreach (var notification in notifications)
            {
                await StartupRestoreDialogs.ShowNotificationAsync(
                    startup,
                    notification);
            }

            CompleteStartup(result);

            void CompleteStartup(StartupResult startupResult)
            {
                Window next = startupResult switch
                {
                    StartupResult.Success success =>
                        success.Session.CreateWindow(),
                    _ => new StartupRecoveryWindow(
                        startupResult),
                };
                completed = true;
                desktop.MainWindow = next;
                next.Show();
                startup.Close();
                cancellation.Dispose();
            }
        };
        desktop.MainWindow = startup;
    }
}
