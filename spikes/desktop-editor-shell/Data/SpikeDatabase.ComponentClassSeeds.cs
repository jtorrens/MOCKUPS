using Microsoft.Data.Sqlite;
using System.Text.Json.Nodes;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void EnsureButtonComponentClasses(SqliteConnection connection)
    {
        var seed = ComponentSeedRows.Single((candidate) => candidate.ComponentType == "button");
        foreach (var projectId in QueryProjectRows(connection).Select((project) => project.Id))
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM component_classes WHERE project_id = $projectId AND component_type = 'button'", ("$projectId", projectId)) == 0)
            {
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

            var buttonId = ScalarString(connection, "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = 'button'", ("$projectId", projectId))!;
            var surfaceId = ScalarString(connection, "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = 'surface'", ("$projectId", projectId))!;
            var labelId = ScalarString(connection, "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = 'label'", ("$projectId", projectId))!;
            var config = ParseJsonObject(ScalarString(connection, "SELECT config_json FROM component_classes WHERE id = $id", ("$id", buttonId)) ?? "{}");
            var metadata = ParseJsonObject(ScalarString(connection, "SELECT metadata_json FROM component_classes WHERE id = $id", ("$id", buttonId)) ?? "{}");
            var preview = ParseJsonObject(ScalarString(connection, "SELECT design_preview_json FROM component_classes WHERE id = $id", ("$id", buttonId)) ?? "{}");
            NormalizeButtonStateConfiguration(config);
            SetButtonPresetReferences(config, surfaceId, labelId);
            if (metadata["presets"] is JsonArray presets)
            {
                foreach (var preset in presets.OfType<JsonObject>())
                {
                    if (preset["config"] is JsonObject presetConfig)
                    {
                        NormalizeButtonStateConfiguration(presetConfig);
                        SetButtonPresetReferences(presetConfig, surfaceId, labelId);
                    }
                }
            }
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson, design_preview_json = $previewJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", buttonId),
                ("$configJson", config.ToJsonString()),
                ("$previewJson", NormalizeButtonDesignPreview(preview).ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()));
        }

        Execute(
            connection,
            "INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json) VALUES ('component.button', $layoutJson)",
            ("$layoutJson", MinimalEditorLayoutJson("component.button")));
    }

    private static JsonObject NormalizeButtonDesignPreview(JsonObject preview)
    {
        var defaults = ParseJsonObject(DefaultComponentDesignPreviewJson("button"));
        preview["pushTrigger"] = false;
        preview["pushElapsedMs"] = 0;
        preview["actions"] = defaults["actions"]?.DeepClone();
        return preview;
    }

    private static void SetButtonPresetReferences(JsonObject config, string surfaceClassId, string labelClassId)
    {
        if (config["button"] is not JsonObject button) return;
        if (button["states"] is not JsonObject states) return;
        foreach (var state in states.Select((entry) => entry.Value).OfType<JsonObject>())
        {
            if (state["surfaceSlot"] is JsonObject surfaceSlot) surfaceSlot["presetId"] = $"{surfaceClassId}::preset::default";
            if (state["labelSlot"] is JsonObject labelSlot) labelSlot["presetId"] = $"{labelClassId}::preset::default";
        }
    }

    private static void NormalizeButtonStateConfiguration(JsonObject config)
    {
        if (config["button"] is not JsonObject button || button["states"] is JsonObject) return;
        var surfaceSlot = button["surfaceSlot"]?.DeepClone() ?? ComponentSurfaceSlot(DefaultComponentPresetId);
        var labelSlot = button["labelSlot"]?.DeepClone() ?? ComponentSurfaceSlot(DefaultComponentPresetId);
        var iconColor = button["iconColorToken"]?.GetValue<string>() ?? "theme.colors.icon";
        JsonObject State(string stateIconColor, double opacity) => new()
        {
            ["surfaceSlot"] = surfaceSlot.DeepClone(),
            ["labelSlot"] = labelSlot.DeepClone(),
            ["iconColorToken"] = stateIconColor,
            ["opacity"] = opacity,
        };
        button["states"] = new JsonObject
        {
            ["normal"] = State(iconColor, 1),
            ["active"] = State("theme.colors.accent", 1),
            ["pushed"] = State("theme.colors.accent", 0.72),
            ["disabled"] = State(iconColor, 0.4),
        };
        button.Remove("surfaceSlot");
        button.Remove("labelSlot");
        button.Remove("iconColorToken");
    }

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

    private static void NormalizeKeyboardConfiguration(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection)
                     .Where((candidate) => candidate.ComponentType == "keyboard"))
        {
            var config = ParseJsonObject(row.ConfigJson);
            var metadata = ParseJsonObject(row.MetadataJson);
            var changed = NormalizeKeyboardConfiguration(config);

            if (metadata["presets"] is JsonArray presets)
            {
                foreach (var preset in presets.OfType<JsonObject>())
                {
                    if (preset["config"] is JsonObject presetConfig)
                    {
                        changed |= NormalizeKeyboardConfiguration(presetConfig);
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

    private static bool NormalizeKeyboardConfiguration(JsonObject config)
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
        foreach (var key in new[]
                 {
                     "backgroundColorToken",
                     "backgroundAlpha",
                     "keyBackgroundColorToken",
                     "specialKeyBackgroundColorToken",
                     "pressedKeyBackgroundColorToken",
                     "keyTextColorToken",
                     "keyBorderColorToken",
                     "popoverBackgroundColorToken",
                     "specialKeyTextScale",
                 })
        {
            changed |= keyboard.Remove(key);
        }
        changed |= config.Remove("style");

        return changed;
    }
}
