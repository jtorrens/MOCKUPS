using Mockups.DesktopEditorShell.Data;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell;

public static class DesktopHost
{
    public static string DefaultDatabasePath() =>
        SqlitePersistence.DefaultDatabasePath();

    public static MainWindow CreateWindow(string databasePath)
    {
        DesktopPreviewBundle.RequireCurrent(
            Path.Combine(AppContext.BaseDirectory, "desktop-preview"));
        var database = SqlitePersistence.OpenCurrent(databasePath);
        var ports = new DesktopApplicationDataPorts(
            database,
            database,
            database,
            database,
            database,
            database,
            database,
            database,
            database,
            database,
            database,
            database,
            database,
            database,
            database,
            database);
        return new MainWindow(
            DesktopApplicationServices.Create(ports));
    }
}
