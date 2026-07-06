using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static readonly object WriteGate = new();
    private readonly string _connectionString;

    public SpikeDatabase(string databasePath)
    {
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
        var root = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, "..", "..", "..", "data", "desktop-editor-spike.sqlite"));
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        ExecuteScript(connection, SchemaSql);
        EnsureProjectColumns(connection);
        EnsureEpisodeColumns(connection);
        EnsureShotColumns(connection);
        EnsureAppColumns(connection);
        EnsureComponentClassColumns(connection);
        SeedEditorLayouts(connection);
        SeedIfEmpty(connection);
        SeedPaletteColorsIfEmpty(connection);
        SeedDevicesIfEmpty(connection);
        SeedActorsIfEmpty(connection);
        SeedProductionFontsIfEmpty(connection);
        SeedRenderPresetsIfEmpty(connection);
        SeedComponentClassesIfEmpty(connection);
        EnsureComponentClassConfigDefaults(connection);
        SeedThemesIfEmpty(connection);
        EnsureThemeComponentPresetReferences(connection);
        ClearShotRenderPresetReferences(connection);
        EnsureThemeTokens(connection);
    }
}
