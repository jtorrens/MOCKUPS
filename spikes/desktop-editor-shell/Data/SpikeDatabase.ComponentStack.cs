using Microsoft.Data.Sqlite;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void EnsureComponentStackClasses(SqliteConnection connection)
    {
        var seed = ComponentSeedRows.Single((candidate) => candidate.ComponentType == "componentStack");
        foreach (var projectId in QueryProjectRows(connection).Select((project) => project.Id))
        {
            if (ScalarLong(connection,
                    "SELECT COUNT(*) FROM component_classes WHERE project_id = $projectId AND component_type = 'componentStack'",
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
                     .Where((candidate) => candidate.ComponentType == "componentStack"))
        {
            var classConfig = ParseJsonObject(row.ConfigJson);
            var previousPreview = ParseJsonObject(row.DesignPreviewJson);
            var runtimeValues = ComponentStackRuntimeValues(classConfig, previousPreview);
            var migratedItems = ComponentStackItemsFromRetiredConfig(classConfig);
            if (migratedItems.Count == 0)
            {
                migratedItems = ComponentStackItemsFromRuntimePreview(previousPreview);
            }
            migratedItems = MigrateComponentStackGapBefore(migratedItems);
            NormalizeComponentStackConfig(classConfig);

            var metadata = ParseJsonObject(row.MetadataJson);
            if (metadata["presets"] is JsonArray presets)
            {
                foreach (var preset in presets.OfType<JsonObject>())
                {
                    if (preset["config"] is not JsonObject presetConfig) continue;
                    if (migratedItems.Count == 0)
                    {
                        migratedItems = ComponentStackItemsFromRetiredConfig(presetConfig);
                    }
                    NormalizeComponentStackConfig(presetConfig);
                }
            }

            var preview = ComponentStackDesignPreview(
                migratedItems,
                runtimeValues.SizingMode,
                runtimeValues.StartGapToken,
                runtimeValues.EndGapToken);
            Execute(connection,
                """
                UPDATE component_classes
                SET config_json = $configJson,
                    design_preview_json = $designPreviewJson,
                    metadata_json = $metadataJson
                WHERE id = $id
                """,
                ("$configJson", classConfig.ToJsonString()),
                ("$designPreviewJson", preview.ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()),
                ("$id", row.Id));
        }

        Execute(connection,
            "INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json) VALUES ('component.componentStack', $layoutJson)",
            ("$layoutJson", MinimalEditorLayoutJson("component.componentStack")));
    }

    private static void NormalizeComponentStackConfig(JsonObject config)
    {
        config.Clear();
        config["componentStack"] = new JsonObject();
    }

    private static (string SizingMode, string StartGapToken, string EndGapToken) ComponentStackRuntimeValues(
        JsonObject config,
        JsonObject preview)
    {
        var previous = config["componentStack"] as JsonObject;
        return (
            previous?["sizingMode"]?.GetValue<string>()
                ?? preview["sizingMode"]?.GetValue<string>()
                ?? "fill",
            previous?["startGapToken"]?.GetValue<string>()
                ?? preview["startGapToken"]?.GetValue<string>()
                ?? "theme.spacing.none",
            previous?["endGapToken"]?.GetValue<string>()
                ?? preview["endGapToken"]?.GetValue<string>()
                ?? "theme.spacing.none");
    }

    private static JsonArray ComponentStackItemsFromRetiredConfig(JsonObject config)
    {
        var result = new JsonArray();
        if (config["componentStack"] is not JsonObject stack
            || stack["order"] is not JsonArray order
            || stack["slots"] is not JsonObject slots)
        {
            return result;
        }

        foreach (var idNode in order)
        {
            var id = idNode?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(id) || slots[id] is not JsonObject slot) continue;
            var gap = slot["gapAfter"] as JsonObject
                ?? throw new InvalidOperationException($"Component Stack item '{id}' is missing its gap contract.");
            result.Add(new JsonObject
            {
                ["id"] = id,
                ["presetId"] = slot["presetId"]?.DeepClone(),
                ["overrides"] = slot["overrides"]?.DeepClone() ?? new JsonObject(),
                ["inputs"] = slot["inputs"]?.DeepClone() ?? new JsonObject(),
                ["alignment"] = slot["alignment"]?.DeepClone(),
                ["gapMode"] = gap["mode"]?.DeepClone(),
                ["gapToken"] = gap["token"]?.DeepClone(),
                ["reflowWeight"] = gap["weight"]?.DeepClone(),
            });
        }
        return result;
    }

    private static JsonArray ComponentStackItemsFromRuntimePreview(JsonObject preview)
    {
        return preview["items"] is JsonArray items
            ? new JsonArray(items.Select((item) => item?.DeepClone()).ToArray())
            : new JsonArray();
    }

    private static JsonArray MigrateComponentStackGapBefore(JsonArray items)
    {
        if (!items.OfType<JsonObject>().Any((item) => item.ContainsKey("gapMode")))
        {
            return items;
        }

        var retiredItems = items.OfType<JsonObject>().ToList();
        var migrated = new JsonArray();
        for (var index = 0; index < retiredItems.Count; index++)
        {
            var item = retiredItems[index].DeepClone() as JsonObject ?? new JsonObject();
            var precedingGapOwner = index > 0 ? retiredItems[index - 1] : null;
            item.Remove("gapMode");
            item.Remove("gapToken");
            item.Remove("reflowWeight");
            item["gapBeforeMode"] = precedingGapOwner?["gapMode"]?.DeepClone() ?? "fixed";
            item["gapBeforeToken"] = precedingGapOwner?["gapToken"]?.DeepClone() ?? "theme.spacing.none";
            item["gapBeforeWeight"] = precedingGapOwner?["reflowWeight"]?.DeepClone() ?? 1;
            migrated.Add(item);
        }
        return migrated;
    }
}
