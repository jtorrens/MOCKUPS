using System;
using System.IO;

namespace Mockups.DesktopEditorShell;

public static class DesktopHost
{
    public static string DefaultDatabasePath() =>
        Data.SqlitePersistence.DefaultDatabasePath();

    public static MainWindow CreateWindow(string databasePath)
    {
        var coordinator = new ApplicationStartupCoordinator(
            Path.Combine(AppContext.BaseDirectory, "desktop-preview"));
        return coordinator.Start(databasePath) switch
        {
            StartupResult.Success success =>
                success.Session.CreateWindow(),
            var failure => throw new InvalidOperationException(
                StartupResultMessage.For(failure)),
        };
    }
}
