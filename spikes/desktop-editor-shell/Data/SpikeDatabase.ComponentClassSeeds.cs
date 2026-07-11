using Microsoft.Data.Sqlite;
using System.Text.Json.Nodes;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void SeedComponentClassesIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            foreach (var seed in ComponentSeedRows)
            {
                if (ScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM component_classes WHERE project_id = $projectId AND component_type = $componentType",
                        ("$projectId", projectId),
                        ("$componentType", seed.ComponentType)) > 0)
                {
                    continue;
                }

                Execute(
                    connection,
                    """
                    INSERT INTO component_classes (id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json)
                    VALUES ($id, $projectId, $componentType, $recordClassId, $name, $notes, $configJson, $designPreviewJson, $metadataJson)
                    """,
                    ("$id", $"component_{projectId}_{seed.ComponentType}"),
                    ("$projectId", projectId),
                    ("$componentType", seed.ComponentType),
                    ("$recordClassId", seed.RecordClassId),
                    ("$name", seed.Name),
                    ("$notes", ComponentTypeLabel(seed.ComponentType)),
                    ("$configJson", seed.ConfigJson),
                    ("$designPreviewJson", seed.DesignPreviewJson),
                    ("$metadataJson", seed.MetadataJson));
            }
        }
    }

    private static void EnsureKeyboardThemeMetricTokens(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection)
                     .Where((candidate) => candidate.ComponentType == "keyboard"))
        {
            var config = ParseJsonObject(row.ConfigJson);
            var metadata = ParseJsonObject(row.MetadataJson);
            var changed = EnsureKeyboardThemeMetricTokens(config);

            if (metadata["presets"] is JsonArray presets)
            {
                foreach (var preset in presets.OfType<JsonObject>())
                {
                    if (preset["config"] is JsonObject presetConfig)
                    {
                        changed |= EnsureKeyboardThemeMetricTokens(presetConfig);
                    }
                }
            }

            if (!changed)
            {
                continue;
            }

            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", row.Id),
                ("$configJson", config.ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()));
        }
    }

    private static bool EnsureKeyboardThemeMetricTokens(JsonObject config)
    {
        if (config["keyboard"] is not JsonObject keyboard)
        {
            return false;
        }

        var changed = false;
        if (!keyboard.ContainsKey("heightToken"))
        {
            keyboard["heightToken"] = "theme.keyboard.height";
            changed = true;
        }
        if (!keyboard.ContainsKey("keyGapToken"))
        {
            keyboard["keyGapToken"] = "theme.keyboard.keyGap";
            changed = true;
        }
        if (!keyboard.ContainsKey("rowGapToken"))
        {
            keyboard["rowGapToken"] = "theme.keyboard.rowGap";
            changed = true;
        }
        if (keyboard.Remove("popoverBackgroundColorToken")) changed = true;

        return changed;
    }
}
