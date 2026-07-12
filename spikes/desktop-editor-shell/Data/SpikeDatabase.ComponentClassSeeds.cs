using Microsoft.Data.Sqlite;
using System;
using System.Text.Json.Nodes;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void NormalizeDefaultComponentConfigAuthority(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection))
        {
            var defaultConfig = ComponentClassPresets(row.MetadataJson)
                .FirstOrDefault((preset) => preset.Id.Equals(DefaultComponentPresetId, StringComparison.Ordinal))
                ?.ConfigJson;
            if (string.IsNullOrWhiteSpace(defaultConfig) || defaultConfig == "{}")
            {
                throw new InvalidOperationException($"Component class '{row.Id}' has no canonical Default variant config.");
            }
            if (JsonNode.DeepEquals(ParseJsonObject(row.ConfigJson), ParseJsonObject(defaultConfig)))
            {
                continue;
            }
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson WHERE id = $id",
                ("$id", row.Id),
                ("$configJson", defaultConfig!));
        }
    }

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
        if (preview["contentMode"] is null) preview["contentMode"] = "text";
        preview["iconSizeToken"] ??= "theme.iconSizes.m";
        preview["textSizeToken"] ??= "theme.typography.sizes.s";
        preview["pushTrigger"] = false;
        preview["pushElapsedMs"] = 0;
        preview["actions"] = defaults["actions"]?.DeepClone();
        return preview;
    }

    private static void NormalizeIconRowButtonCollections(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection))
        {
            var buttonClassId = ScalarString(
                connection,
                "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = 'button'",
                ("$projectId", row.ProjectId))
                ?? throw new InvalidOperationException($"Project '{row.ProjectId}' has no Button component class.");
            var buttonPresetId = $"{buttonClassId}::preset::{DefaultComponentPresetId}";
            var config = ParseJsonObject(row.ConfigJson);
            var metadata = ParseJsonObject(row.MetadataJson);
            var preview = ParseJsonObject(row.DesignPreviewJson);
            NormalizeIconRowNodes(config, buttonPresetId);
            NormalizeIconRowNodes(metadata, buttonPresetId);
            NormalizeIconRowNodes(preview, buttonPresetId);
            if (row.ComponentType.Equals("iconRow", StringComparison.Ordinal))
            {
                var defaults = ParseJsonObject(DefaultComponentDesignPreviewJson("iconRow"));
                if (preview["items"] is not JsonArray)
                {
                    preview["items"] = defaults["items"]?.DeepClone();
                    NormalizeIconRowNodes(preview, buttonPresetId);
                }
                preview["inputs"] = defaults["inputs"]?.DeepClone();
                preview["collections"] = defaults["collections"]?.DeepClone();
                NormalizeIconRowNodes(preview, buttonPresetId);
            }
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson, design_preview_json = $previewJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", row.Id),
                ("$configJson", config.ToJsonString()),
                ("$previewJson", preview.ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()));
        }
    }

    private static void NormalizeIconRowNodes(JsonNode? node, string buttonPresetId)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array.ToList()) NormalizeIconRowNodes(child, buttonPresetId);
            return;
        }
        if (node is not JsonObject obj) return;

        if (obj["iconRow"] is JsonObject iconRowConfig)
        {
            iconRowConfig.Remove("size");
            iconRowConfig.Remove("buttonIconSlot");
            iconRowConfig["sizeSource"] ??= "shared";
            iconRowConfig["iconSizeToken"] ??= "theme.iconSizes.m";
            iconRowConfig["textSizeToken"] ??= "theme.typography.sizes.s";
        }
        if (obj["iconBar"] is JsonObject iconBarConfig)
        {
            iconBarConfig.Remove("iconButtonSlot");
            iconBarConfig["sizeSource"] ??= "shared";
            iconBarConfig["iconSizeToken"] ??= "theme.iconSizes.m";
            iconBarConfig["textSizeToken"] ??= "theme.typography.sizes.s";
        }

        var looksLikeLegacyIconRow = obj["icons"] is JsonArray
            && obj["orientation"] is not null
            && obj["gap"] is not null
            && (obj["actionIconNumber"] is not null || obj["buttonIconPresetId"] is not null || obj["size"] is not null);
        if (looksLikeLegacyIconRow && obj["icons"] is JsonArray icons)
        {
            obj["items"] = MigratedIconRowItems(icons, buttonPresetId);
            foreach (var retired in new[]
            {
                "size", "icons", "actionIconNumber", "actionBackgroundAlpha", "actionBackgroundColor",
                "actionIconColor", "buttonIconPresetId", "buttonIconOverrides", "iconColorTokenOverride",
            }) obj.Remove(retired);
        }
        if (obj["items"] is JsonArray items)
        {
            foreach (var item in items.OfType<JsonObject>())
            {
                item["iconSizeToken"] ??= "theme.iconSizes.m";
                item["textSizeToken"] ??= "theme.typography.sizes.s";
                if (item["buttonPresetId"] is JsonValue presetValue
                    && presetValue.TryGetValue<string>(out var preset)
                    && (!preset.Contains("::preset::", StringComparison.Ordinal)
                        || preset.StartsWith("button::preset::", StringComparison.Ordinal)))
                {
                    item["buttonPresetId"] = buttonPresetId;
                }
            }
        }
        if (obj["jsonKey"]?.GetValue<string>() == "buttonPresetId"
            && obj["componentType"]?.GetValue<string>() == "button")
        {
            obj["defaultValue"] = buttonPresetId;
        }
        foreach (var child in obj.Select((entry) => entry.Value).ToList()) NormalizeIconRowNodes(child, buttonPresetId);
    }

    private static JsonArray MigratedIconRowItems(JsonArray icons, string buttonPresetId)
    {
        var items = new JsonArray();
        for (var index = 0; index < icons.Count; index++)
        {
            items.Add(new JsonObject
            {
                ["id"] = $"button_{index + 1:000}",
                ["buttonPresetId"] = buttonPresetId,
                ["contentMode"] = "icon",
                ["state"] = "normal",
                ["iconToken"] = icons[index]?.GetValue<string>() ?? "",
                ["text"] = "",
                ["pushTrigger"] = false,
                ["pushElapsedMs"] = 0,
                ["buttonOverrides"] = new JsonObject(),
            });
        }
        return items;
    }

    private static void RetireButtonIconComponentClasses(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection))
        {
            var buttonClassId = ScalarString(
                connection,
                "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = 'button'",
                ("$projectId", row.ProjectId))
                ?? throw new InvalidOperationException($"Project '{row.ProjectId}' has no Button component class.");
            var buttonPresetId = $"{buttonClassId}::preset::{DefaultComponentPresetId}";
            var config = ParseJsonObject(row.ConfigJson);
            var metadata = ParseJsonObject(row.MetadataJson);
            var preview = ParseJsonObject(row.DesignPreviewJson);
            RetireButtonIconReferences(config, buttonPresetId);
            RetireButtonIconReferences(metadata, buttonPresetId);
            RetireButtonIconReferences(preview, buttonPresetId);
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson, design_preview_json = $previewJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", row.Id),
                ("$configJson", config.ToJsonString()),
                ("$previewJson", preview.ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()));
        }
        Execute(connection, "DELETE FROM component_classes WHERE component_type = 'buttonIcon'");
        Execute(connection, "DELETE FROM editor_layouts WHERE record_class_id = 'component.buttonIcon'");
    }

    private static void RetireButtonIconReferences(JsonNode? node, string buttonPresetId)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array.ToList()) RetireButtonIconReferences(child, buttonPresetId);
            return;
        }
        if (node is not JsonObject obj) return;

        obj.Remove("buttonIconSlot");
        obj.Remove("buttonIconPresetId");
        if (obj["badgeSlot"] is JsonObject badge
            && badge["showBadge"] is not null
            && badge["iconToken"] is not null)
        {
            var legacySize = badge["overrides"]?["buttonIcon"]?["size"]?.GetValue<double?>();
            badge["size"] = legacySize is > 0 ? legacySize.Value : badge["size"]?.GetValue<double?>() ?? 16;
            badge["presetId"] = buttonPresetId;
            badge["overrides"] = new JsonObject();
            badge.Remove("backgroundColor");
            badge.Remove("iconColor");
        }
        foreach (var child in obj.Select((entry) => entry.Value).ToList()) RetireButtonIconReferences(child, buttonPresetId);
    }

    private static void SetButtonPresetReferences(JsonObject config, string surfaceClassId, string labelClassId)
    {
        if (config["button"] is not JsonObject button) return;
        if (button["states"] is not JsonObject states) return;
        foreach (var state in states.Select((entry) => entry.Value).OfType<JsonObject>())
        {
            QualifyButtonPresetReference(state["surfaceSlot"] as JsonObject, surfaceClassId);
            QualifyButtonPresetReference(state["labelSlot"] as JsonObject, labelClassId);
        }
    }

    private static void QualifyButtonPresetReference(JsonObject? slot, string componentClassId)
    {
        if (slot is null) return;
        var presetId = slot["presetId"] is JsonValue value && value.TryGetValue<string>(out var text)
            && !string.IsNullOrWhiteSpace(text)
                ? text
                : DefaultComponentPresetId;
        if (presetId.Contains("::preset::", StringComparison.Ordinal)) return;
        slot["presetId"] = $"{componentClassId}::preset::{presetId}";
    }

    private static void NormalizeButtonStateConfiguration(JsonObject config)
    {
        if (config["button"] is not JsonObject button) return;
        button.Remove("contentMode");
        button.Remove("iconSizeToken");
        if (button["states"] is JsonObject existingStates)
        {
            foreach (var state in existingStates.Select((entry) => entry.Value).OfType<JsonObject>())
            {
                state.Remove("opacity");
            }
            return;
        }
        var surfaceSlot = button["surfaceSlot"]?.DeepClone() ?? ComponentSurfaceSlot(DefaultComponentPresetId);
        var labelSlot = button["labelSlot"]?.DeepClone() ?? ComponentSurfaceSlot(DefaultComponentPresetId);
        var iconColor = button["iconColorToken"]?.GetValue<string>() ?? "theme.colors.icon";
        JsonObject State(string stateIconColor) => new()
        {
            ["surfaceSlot"] = surfaceSlot.DeepClone(),
            ["labelSlot"] = labelSlot.DeepClone(),
            ["iconColorToken"] = stateIconColor,
        };
        button["states"] = new JsonObject
        {
            ["normal"] = State(iconColor),
            ["active"] = State("theme.colors.accent"),
            ["pushed"] = State("theme.colors.accent"),
            ["disabled"] = State(iconColor),
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
