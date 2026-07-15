using Microsoft.Data.Sqlite;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void EnsureKeypadComponentClasses(SqliteConnection connection)
    {
        var seed = ComponentSeedRows.Single((candidate) => candidate.ComponentType == "keypad");
        foreach (var projectId in QueryProjectRows(connection).Select((project) => project.Id))
        {
            var existing = QueryComponentClassRows(connection).FirstOrDefault((candidate) =>
                candidate.ProjectId == projectId && candidate.ComponentType == "keypad");
            if (existing is null)
            {
                var config = ParseJsonObject(seed.ConfigJson);
                NormalizeKeypadConfig(connection, config, projectId);
                var metadata = ParseJsonObject(seed.MetadataJson);
                SetDefaultComponentPresetConfig(metadata, config);
                var preview = ParseJsonObject(seed.DesignPreviewJson);
                preview["pushedKey"] = "";
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
                    ("$configJson", config.ToJsonString()),
                    ("$designPreviewJson", preview.ToJsonString()),
                    ("$metadataJson", metadata.ToJsonString()));
                continue;
            }

            var existingConfig = ParseJsonObject(existing.ConfigJson);
            var existingMetadata = ParseJsonObject(existing.MetadataJson);
            NormalizeKeypadConfig(connection, existingConfig, projectId);
            if (existingMetadata["presets"] is JsonArray presets)
            {
                foreach (var preset in presets.OfType<JsonObject>())
                {
                    if (preset["config"] is JsonObject presetConfig)
                    {
                        NormalizeKeypadConfig(connection, presetConfig, projectId);
                    }
                }
            }
            var existingPreview = ParseJsonObject(existing.DesignPreviewJson);
            existingPreview["pushedKey"] ??= "";
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson, design_preview_json = $previewJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", existing.Id),
                ("$configJson", existingConfig.ToJsonString()),
                ("$previewJson", existingPreview.ToJsonString()),
                ("$metadataJson", existingMetadata.ToJsonString()));
        }

        Execute(
            connection,
            "INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json) VALUES ('component.keypad', $layoutJson)",
            ("$layoutJson", KeypadEditorLayoutJson()));
    }

    private static void NormalizeKeypadConfig(
        SqliteConnection connection,
        JsonObject config,
        string projectId)
    {
        var keypad = config["keypad"] as JsonObject
            ?? throw new InvalidOperationException("Missing Keypad config.");
        var labelReference = SeededComponentPresetReference(projectId, "label");
        keypad["iconSizeToken"] ??= "theme.iconSizes.m";

        var legacyNormal = keypad["normalKeySlot"]?.DeepClone() as JsonObject;
        var states = keypad["states"] as JsonObject ?? new JsonObject();
        keypad["states"] = states;
        var normalState = states["normal"] as JsonObject;
        var commonLabelSlot = keypad["labelSlot"] as JsonObject
            ?? normalState?["labelSlot"]?.DeepClone() as JsonObject
            ?? legacyNormal
            ?? KeypadLabelSlot("theme.colors.textPrimary");
        keypad["labelSlot"] = commonLabelSlot;
        commonLabelSlot["presetId"] = QualifiedKeypadReference(commonLabelSlot["presetId"], labelReference);
        commonLabelSlot["overrides"] ??= new JsonObject();
        NormalizeCommonKeypadLabelSlot(commonLabelSlot);

        EnsureKeypadState(connection, projectId, states, "normal", "theme.colors.textPrimary");
        EnsureKeypadState(connection, projectId, states, "active", "theme.colors.accent");
        EnsureKeypadState(connection, projectId, states, "pushed", "theme.colors.accent");
        EnsureKeypadState(connection, projectId, states, "disabled", "theme.colors.textSecondary");
        keypad.Remove("normalKeySlot");
        keypad.Remove("activeKeySlot");
        keypad.Remove("disabledKeySlot");

        if (keypad["keys"] is not JsonArray keys)
        {
            throw new InvalidOperationException("Missing Keypad keys collection.");
        }
        foreach (var key in keys.OfType<JsonObject>())
        {
            if (string.Equals(key["kind"]?.GetValue<string>(), "key", StringComparison.Ordinal))
            {
                key["kind"] = "text";
            }
            if (string.IsNullOrWhiteSpace(key["iconToken"]?.GetValue<string>()))
            {
                key["iconToken"] = "app_clock";
            }
        }
    }

    private static void EnsureKeypadState(
        SqliteConnection connection,
        string projectId,
        JsonObject states,
        string stateId,
        string defaultTextColorToken)
    {
        var state = states[stateId] as JsonObject ?? new JsonObject();
        states[stateId] = state;
        var migratedSurface = ResolveMigratedKeypadSurface(connection, projectId, state["surfaceSlot"] as JsonObject);
        state["backgroundColorToken"] ??= migratedSurface.BackgroundColorToken;
        state["backgroundAlpha"] ??= migratedSurface.BackgroundAlpha;
        state["borderAlpha"] ??= migratedSurface.BorderAlpha;
        state["textColorToken"] ??= state["iconColorToken"]?.DeepClone()
            ?? JsonValue.Create(defaultTextColorToken);
        state.Remove("surfaceSlot");
        state.Remove("labelSlot");
        state.Remove("iconColorToken");
    }

    private static (string BackgroundColorToken, double BackgroundAlpha, double BorderAlpha)
        ResolveMigratedKeypadSurface(
            SqliteConnection connection,
            string projectId,
            JsonObject? surfaceSlot)
    {
        if (surfaceSlot is null)
        {
            return ("theme.colors.surface", 1, 1);
        }
        var defaultReference = SeededComponentPresetReference(projectId, "surface");
        var reference = QualifiedKeypadReference(surfaceSlot["presetId"], defaultReference);
        var effective = ParseJsonObject(GetComponentClassPresetConfigJson(
            connection,
            projectId,
            "surface",
            reference));
        if (surfaceSlot["overrides"] is JsonObject overrides)
        {
            MergeOverride(effective, overrides);
        }
        var surface = effective["surface"] as JsonObject
            ?? throw new InvalidOperationException($"Missing Surface config for Keypad state reference '{reference}'.");
        return (
            surface["backgroundColorToken"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Missing migrated Keypad Surface background token."),
            surface["backgroundAlpha"]?.GetValue<double>()
                ?? throw new InvalidOperationException("Missing migrated Keypad Surface background alpha."),
            surface["borderAlpha"]?.GetValue<double>()
                ?? throw new InvalidOperationException("Missing migrated Keypad Surface border alpha."));
    }

    private static void NormalizeCommonKeypadLabelSlot(JsonObject labelSlot)
    {
        var overrides = labelSlot["overrides"] as JsonObject ?? new JsonObject();
        labelSlot["overrides"] = overrides;
        var label = overrides["label"] as JsonObject ?? new JsonObject();
        overrides["label"] = label;
        label["reserveSubtextSpace"] = true;

        if (label["surfaceSlot"]?["overrides"] is not JsonObject surfaceOverrides) return;
        if (surfaceOverrides["surface"] is JsonObject surface)
        {
            surface.Remove("backgroundAlpha");
            surface.Remove("borderAlpha");
            if (surface.Count == 0) surfaceOverrides.Remove("surface");
        }
        if (surfaceOverrides["style"] is JsonObject style)
        {
            style.Remove("borderWidth");
            style.Remove("shadowEnabled");
            style.Remove("reliefEnabled");
            if (style.Count == 0) surfaceOverrides.Remove("style");
        }
        if (surfaceOverrides.Count == 0) label.Remove("surfaceSlot");
    }

    private static string QualifiedKeypadReference(JsonNode? node, string defaultReference)
    {
        var value = node?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(value) || value == DefaultComponentPresetId) return defaultReference;
        if (value.Contains("::preset::", StringComparison.Ordinal)) return value;
        var classId = defaultReference.Split("::preset::", StringSplitOptions.None)[0];
        return $"{classId}::preset::{value}";
    }

    private static string KeypadEditorLayoutJson() =>
        """
        {
          "cards": [
            {
              "id": "general",
              "label": "General",
              "subtitle": "Identity",
              "icon": "general",
              "order": 10,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "identity",
                  "label": "Identity",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "component.type", "order": 20, "visible": true },
                    { "id": "core.notes", "order": 30, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "layout",
              "label": "Layout",
              "subtitle": "Grid geometry and shared key sizing",
              "icon": "layout",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "layout",
                  "label": "Layout",
                  "icon": "layout",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "component.keypad.sizingMode", "order": 10, "visible": true },
                    { "id": "component.keypad.columns", "order": 20, "visible": true },
                    { "id": "component.keypad.keySize", "order": 30, "visible": true },
                    { "id": "component.keypad.padding", "order": 40, "visible": true },
                    { "id": "component.keypad.columnGapToken", "order": 50, "visible": true },
                    { "id": "component.keypad.rowGapToken", "order": 60, "visible": true },
                    { "id": "component.keypad.iconSizeToken", "order": 70, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "keys",
              "label": "Keys",
              "subtitle": "Ordered keypad contents",
              "icon": "keypad",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "keys",
                  "label": "Keys",
                  "icon": "keypad",
                  "order": 20,
                  "visible": true,
                  "fields": [
                    { "id": "component.keypad.keys", "order": 10, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "states",
              "label": "States",
              "subtitle": "Shared label and state color overrides",
              "icon": "variants",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groupLayout": "verticalCards",
              "groups": [
                {
                  "id": "normalState",
                  "label": "Normal",
                  "icon": "variants",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "component.keypad.label.editor", "order": 10, "visible": true },
                    { "id": "component.keypad.states.normal.backgroundColorToken", "order": 20, "visible": true },
                    { "id": "component.keypad.states.normal.textColorToken", "order": 30, "visible": true },
                    { "id": "component.keypad.states.normal.backgroundAlpha", "order": 40, "visible": true },
                    { "id": "component.keypad.states.normal.borderAlpha", "order": 50, "visible": true }
                  ]
                },
                {
                  "id": "activeState",
                  "label": "Active",
                  "icon": "variants",
                  "order": 20,
                  "visible": true,
                  "fields": [
                    { "id": "component.keypad.states.active.backgroundColorToken", "order": 10, "visible": true },
                    { "id": "component.keypad.states.active.textColorToken", "order": 20, "visible": true },
                    { "id": "component.keypad.states.active.backgroundAlpha", "order": 30, "visible": true },
                    { "id": "component.keypad.states.active.borderAlpha", "order": 40, "visible": true }
                  ]
                },
                {
                  "id": "pushedState",
                  "label": "Pushed",
                  "icon": "variants",
                  "order": 30,
                  "visible": true,
                  "fields": [
                    { "id": "component.keypad.states.pushed.backgroundColorToken", "order": 10, "visible": true },
                    { "id": "component.keypad.states.pushed.textColorToken", "order": 20, "visible": true },
                    { "id": "component.keypad.states.pushed.backgroundAlpha", "order": 30, "visible": true },
                    { "id": "component.keypad.states.pushed.borderAlpha", "order": 40, "visible": true }
                  ]
                },
                {
                  "id": "disabledState",
                  "label": "Disabled",
                  "icon": "variants",
                  "order": 40,
                  "visible": true,
                  "fields": [
                    { "id": "component.keypad.states.disabled.backgroundColorToken", "order": 10, "visible": true },
                    { "id": "component.keypad.states.disabled.textColorToken", "order": 20, "visible": true },
                    { "id": "component.keypad.states.disabled.backgroundAlpha", "order": 30, "visible": true },
                    { "id": "component.keypad.states.disabled.borderAlpha", "order": 40, "visible": true }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
