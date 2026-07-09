using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static readonly object WriteGate = new();
    private readonly string _connectionString;

    public SpikeDatabase(string databasePath)
    {
        databasePath = Path.GetFullPath(databasePath);
        ProjectPathService.ConfigureProjectRoot(ProjectRootForDatabase(databasePath));
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();

        Initialize();
    }

    public static string DefaultDatabasePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "package.json"))
                && Directory.Exists(Path.Combine(directory.FullName, "assets")))
            {
                return Path.Combine(directory.FullName, "data", "desktop-editor-spike.sqlite");
            }

            directory = directory.Parent;
        }

        var root = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, "..", "..", "..", "..", "..", "data", "desktop-editor-spike.sqlite"));
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        ExecuteScript(connection, SchemaSql);
        EnsureProjectColumns(connection);
        EnsureEpisodeColumns(connection);
        EnsureShotColumns(connection);
        EnsureAppColumns(connection);
        EnsureModuleColumns(connection);
        EnsureComponentClassColumns(connection);
        SeedEditorLayouts(connection);
        SeedIfEmpty(connection);
        SeedPaletteColorsIfEmpty(connection);
        SeedDevicesIfEmpty(connection);
        EnsureDeviceMetricDefaults(connection);
        SeedActorsIfEmpty(connection);
        SeedProductionFontsIfEmpty(connection);
        SeedRenderPresetsIfEmpty(connection);
        MigrateVideoComponentClassesToMedia(connection);
        SeedComponentClassesIfEmpty(connection);
        EnsureComponentClassConfigDefaults(connection);
        EnsureComponentClassRecordClassIds(connection);
        SeedThemesIfEmpty(connection);
        EnsureThemeComponentPresetReferences(connection);
        ClearShotRenderPresetReferences(connection);
        EnsureThemeTokens(connection);
    }

    private static string ProjectRootForDatabase(string databasePath)
    {
        var databaseDirectory = Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory;
        var directoryName = Path.GetFileName(databaseDirectory);
        if (string.Equals(directoryName, "data", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(databaseDirectory)?.FullName ?? databaseDirectory;
        }

        return databaseDirectory;
    }
}
