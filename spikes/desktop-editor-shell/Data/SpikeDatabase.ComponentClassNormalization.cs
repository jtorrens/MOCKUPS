using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

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

    private static void MigrateVideoComponentClassesToMedia(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection).Where((candidate) => candidate.ComponentType == "video"))
        {
            var mediaId = row.Id.EndsWith("_video", StringComparison.Ordinal)
                ? row.Id[..^"_video".Length] + "_media"
                : row.Id + "_media";
            if (ScalarLong(connection, "SELECT COUNT(*) FROM component_classes WHERE id = $id", ("$id", mediaId)) > 0)
            {
                mediaId = row.Id;
            }

            var config = ParseJsonObject(row.ConfigJson);
            MigrateVideoConfigObjectToMedia(config);
            var designPreview = ParseJsonObject(DefaultComponentDesignPreviewJson("media"));
            var metadata = ParseJsonObject(row.MetadataJson);
            MigrateVideoPresetConfigsToMedia(metadata);

            var name = row.Name.Equals("Default Video", StringComparison.Ordinal)
                ? "Default Media"
                : row.Name.Replace("Video", "Media", StringComparison.Ordinal);
            Execute(
                connection,
                """
                UPDATE component_classes
                SET id = $nextId,
                    component_type = 'media',
                    record_class_id = 'component.media',
                    name = $name,
                    notes = $notes,
                    config_json = $configJson,
                    design_preview_json = $designPreviewJson,
                    metadata_json = $metadataJson
                WHERE id = $id
                """,
                ("$id", row.Id),
                ("$nextId", mediaId),
                ("$name", string.IsNullOrWhiteSpace(name) ? "Default Media" : name),
                ("$notes", ComponentTypeLabel("media")),
                ("$configJson", config.ToJsonString()),
                ("$designPreviewJson", designPreview.ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()));

            if (!mediaId.Equals(row.Id, StringComparison.Ordinal))
            {
                ReplaceComponentPresetReferencePrefix(connection, row.Id, mediaId);
            }
        }
    }

    private static void ReplaceComponentPresetReferencePrefix(
        SqliteConnection connection,
        string previousComponentClassId,
        string nextComponentClassId)
    {
        var previousPrefix = previousComponentClassId + "::preset::";
        var nextPrefix = nextComponentClassId + "::preset::";
        Execute(
            connection,
            """
            UPDATE component_classes
            SET config_json = replace(config_json, $previousPrefix, $nextPrefix),
                design_preview_json = replace(design_preview_json, $previousPrefix, $nextPrefix),
                metadata_json = replace(metadata_json, $previousPrefix, $nextPrefix)
            """,
            ("$previousPrefix", previousPrefix),
            ("$nextPrefix", nextPrefix));
    }

    private static bool MigrateVideoPresetConfigsToMedia(JsonObject metadata)
    {
        var changed = false;
        if (metadata["presets"] is not JsonArray presets)
        {
            return false;
        }

        foreach (var preset in presets.OfType<JsonObject>())
        {
            if (preset["config"] is JsonObject config)
            {
                changed |= MigrateVideoConfigObjectToMedia(config);
            }
        }

        return changed;
    }

    private static bool MigrateVideoConfigObjectToMedia(JsonObject config)
    {
        if (config["media"] is JsonObject)
        {
            config.Remove("video");
            config.Remove("style");
            return false;
        }

        var video = config["video"] as JsonObject;
        var media = new JsonObject
        {
            ["surfaceSlot"] = video?["surfaceSlot"]?.DeepClone() ?? ComponentSurfaceSlot(DefaultComponentPresetId),
            ["controlBarHeight"] = 56,
            ["inlineTopIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
            ["inlineCenterIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
            ["inlineBottomIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
            ["fullScreenTopIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
            ["fullScreenCenterIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
            ["fullScreenBottomIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
            ["controlsFadeDelayMs"] = 900,
            ["controlsFadeDurationMs"] = 180,
            ["motion"] = JsonNode.Parse(MediaMotionDefault().ToJsonString()),
        };
        config["media"] = media;
        config.Remove("video");
        config.Remove("textInput");
        config.Remove("style");
        return true;
    }

    private static void EnsureComponentClassRecordClassIds(SqliteConnection connection)
    {
        foreach (var seed in ComponentSeedRows)
        {
            Execute(
                connection,
                "UPDATE component_classes SET record_class_id = $recordClassId WHERE component_type = $componentType AND record_class_id <> $recordClassId",
                ("$componentType", seed.ComponentType),
                ("$recordClassId", seed.RecordClassId));
        }
    }

    private static void EnsureComponentClassConfigDefaults(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection))
        {
            var config = ParseJsonObject(string.IsNullOrWhiteSpace(row.ConfigJson) ? "{}" : row.ConfigJson);
            var defaults = ParseJsonObject(DefaultComponentClassConfigJson(row.ComponentType));
            var configChanged = NormalizeComponentConfigDefaults(
                connection,
                row.ProjectId,
                row.ComponentType,
                config,
                defaults);

            var designPreview = ParseJsonObject(string.IsNullOrWhiteSpace(row.DesignPreviewJson) ? "{}" : row.DesignPreviewJson);
            var designPreviewDefaults = ParseJsonObject(DefaultComponentDesignPreviewJson(row.ComponentType));
            var designPreviewChanged = JsonPath.MergeMissing(designPreview, designPreviewDefaults);
            designPreviewChanged |= EnsureComponentInputs(row.ComponentType, designPreview, designPreviewDefaults);
            designPreviewChanged |= EnsureComponentDesignPreviewText(row.ComponentType, designPreview);
            designPreviewChanged |= EnsureButtonIconPreviewSize(row.ComponentType, designPreview);
            designPreviewChanged |= NormalizeBubbleWriteOnFrameInputs(row.ComponentType, designPreview);
            designPreviewChanged |= EnsureComponentPreviewActions(row.ComponentType, designPreview, designPreviewDefaults);

            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(row.MetadataJson) ? "{}" : row.MetadataJson);
            var metadataChanged = EnsureDefaultComponentPreset(metadata, config);
            metadataChanged |= NormalizeComponentPresetConfigs(
                connection,
                row.ProjectId,
                row.ComponentType,
                metadata,
                defaults);

            if (!configChanged && !designPreviewChanged && !metadataChanged)
            {
                continue;
            }

            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson, design_preview_json = $designPreviewJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", row.Id),
                ("$configJson", config.ToJsonString()),
                ("$designPreviewJson", designPreview.ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()));
        }
    }

    private static bool EnsureDefaultComponentPreset(JsonObject metadata, JsonObject config)
    {
        if (metadata["presets"] is not JsonArray presets)
        {
            presets = [];
            metadata["presets"] = presets;
        }

        foreach (var presetNode in presets.OfType<JsonObject>())
        {
            if (!JsonPath.String(presetNode, "id", "").Equals(DefaultComponentPresetId, StringComparison.Ordinal))
            {
                continue;
            }

            var changed = false;
            if (!JsonPath.String(presetNode, "name", "").Equals("Default", StringComparison.Ordinal))
            {
                presetNode["name"] = "Default";
                changed = true;
            }

            if (!JsonBool(presetNode, ["protected"]))
            {
                presetNode["protected"] = true;
                changed = true;
            }

            if (!JsonBool(presetNode, ["locked"]))
            {
                presetNode["locked"] = true;
                changed = true;
            }

            if (presetNode["config"] is not JsonObject)
            {
                presetNode["config"] = JsonNode.Parse(config.ToJsonString());
                changed = true;
            }

            return changed;
        }

        presets.Insert(0, new JsonObject
        {
            ["id"] = DefaultComponentPresetId,
            ["name"] = "Default",
            ["protected"] = true,
            ["locked"] = true,
            ["config"] = JsonNode.Parse(config.ToJsonString()),
        });
        return true;
    }

    private static bool NormalizeComponentPresetConfigs(
        SqliteConnection connection,
        string projectId,
        string componentType,
        JsonObject metadata,
        JsonObject defaults)
    {
        var changed = false;
        foreach (var preset in EnsurePresetArray(metadata).OfType<JsonObject>())
        {
            JsonObject presetConfig;
            if (preset["config"] is JsonObject configObject)
            {
                presetConfig = configObject;
            }
            else if (preset["configJson"] is JsonValue configValue
                && configValue.TryGetValue<string>(out var configJson)
                && !string.IsNullOrWhiteSpace(configJson))
            {
                presetConfig = ParseJsonObject(configJson);
                preset["config"] = presetConfig;
                changed = true;
            }
            else
            {
                presetConfig = ParseJsonObject(defaults.ToJsonString());
                preset["config"] = presetConfig;
                changed = true;
            }

            if (!NormalizeComponentConfigDefaults(connection, projectId, componentType, presetConfig, defaults))
            {
                continue;
            }

            changed = true;
        }

        return changed;
    }

    private static bool NormalizeComponentConfigDefaults(
        SqliteConnection connection,
        string projectId,
        string componentType,
        JsonObject config,
        JsonObject defaults)
    {
        var changed = NormalizeAvatarLabelPlacement(componentType, config);
        changed |= NormalizeButtonIconLabelSlot(componentType, config);
        changed |= NormalizeButtonIconSizing(componentType, config);
        changed |= NormalizeAudioEmbeddedSlots(componentType, config);
        changed |= NormalizeComponentSlots(componentType, config);
        changed |= NormalizeTextInputBarSlots(connection, projectId, componentType, config);
        changed |= NormalizeKeyboardSlots(connection, projectId, componentType, config);
        changed |= NormalizeMediaSlots(connection, projectId, componentType, config);
        changed |= NormalizeBubbleSlots(connection, projectId, componentType, config);
        changed |= NormalizeEmbeddedSlotPresetIds(connection, projectId, config);
        changed |= NormalizeComponentInputBindingPresetIds(connection, projectId, config);
        changed |= NormalizeComponentTypographyStyles(config);
        changed |= JsonPath.MergeMissing(config, defaults);
        changed |= NormalizeEmbeddedSlotPresetIds(connection, projectId, config);
        changed |= NormalizeComponentInputBindingPresetIds(connection, projectId, config);
        changed |= NormalizeComponentSpacingTokens(config);
        changed |= NormalizeComponentIconSizeTokens(config);
        changed |= NormalizeReliefIntensity(config, "reliefTopIntensity");
        changed |= NormalizeReliefIntensity(config, "reliefBottomIntensity");
        return changed;
    }

    private static bool NormalizeComponentInputBindingPresetIds(
        SqliteConnection connection,
        string projectId,
        JsonObject config)
    {
        var changed = false;
        changed |= NormalizeComponentInputBindingSlot(connection, projectId, config, ["textInput", "textBoxInputs", "leftIconRowSlot"], ["textInput", "textBoxInputs", "leftIconRowPresetId"], "iconRow");
        changed |= NormalizeComponentInputBindingSlot(connection, projectId, config, ["textInput", "textBoxInputs", "rightIconRowSlot"], ["textInput", "textBoxInputs", "rightIconRowPresetId"], "iconRow");
        changed |= NormalizeComponentInputBindingSlot(connection, projectId, config, ["textInput", "textBoxInputs", "buttonIconSlot"], ["textInput", "textBoxInputs", "buttonIconPresetId"], "buttonIcon");
        return changed;
    }

    private static bool NormalizeComponentInputBindingSlot(
        SqliteConnection connection,
        string projectId,
        JsonObject config,
        string[] slotPath,
        string[] legacyPresetPath,
        string componentType)
    {
        var changed = false;
        var legacyPresetId = JsonPath.String(config, legacyPresetPath);
        if (JsonPath.Get(config, slotPath) is not JsonObject slot)
        {
            var currentValue = string.IsNullOrWhiteSpace(legacyPresetId) ? DefaultComponentPresetId : legacyPresetId;
            slot = ComponentSurfaceSlot(currentValue);
            JsonPath.Set(config, slotPath, slot);
            changed = true;
        }

        var presetId = JsonPath.String(slot, ["presetId"]);
        var normalizedValue = NormalizeComponentPresetReference(
            connection,
            projectId,
            componentType,
            string.IsNullOrWhiteSpace(presetId) ? legacyPresetId : presetId);
        if (!string.IsNullOrWhiteSpace(normalizedValue) && !normalizedValue.Equals(presetId, StringComparison.Ordinal))
        {
            slot["presetId"] = normalizedValue;
            changed = true;
        }

        if (slot["overrides"] is not JsonObject)
        {
            slot["overrides"] = new JsonObject();
            changed = true;
        }

        if (JsonPath.Get(config, legacyPresetPath) is not null)
        {
            RemoveJsonValue(config, legacyPresetPath);
            changed = true;
        }

        return changed;
    }
}
