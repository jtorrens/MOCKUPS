using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static IReadOnlyList<string> GetReferenceUsages(
        SqliteConnection connection,
        ProjectTreeNodeKind kind,
        string nodeId,
        string searchValue)
    {
        if (string.IsNullOrWhiteSpace(searchValue)) return [];

        var ownTable = TableNameForKind(kind);
        var usages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in ReferenceSearchTables(connection))
        {
            if (!HasColumn(connection, table, "id")) continue;

            var textColumns = TextColumns(connection, table);
            foreach (var column in textColumns)
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT "id"
                    FROM {QuoteIdentifier(table)}
                    WHERE {QuoteIdentifier(column)} LIKE $needle
                      AND ($ownTable <> $table OR "id" <> $nodeId)
                    LIMIT 25
                    """;
                command.Parameters.AddWithValue("$needle", $"%{searchValue}%");
                command.Parameters.AddWithValue("$ownTable", ownTable);
                command.Parameters.AddWithValue("$table", table);
                command.Parameters.AddWithValue("$nodeId", nodeId);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var rowId = reader.GetString(0);
                    usages.Add($"{TableDisplayName(table)}: {RowDisplayName(connection, table, rowId)} ({column})");
                }
            }
        }

        return usages.ToList();
    }

    private static Dictionary<string, List<string>> BuildReferenceUsageIndex(
        IReadOnlyList<ShotRow> shots,
        IReadOnlyList<ActorRow> actors,
        IReadOnlyList<ThemeRow> themes,
        IReadOnlyList<PaletteColorRow> paletteColors,
        IReadOnlyList<ProductionFontRow> productionFonts,
        IReadOnlyList<IconThemeRow> iconThemes,
        IReadOnlyList<StatusBarRow> statusBars,
        IReadOnlyList<NavigationBarRow> navigationBars,
        IReadOnlyList<RenderPresetRow> renderPresets,
        IReadOnlyList<ComponentClassRow> componentClasses)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var actor in actors)
        {
            AddUsage(index, ProjectTreeNodeKind.Device, actor.DefaultDeviceId, $"Actor: {actor.DisplayName}");
            AddUsage(index, ProjectTreeNodeKind.Theme, actor.DefaultThemeId, $"Actor: {actor.DisplayName}");
        }

        foreach (var shot in shots)
        {
            AddUsage(index, ProjectTreeNodeKind.Actor, shot.OwnerActorId, $"Shot: {shot.Name}");
        }

        foreach (var theme in themes)
        {
            AddUsage(index, ProjectTreeNodeKind.IconTheme, theme.IconThemeId, $"Theme: {theme.Name}");
            AddUsage(index, ProjectTreeNodeKind.StatusBar, theme.StatusBarId, $"Theme: {theme.Name}");
            AddUsage(index, ProjectTreeNodeKind.NavigationBar, theme.NavigationBarId, $"Theme: {theme.Name}");

            foreach (var font in productionFonts)
            {
                AddUsageIfContains(index, ProjectTreeNodeKind.ProductionFont, font.Id, theme.TokensJson, $"Theme: {theme.Name}");
            }

            foreach (var color in paletteColors)
            {
                AddUsageIfContains(index, ProjectTreeNodeKind.PaletteColor, color.Id, theme.TokensJson, $"Theme: {theme.Name}", color.Token);
            }
        }

        foreach (var componentClass in componentClasses)
        {
            var componentText = string.Join(
                "\n",
                componentClass.ConfigJson,
                componentClass.DesignPreviewJson,
                componentClass.MetadataJson);

            foreach (var color in paletteColors)
            {
                AddUsageIfContains(index, ProjectTreeNodeKind.PaletteColor, color.Id, componentText, $"Component Class: {componentClass.Name}", color.Token);
            }

            foreach (var iconTheme in iconThemes)
            {
                AddUsageIfContains(index, ProjectTreeNodeKind.IconTheme, iconTheme.Id, componentText, $"Component Class: {componentClass.Name}");
            }

            foreach (var statusBar in statusBars)
            {
                AddUsageIfContains(index, ProjectTreeNodeKind.StatusBar, statusBar.Id, componentText, $"Component Class: {componentClass.Name}");
            }

            foreach (var navigationBar in navigationBars)
            {
                AddUsageIfContains(index, ProjectTreeNodeKind.NavigationBar, navigationBar.Id, componentText, $"Component Class: {componentClass.Name}");
            }

            foreach (var renderPreset in renderPresets)
            {
                AddUsageIfContains(index, ProjectTreeNodeKind.RenderPreset, renderPreset.Id, componentText, $"Component Class: {componentClass.Name}");
            }

            foreach (var font in productionFonts)
            {
                AddUsageIfContains(index, ProjectTreeNodeKind.ProductionFont, font.Id, componentText, $"Component Class: {componentClass.Name}");
            }
        }

        foreach (var presetOwner in componentClasses)
        {
            foreach (var preset in ComponentClassPresets(presetOwner.MetadataJson))
            {
                foreach (var componentClass in componentClasses.Where((candidate) => candidate.ProjectId == presetOwner.ProjectId))
                {
                    if (componentClass.Id == presetOwner.Id)
                    {
                        continue;
                    }

                    if (componentClass.ConfigJson.Contains($"\"presetId\":\"{preset.Id}\"", StringComparison.Ordinal)
                        || componentClass.ConfigJson.Contains($"\"presetId\": \"{preset.Id}\"", StringComparison.Ordinal))
                    {
                        AddUsage(
                            index,
                            ProjectTreeNodeKind.ComponentPreset,
                            ComponentPresetNodeId(presetOwner.Id, preset.Id),
                            $"Component Class: {componentClass.Name}");
                    }
                }
            }
        }

        foreach (var actor in actors)
        {
            foreach (var color in paletteColors)
            {
                AddUsageIfContains(index, ProjectTreeNodeKind.PaletteColor, color.Id, actor.MetadataJson, $"Actor: {actor.DisplayName}", color.Token);
            }
        }

        return index;
    }

    private static bool IsUsed(IReadOnlyDictionary<string, List<string>> index, ProjectTreeNodeKind kind, string id)
    {
        return index.ContainsKey(ReferenceKey(kind, id));
    }

    private static void AddUsage(
        IDictionary<string, List<string>> index,
        ProjectTreeNodeKind kind,
        string id,
        string usage)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var key = ReferenceKey(kind, id);
        if (!index.TryGetValue(key, out var usages))
        {
            usages = [];
            index[key] = usages;
        }

        if (!usages.Contains(usage, StringComparer.OrdinalIgnoreCase))
        {
            usages.Add(usage);
        }
    }

    private static void AddUsageIfContains(
        IDictionary<string, List<string>> index,
        ProjectTreeNodeKind kind,
        string id,
        string haystack,
        string usage,
        string? searchValue = null)
    {
        var needle = string.IsNullOrWhiteSpace(searchValue) ? id : searchValue;
        if (string.IsNullOrWhiteSpace(needle)) return;
        if (string.IsNullOrWhiteSpace(haystack)) return;
        if (!haystack.Contains(needle, StringComparison.Ordinal)) return;

        AddUsage(index, kind, id, usage);
    }

    private static string ReferenceKey(ProjectTreeNodeKind kind, string id)
    {
        return $"{kind}:{id}";
    }

    private static bool HasColumn(SqliteConnection connection, string table, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(table)})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ReferenceSearchValue(SqliteConnection connection, ProjectTreeNode node)
    {
        if (node.Kind != ProjectTreeNodeKind.PaletteColor)
        {
            return node.Id;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT token FROM palette_colors WHERE id = $id";
        command.Parameters.AddWithValue("$id", node.Id);
        return command.ExecuteScalar() as string ?? node.Name;
    }

    private static string TableNameForKind(ProjectTreeNodeKind kind)
    {
        return kind switch
        {
            ProjectTreeNodeKind.Episode => "episodes",
            ProjectTreeNodeKind.Shot => "shots",
            ProjectTreeNodeKind.App => "apps",
            ProjectTreeNodeKind.Module => "modules",
            ProjectTreeNodeKind.PaletteColor => "palette_colors",
            ProjectTreeNodeKind.Device => "devices",
            ProjectTreeNodeKind.Actor => "actors",
            ProjectTreeNodeKind.Theme => "themes",
            ProjectTreeNodeKind.ProductionFont => "production_fonts",
            ProjectTreeNodeKind.IconTheme => "icon_themes",
            ProjectTreeNodeKind.StatusBar => "status_bars",
            ProjectTreeNodeKind.NavigationBar => "navigation_bars",
            ProjectTreeNodeKind.RenderPreset => "render_presets",
            ProjectTreeNodeKind.ComponentClass => "component_classes",
            _ => "",
        };
    }

    private static IReadOnlyList<string> ReferenceSearchTables(SqliteConnection connection)
    {
        var tables = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static IReadOnlyList<string> TextColumns(SqliteConnection connection, string table)
    {
        var columns = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(table)})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            var type = ReadString(reader, 2);
            if (name == "id") continue;
            if (type.Contains("TEXT", StringComparison.OrdinalIgnoreCase))
            {
                columns.Add(name);
            }
        }

        return columns;
    }

    private static string RowDisplayName(SqliteConnection connection, string table, string rowId)
    {
        var labelColumn = LabelColumn(connection, table);
        if (labelColumn is null) return rowId;

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {QuoteIdentifier(labelColumn)} FROM {QuoteIdentifier(table)} WHERE id = $id";
        command.Parameters.AddWithValue("$id", rowId);
        return command.ExecuteScalar() as string ?? rowId;
    }

    private static string? LabelColumn(SqliteConnection connection, string table)
    {
        var columns = TextColumns(connection, table);
        foreach (var preferred in new[] { "name", "display_name", "family_name", "slug", "component_type", "token" })
        {
            if (columns.Contains(preferred, StringComparer.OrdinalIgnoreCase))
            {
                return preferred;
            }
        }

        return columns.FirstOrDefault();
    }

    private static string TableDisplayName(string table)
    {
        return table switch
        {
            "actors" => "Actor",
            "apps" => "App",
            "component_classes" => "Component Class",
            "devices" => "Device",
            "episodes" => "Episode",
            "icon_themes" => "Icon Theme",
            "modules" => "Module",
            "navigation_bars" => "Navigation Bar",
            "palette_colors" => "Palette Color",
            "production_fonts" => "Production Font",
            "render_presets" => "Render Preset",
            "projects" => "Project",
            "shots" => "Shot",
            "status_bars" => "Status Bar",
            "themes" => "Theme",
            _ => table,
        };
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
