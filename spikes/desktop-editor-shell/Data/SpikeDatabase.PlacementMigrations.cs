using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void NormalizeAlignmentPlacementModes(SqliteConnection connection)
    {
        foreach (var (table, columns) in new (string Table, string[] Columns)[]
        {
            ("apps", ["config_json", "metadata_json"]),
            ("modules", ["config_json", "design_preview_json", "metadata_json"]),
            ("module_instances", ["transition_json", "content_json", "behavior_json", "animation_json", "metadata_json"]),
            ("component_classes", ["config_json", "design_preview_json", "metadata_json"]),
            ("shots", ["canvas_json", "metadata_json"]),
            ("devices", ["metrics_json"]),
            ("actors", ["metadata_json"]),
            ("themes", ["tokens_json", "metadata_json"]),
            ("icon_themes", ["mapping_json", "metadata_json"]),
            ("render_presets", ["codec_json", "color_json", "quality_json", "export_json", "metadata_json"]),
            ("palette_colors", ["metadata_json"]),
        })
        {
            MigrateAlignmentPlacementTable(connection, table, columns);
        }
    }

    private static void MigrateAlignmentPlacementTable(
        SqliteConnection connection,
        string table,
        IReadOnlyList<string> columns)
    {
        using var select = connection.CreateCommand();
        select.CommandText = $"SELECT id, {string.Join(", ", columns)} FROM {table}";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string[] Values)>();
        while (reader.Read())
        {
            var values = columns.Select((_, index) => reader.GetString(index + 1)).ToArray();
            var changed = false;
            for (var index = 0; index < values.Length; index += 1)
            {
                var node = JsonNode.Parse(values[index])
                    ?? throw new System.InvalidOperationException($"Invalid JSON in {table}.{columns[index]} for '{reader.GetString(0)}'.");
                if (!MigrateAlignmentPlacementNode(node)) continue;
                values[index] = node.ToJsonString();
                changed = true;
            }
            if (changed) updates.Add((reader.GetString(0), values));
        }
        reader.Close();

        foreach (var update in updates)
        {
            var assignments = string.Join(", ", columns.Select((column, index) => $"{column} = $value{index}"));
            using var command = connection.CreateCommand();
            command.CommandText = $"UPDATE {table} SET {assignments} WHERE id = $id";
            command.Parameters.AddWithValue("$id", update.Id);
            for (var index = 0; index < update.Values.Length; index += 1)
            {
                command.Parameters.AddWithValue($"$value{index}", update.Values[index]);
            }
            command.ExecuteNonQuery();
        }
    }

    private static bool MigrateAlignmentPlacementNode(JsonNode node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                changed |= MigrateLabelSubtextPlacement(obj);
                if (obj["mode"] is JsonValue modeValue
                    && modeValue.TryGetValue<string>(out var mode)
                    && mode == "edge"
                    && obj["alignX"] is not null
                    && obj["alignY"] is not null
                    && obj["offsetX"] is not null
                    && obj["offsetY"] is not null)
                {
                    obj["mode"] = "outsideEdge";
                    changed = true;
                }
                foreach (var child in obj.Select((entry) => entry.Value).Where((child) => child is not null).ToList())
                {
                    changed |= MigrateAlignmentPlacementNode(child!);
                }
                break;
            case JsonArray array:
                foreach (var child in array.Where((child) => child is not null))
                {
                    changed |= MigrateAlignmentPlacementNode(child!);
                }
                break;
        }
        return changed;
    }

    private static bool MigrateLabelSubtextPlacement(JsonObject label)
    {
        if (label["subtextPlacement"] is not JsonObject placement) return false;
        if (placement["alignY"] is not JsonValue alignYValue
            || !alignYValue.TryGetValue<double>(out var alignY))
        {
            throw new System.InvalidOperationException(
                "Label subtextPlacement must contain a numeric alignY before migration.");
        }
        label["subtextVerticalPosition"] = alignY < 0.5 ? "top" : "bottom";
        label["subtextHorizontalAlign"] = "center";
        label.Remove("subtextPlacement");
        return true;
    }
}
