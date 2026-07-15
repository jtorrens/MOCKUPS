using Microsoft.Data.Sqlite;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void EnsureCollectionStackClasses(SqliteConnection connection)
    {
        var seed = ComponentSeedRows.Single((candidate) => candidate.ComponentType == "collectionStack");
        foreach (var projectId in QueryProjectRows(connection).Select((project) => project.Id))
        {
            if (ScalarLong(connection,
                    "SELECT COUNT(*) FROM component_classes WHERE project_id = $projectId AND component_type = 'collectionStack'",
                    ("$projectId", projectId)) == 0)
            {
                Execute(connection,
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

        foreach (var row in QueryComponentClassRows(connection)
                     .Where((candidate) => candidate.ComponentType == "collectionStack"))
        {
            var config = ParseJsonObject(row.ConfigJson);
            config.Clear();
            config["collectionStack"] = new JsonObject();

            var previous = ParseJsonObject(row.DesignPreviewJson);
            var items = previous["items"] is JsonArray previousItems
                ? previousItems.DeepClone() as JsonArray ?? new JsonArray()
                : new JsonArray();
            var preview = CollectionStackDesignPreview(
                items,
                previous["distributionMode"]?.GetValue<string>() ?? "stacked",
                previous["sizingMode"]?.GetValue<string>() ?? "content",
                previous["startGapToken"]?.GetValue<string>() ?? "theme.spacing.none",
                previous["endGapToken"]?.GetValue<string>() ?? "theme.spacing.none",
                previous["stackDirection"]?.GetValue<string>() ?? "down",
                previous["stackOffsetToken"]?.GetValue<string>() ?? "theme.spacing.m");

            var metadata = ParseJsonObject(row.MetadataJson);
            if (metadata["presets"] is JsonArray presets)
            {
                foreach (var preset in presets.OfType<JsonObject>())
                {
                    if (preset["config"] is not JsonObject presetConfig) continue;
                    presetConfig.Clear();
                    presetConfig["collectionStack"] = new JsonObject();
                }
            }

            Execute(connection,
                """
                UPDATE component_classes
                SET config_json = $configJson,
                    design_preview_json = $designPreviewJson,
                    metadata_json = $metadataJson
                WHERE id = $id
                """,
                ("$configJson", config.ToJsonString()),
                ("$designPreviewJson", preview.ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()),
                ("$id", row.Id));
        }

        Execute(connection,
            "INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json) VALUES ('component.collectionStack', $layoutJson)",
            ("$layoutJson", MinimalEditorLayoutJson("component.collectionStack")));
    }
}
