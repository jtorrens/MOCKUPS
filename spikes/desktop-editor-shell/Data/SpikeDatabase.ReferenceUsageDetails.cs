using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public sealed record ReferenceUsageDetail(
        string TargetNodeId,
        ProjectTreeNodeKind TargetKind,
        string Label,
        string Field,
        bool IsProduction);

    public IReadOnlyList<ReferenceUsageDetail> GetReferenceUsageDetails(ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.ComponentPreset)
        {
            return GetComponentPresetReferenceUsageDetails(node)
                .Select((usage) => new ReferenceUsageDetail(
                    usage.TargetNodeId,
                    ReferenceKindForSource(usage.SourceKind),
                    $"{usage.SourceKind}: {usage.SourceName}",
                    usage.Detail,
                    IsProductionUsageSource(usage.SourceKind)))
                .OrderBy((usage) => usage.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        using var connection = OpenConnection();
        return GetReferenceUsageDetails(connection, node.Kind, node.Id, ReferenceSearchValue(connection, node));
    }

    private static IReadOnlyList<ReferenceUsageDetail> GetReferenceUsageDetails(
        SqliteConnection connection,
        ProjectTreeNodeKind kind,
        string nodeId,
        string searchValue)
    {
        if (string.IsNullOrWhiteSpace(searchValue)) return [];

        var ownTable = TableNameForKind(kind);
        var usages = new Dictionary<string, ReferenceUsageDetail>(StringComparer.Ordinal);
        foreach (var table in ReferenceSearchTables(connection))
        {
            if (!TryReferenceNodeKind(table, out var targetKind) || !HasColumn(connection, table, "id")) continue;

            foreach (var column in TextColumns(connection, table))
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
                    var targetId = reader.GetString(0);
                    var detail = new ReferenceUsageDetail(
                        targetId,
                        targetKind,
                        $"{TableDisplayName(table)}: {RowDisplayName(connection, table, targetId)}",
                        column,
                        targetKind is ProjectTreeNodeKind.Episode or ProjectTreeNodeKind.Shot or ProjectTreeNodeKind.ModuleInstance);
                    usages[$"{targetId}:{column}"] = detail;
                }
            }
        }

        return usages.Values
            .OrderBy((usage) => usage.IsProduction)
            .ThenBy((usage) => usage.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryReferenceNodeKind(string table, out ProjectTreeNodeKind kind)
    {
        kind = table switch
        {
            "apps" => ProjectTreeNodeKind.App,
            "actors" => ProjectTreeNodeKind.Actor,
            "component_classes" => ProjectTreeNodeKind.ComponentClass,
            "devices" => ProjectTreeNodeKind.Device,
            "episodes" => ProjectTreeNodeKind.Episode,
            "icon_themes" => ProjectTreeNodeKind.IconTheme,
            "module_instances" => ProjectTreeNodeKind.ModuleInstance,
            "modules" => ProjectTreeNodeKind.Module,
            "palette_colors" => ProjectTreeNodeKind.PaletteColor,
            "production_fonts" => ProjectTreeNodeKind.ProductionFont,
            "projects" => ProjectTreeNodeKind.Project,
            "render_presets" => ProjectTreeNodeKind.RenderPreset,
            "shots" => ProjectTreeNodeKind.Shot,
            "themes" => ProjectTreeNodeKind.Theme,
            _ => default,
        };
        return table is "apps" or "actors" or "component_classes" or "devices" or "episodes"
            or "icon_themes" or "module_instances" or "modules" or "palette_colors" or "production_fonts"
            or "projects" or "render_presets" or "shots" or "themes";
    }

    private static bool IsProductionUsageSource(string sourceKind) =>
        sourceKind.Contains("Episode", StringComparison.OrdinalIgnoreCase)
        || sourceKind.Contains("Shot", StringComparison.OrdinalIgnoreCase)
        || sourceKind.Contains("Instance", StringComparison.OrdinalIgnoreCase);

    private static ProjectTreeNodeKind ReferenceKindForSource(string sourceKind) =>
        sourceKind switch
        {
            "Component Class" or "Component Variant" => ProjectTreeNodeKind.ComponentClass,
            "Module" => ProjectTreeNodeKind.Module,
            "Theme" => ProjectTreeNodeKind.Theme,
            _ => ProjectTreeNodeKind.ComponentClass,
        };
}
