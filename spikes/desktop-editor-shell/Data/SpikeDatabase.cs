using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static readonly object WriteGate = new();
    private readonly string _databasePath;
    private readonly string _connectionString;

    public SpikeDatabase(string databasePath)
    {
        databasePath = Path.GetFullPath(databasePath);
        _databasePath = databasePath;
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
        if (HasUserTables(connection))
        {
            ValidateSchemaV1(connection);
            EnsureThemeTokens(connection);
            EnsureButtonComponentClasses(connection);
            NormalizeIconRowButtonCollections(connection);
            RetireButtonIconComponentClasses(connection);
            NormalizeRadiusTokenVocabulary(connection);
            NormalizeModuleAppearanceModes(connection);
            NormalizeKeyboardConfiguration(connection);
            NormalizeRuntimeInputContracts(connection);
            NormalizeConversationModuleInstanceBehavior(connection);
            NormalizeModuleInstanceRuntimePayloads(connection);
            SynchronizeTimelineDurations(connection);
            NormalizeConversationHeaderComposition(connection);
            NormalizeCalculatedLabelComposition(connection);
            NormalizeComponentSpacingTokens(connection);
            NormalizeBubbleStatusGapTokens(connection);
            NormalizeEditorLayouts(connection);
            NormalizeDefaultComponentConfigAuthority(connection);
            return;
        }

        ExecuteScript(connection, SchemaSql);
        SeedEditorLayouts(connection);
        SeedIfEmpty(connection);
        SeedModuleInstancesIfEmpty(connection);
        SeedPaletteColorsIfEmpty(connection);
        SeedDevicesIfEmpty(connection);
        SeedActorsIfEmpty(connection);
        SeedProductionFontsIfEmpty(connection);
        SeedRenderPresetsIfEmpty(connection);
        SeedComponentClassesIfEmpty(connection);
        EnsureButtonComponentClasses(connection);
        NormalizeIconRowButtonCollections(connection);
        RetireButtonIconComponentClasses(connection);
        SeedThemesIfEmpty(connection);
        EnsureThemeTokens(connection);
        NormalizeRadiusTokenVocabulary(connection);
        NormalizeModuleAppearanceModes(connection);
        NormalizeKeyboardConfiguration(connection);
        NormalizeRuntimeInputContracts(connection);
        NormalizeConversationModuleInstanceBehavior(connection);
        NormalizeModuleInstanceRuntimePayloads(connection);
        SynchronizeTimelineDurations(connection);
        NormalizeConversationHeaderComposition(connection);
        NormalizeCalculatedLabelComposition(connection);
        NormalizeComponentSpacingTokens(connection);
        NormalizeBubbleStatusGapTokens(connection);
        NormalizeEditorLayouts(connection);
        NormalizeDefaultComponentConfigAuthority(connection);
        ValidateSchemaV1(connection);
    }

    private static bool HasUserTables(SqliteConnection connection)
    {
        return ScalarLong(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'") > 0;
    }

    private void ValidateSchemaV1(SqliteConnection connection)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "projects", "episodes", "shots", "apps", "modules", "module_instances",
            "palette_colors", "devices", "actors", "production_fonts", "icon_themes",
            "render_presets", "component_classes", "themes", "editor_layouts",
        };
        var actual = new HashSet<string>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            using var reader = command.ExecuteReader();
            while (reader.Read()) actual.Add(reader.GetString(0));
        }

        if (!actual.SetEquals(expected) || ScalarLong(connection, "PRAGMA user_version") != 1)
        {
            throw new InvalidOperationException($"Desktop database '{_databasePath}' is not schema v1. Use the committed schema v1 database instead of running historical migrations.");
        }

        if (!HasColumn(connection, "shots", "fps_override") || HasColumn(connection, "shots", "fps"))
        {
            throw new InvalidOperationException("Schema v1 shots must use fps_override and must not contain the historical fps column.");
        }
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
