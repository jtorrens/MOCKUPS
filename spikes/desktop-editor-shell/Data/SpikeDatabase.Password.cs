using Microsoft.Data.Sqlite;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void EnsurePasswordComponentClasses(SqliteConnection connection)
    {
        EnsurePasswordOwnedComponentClass(
            connection,
            "fingerprint",
            NormalizeFingerprintConfig,
            FingerprintEditorLayoutJson(),
            _ => { });
        EnsurePasswordOwnedComponentClass(
            connection,
            "faceRecognition",
            NormalizeFaceRecognitionConfig,
            FaceRecognitionEditorLayoutJson(),
            _ => { });
        EnsurePasswordOwnedComponentClass(
            connection,
            "drawPassword",
            (_, _) => { },
            DrawPasswordEditorLayoutJson(),
            _ => { });
        EnsurePasswordOwnedComponentClass(
            connection,
            "codeIndicator",
            NormalizeCodeIndicatorConfig,
            CodeIndicatorEditorLayoutJson(),
            _ => { });
        EnsurePasswordOwnedComponentClass(
            connection,
            "password",
            NormalizePasswordConfig,
            PasswordEditorLayoutJson(),
            NormalizePasswordPreview);
        foreach (var projectId in QueryProjectRows(connection).Select((project) => project.Id))
        {
            EnsurePasswordVariants(connection, projectId);
        }
    }

    private static void EnsurePasswordOwnedComponentClass(
        SqliteConnection connection,
        string componentType,
        Action<JsonObject, string> normalizeConfig,
        string layoutJson,
        Action<JsonObject> normalizePreview)
    {
        var seed = ComponentSeedRows.Single((candidate) => candidate.ComponentType == componentType);
        foreach (var projectId in QueryProjectRows(connection).Select((project) => project.Id))
        {
            var existing = QueryComponentClassRows(connection).FirstOrDefault((candidate) =>
                candidate.ProjectId == projectId && candidate.ComponentType == componentType);
            if (existing is null)
            {
                var config = ParseJsonObject(seed.ConfigJson);
                normalizeConfig(config, projectId);
                var metadata = ParseJsonObject(seed.MetadataJson);
                var preview = ParseJsonObject(seed.DesignPreviewJson);
                normalizePreview(preview);
                SetDefaultComponentPresetConfig(metadata, config);
                Execute(
                    connection,
                    """
                    INSERT INTO component_classes (id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json)
                    VALUES ($id, $projectId, $componentType, $recordClassId, $name, $notes, $configJson, $designPreviewJson, $metadataJson)
                    """,
                    ("$id", $"component_{projectId}_{componentType}"),
                    ("$projectId", projectId),
                    ("$componentType", componentType),
                    ("$recordClassId", seed.RecordClassId),
                    ("$name", seed.Name),
                    ("$notes", ComponentTypeLabel(componentType)),
                    ("$configJson", config.ToJsonString()),
                    ("$designPreviewJson", preview.ToJsonString()),
                    ("$metadataJson", metadata.ToJsonString()));
                continue;
            }

            var configJson = ParseJsonObject(existing.ConfigJson);
            var metadataJson = ParseJsonObject(existing.MetadataJson);
            var previewJson = ParseJsonObject(existing.DesignPreviewJson);
            normalizeConfig(configJson, projectId);
            normalizePreview(previewJson);
            if (metadataJson["presets"] is JsonArray presets)
            {
                foreach (var preset in presets.OfType<JsonObject>())
                {
                    if (preset["config"] is JsonObject presetConfig)
                    {
                        normalizeConfig(presetConfig, projectId);
                    }
                }
            }
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson, design_preview_json = $designPreviewJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", existing.Id),
                ("$configJson", configJson.ToJsonString()),
                ("$designPreviewJson", previewJson.ToJsonString()),
                ("$metadataJson", metadataJson.ToJsonString()));
        }

        Execute(
            connection,
            "INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json) VALUES ($recordClassId, $layoutJson)",
            ("$recordClassId", $"component.{componentType}"),
            ("$layoutJson", layoutJson));
    }

    private static void NormalizeCodeIndicatorConfig(JsonObject config, string projectId)
    {
        var indicator = config["codeIndicator"] as JsonObject
            ?? throw new InvalidOperationException("Missing Code Indicator config.");
        indicator["displayMode"] ??= "visible";
        var states = indicator["states"] as JsonObject
            ?? throw new InvalidOperationException("Missing Code Indicator states.");
        foreach (var stateId in new[] { "initial", "correct", "incorrect" })
        {
            var state = states[stateId] as JsonObject
                ?? throw new InvalidOperationException($"Missing Code Indicator state '{stateId}'.");
            QualifyOwnedSlot(state, "emptySurfaceSlot", projectId, "surface");
            QualifyOwnedSlot(state, "filledSurfaceSlot", projectId, "surface");
        }
    }

    private static void NormalizeFingerprintConfig(JsonObject config, string projectId)
    {
        var fingerprint = config["fingerprint"] as JsonObject
            ?? throw new InvalidOperationException("Missing Fingerprint config.");
        fingerprint["iconSizeMultiplier"] ??= 1;
    }

    private static void NormalizeFaceRecognitionConfig(JsonObject config, string projectId)
    {
        var faceRecognition = config["faceRecognition"] as JsonObject
            ?? throw new InvalidOperationException("Missing Face Recognition config.");
        faceRecognition["iconSizeMultiplier"] ??= 1;
    }

    private static void NormalizePasswordConfig(JsonObject config, string projectId)
    {
        var password = config["password"] as JsonObject
            ?? throw new InvalidOperationException("Missing Password config.");
        password.Remove("labelGapToken");
        password.Remove("indicatorGapToken");
        password.Remove("keypadGapToken");
        password["mode"] ??= "pin";
        if (password["upperAnchor"]?.GetValue<string>() == "keypad") password["upperAnchor"] = "input";
        if (password["lowerAnchor"]?.GetValue<string>() == "keypad") password["lowerAnchor"] = "input";
        password["upperAnchor"] ??= "container";
        password["lowerAnchor"] ??= "container";
        password["labelIndicatorGapToken"] ??= "theme.spacing.l";
        password["startGapToken"] ??= "theme.spacing.xl";
        password["upperGapToken"] ??= "theme.spacing.xl";
        password["lowerGapToken"] ??= "theme.spacing.l";
        password["endGapToken"] ??= "theme.spacing.l";
        password["fingerprintSlot"] ??= ComponentSurfaceSlot(SeededComponentPresetReference(projectId, "fingerprint"));
        password["faceRecognitionSlot"] ??= ComponentSurfaceSlot(SeededComponentPresetReference(projectId, "faceRecognition"));
        password["drawPasswordSlot"] ??= ComponentSurfaceSlot(SeededComponentPresetReference(projectId, "drawPassword"));
        QualifyOwnedSlot(password, "initialLabelSlot", projectId, "label");
        QualifyOwnedSlot(password, "correctLabelSlot", projectId, "label");
        QualifyOwnedSlot(password, "incorrectLabelSlot", projectId, "label");
        QualifyOwnedSlot(password, "indicatorSlot", projectId, "codeIndicator");
        QualifyOwnedSlot(password, "keypadSlot", projectId, "keypad");
        QualifyOwnedSlot(password, "fingerprintSlot", projectId, "fingerprint");
        QualifyOwnedSlot(password, "faceRecognitionSlot", projectId, "faceRecognition");
        QualifyOwnedSlot(password, "drawPasswordSlot", projectId, "drawPassword");
        QualifyOwnedSlot(password, "iconBarSlot", projectId, "iconBar");
    }

    private static void EnsurePasswordVariants(SqliteConnection connection, string projectId)
    {
        EnsureSeededComponentVariant(connection, projectId, "codeIndicator", "empty", "Empty", (config) =>
        {
            config["codeIndicator"]!["displayMode"] = "collapsed";
        });
        EnsurePasswordModeVariant(connection, projectId, "fingerprint", "Fingerprint");
        EnsurePasswordModeVariant(connection, projectId, "faceRecognition", "Face Recognition");
        EnsurePasswordModeVariant(connection, projectId, "drawPassword", "Draw Password");
    }

    private static void EnsurePasswordModeVariant(
        SqliteConnection connection,
        string projectId,
        string mode,
        string name)
    {
        EnsureSeededComponentVariant(connection, projectId, "password", mode, name, (config) =>
        {
            var password = config["password"] as JsonObject
                ?? throw new InvalidOperationException("Missing Password config.");
            password["mode"] = mode;
            if (password["indicatorSlot"] is JsonObject indicatorSlot)
            {
                indicatorSlot["presetId"] = $"component_{projectId}_codeIndicator::preset::empty";
            }
        });
    }

    private static void NormalizePasswordPreview(JsonObject preview)
    {
        preview.Remove("availableWidth");
        if (preview["testValues"] is JsonObject testValues)
        {
            testValues.Remove("availableWidth");
        }
        if (preview["inputs"] is JsonArray inputs)
        {
            for (var index = inputs.Count - 1; index >= 0; index--)
            {
                if (inputs[index] is JsonObject input
                    && input["id"]?.GetValue<string>() == "availableWidth")
                {
                    inputs.RemoveAt(index);
                }
            }
        }
        if (preview["actions"] is JsonArray actions)
        {
            foreach (var action in actions.OfType<JsonObject>())
            {
                if (action["id"]?.GetValue<string>() == "enterPassword")
                {
                    action["completionBehavior"] = "holdFinal";
                }
            }
        }
    }

    private static void QualifyOwnedSlot(
        JsonObject owner,
        string slotKey,
        string projectId,
        string componentType)
    {
        var slot = owner[slotKey] as JsonObject
            ?? throw new InvalidOperationException($"Missing embedded component slot '{slotKey}'.");
        var defaultReference = SeededComponentPresetReference(projectId, componentType);
        var current = slot["presetId"]?.GetValue<string>() ?? "";
        slot["presetId"] = string.IsNullOrWhiteSpace(current) || current == DefaultComponentPresetId
            ? defaultReference
            : current.Contains("::preset::", StringComparison.Ordinal)
                ? current
                : $"component_{projectId}_{componentType}::preset::{current}";
        slot["overrides"] ??= new JsonObject();
    }

    private static string CodeIndicatorEditorLayoutJson() =>
        """
        {
          "cards": [
            { "id": "general", "label": "General", "subtitle": "Identity", "icon": "general", "order": 10, "visible": true, "defaultOpen": false, "groups": [
              { "id": "identity", "label": "Identity", "order": 10, "visible": true, "fields": [
                { "id": "core.name", "order": 10, "visible": true },
                { "id": "component.type", "order": 20, "visible": true },
                { "id": "core.notes", "order": 30, "visible": true }
              ] }
            ] },
            { "id": "layout", "label": "Layout", "subtitle": "Glyph geometry", "icon": "layout", "order": 20, "visible": true, "defaultOpen": false, "groups": [
              { "id": "layout", "label": "Layout", "order": 10, "visible": true, "fields": [
                { "id": "component.codeIndicator.displayMode", "order": 10, "visible": true },
                { "id": "component.codeIndicator.glyphSize", "order": 20, "visible": true },
                { "id": "component.codeIndicator.gapToken", "order": 30, "visible": true }
              ] }
            ] },
            { "id": "states", "label": "States", "subtitle": "Empty and filled glyph surfaces", "icon": "variants", "order": 30, "visible": true, "defaultOpen": false, "groupLayout": "verticalCards", "groups": [
              { "id": "initial", "label": "Initial", "order": 10, "visible": true, "fields": [
                { "id": "component.codeIndicator.states.initial.empty.editor", "order": 10, "visible": true },
                { "id": "component.codeIndicator.states.initial.filled.editor", "order": 20, "visible": true }
              ] },
              { "id": "correct", "label": "Correct", "order": 20, "visible": true, "fields": [
                { "id": "component.codeIndicator.states.correct.empty.editor", "order": 10, "visible": true },
                { "id": "component.codeIndicator.states.correct.filled.editor", "order": 20, "visible": true }
              ] },
              { "id": "incorrect", "label": "Incorrect", "order": 30, "visible": true, "fields": [
                { "id": "component.codeIndicator.states.incorrect.empty.editor", "order": 10, "visible": true },
                { "id": "component.codeIndicator.states.incorrect.filled.editor", "order": 20, "visible": true }
              ] }
            ] }
          ]
        }
        """;

    private static string FingerprintEditorLayoutJson() => RecognitionEditorLayoutJson(
        "Fingerprint", "fingerprint", "scanLineThickness");

    private static string FaceRecognitionEditorLayoutJson() => RecognitionEditorLayoutJson(
        "Face Recognition", "faceRecognition", "strokeWidth");

    private static string RecognitionEditorLayoutJson(string title, string prefix, string strokeField) => $$"""
        {
          "cards": [
            { "id": "general", "label": "General", "subtitle": "Identity", "icon": "general", "order": 10, "visible": true, "defaultOpen": false, "groups": [
              { "id": "identity", "label": "Identity", "order": 10, "visible": true, "fields": [
                { "id": "core.name", "order": 10, "visible": true },
                { "id": "component.type", "order": 20, "visible": true },
                { "id": "core.notes", "order": 30, "visible": true }
              ] }
            ] },
            { "id": "layout", "label": "Layout", "subtitle": "{{title}} geometry", "icon": "layout", "order": 20, "visible": true, "defaultOpen": false, "groups": [
              { "id": "layout", "label": "Layout", "order": 10, "visible": true, "fields": [
                { "id": "component.{{prefix}}.size", "order": 10, "visible": true },
                { "id": "component.{{prefix}}.iconToken", "order": 20, "visible": true },
                { "id": "component.{{prefix}}.iconSizeToken", "order": 30, "visible": true },
                { "id": "component.{{prefix}}.iconSizeMultiplier", "order": 40, "visible": true },
                { "id": "component.{{prefix}}.{{strokeField}}", "order": 50, "visible": true }
              ] }
            ] },
            { "id": "states", "label": "States", "subtitle": "Resolved colors", "icon": "variants", "order": 30, "visible": true, "defaultOpen": false, "groups": [
              { "id": "states", "label": "States", "order": 10, "visible": true, "fields": [
                { "id": "component.{{prefix}}.states.initial.colorToken", "order": 10, "visible": true },
                { "id": "component.{{prefix}}.states.active.colorToken", "order": 20, "visible": true },
                { "id": "component.{{prefix}}.states.correct.colorToken", "order": 30, "visible": true },
                { "id": "component.{{prefix}}.states.incorrect.colorToken", "order": 40, "visible": true }
              ] }
            ] }
          ]
        }
        """;

    private static string DrawPasswordEditorLayoutJson() =>
        """
        {
          "cards": [
            { "id": "general", "label": "General", "subtitle": "Identity", "icon": "general", "order": 10, "visible": true, "defaultOpen": false, "groups": [
              { "id": "identity", "label": "Identity", "order": 10, "visible": true, "fields": [
                { "id": "core.name", "order": 10, "visible": true },
                { "id": "component.type", "order": 20, "visible": true },
                { "id": "core.notes", "order": 30, "visible": true }
              ] }
            ] },
            { "id": "layout", "label": "Layout", "subtitle": "Pattern grid", "icon": "layout", "order": 20, "visible": true, "defaultOpen": false, "groups": [
              { "id": "layout", "label": "Layout", "order": 10, "visible": true, "fields": [
                { "id": "component.drawPassword.grid", "order": 10, "visible": true },
                { "id": "component.drawPassword.nodeSize", "order": 20, "visible": true },
                { "id": "component.drawPassword.columnGapToken", "order": 30, "visible": true },
                { "id": "component.drawPassword.rowGapToken", "order": 40, "visible": true },
                { "id": "component.drawPassword.lineWidth", "order": 50, "visible": true }
              ] }
            ] },
            { "id": "states", "label": "States", "subtitle": "Node and line colors", "icon": "variants", "order": 30, "visible": true, "defaultOpen": false, "groupLayout": "verticalCards", "groups": [
              { "id": "initial", "label": "Initial", "order": 10, "visible": true, "fields": [ { "id": "component.drawPassword.states.initial.nodeColorToken", "order": 10, "visible": true }, { "id": "component.drawPassword.states.initial.lineColorToken", "order": 20, "visible": true } ] },
              { "id": "active", "label": "Active", "order": 20, "visible": true, "fields": [ { "id": "component.drawPassword.states.active.nodeColorToken", "order": 10, "visible": true }, { "id": "component.drawPassword.states.active.lineColorToken", "order": 20, "visible": true } ] },
              { "id": "correct", "label": "Correct", "order": 30, "visible": true, "fields": [ { "id": "component.drawPassword.states.correct.nodeColorToken", "order": 10, "visible": true }, { "id": "component.drawPassword.states.correct.lineColorToken", "order": 20, "visible": true } ] },
              { "id": "incorrect", "label": "Incorrect", "order": 40, "visible": true, "fields": [ { "id": "component.drawPassword.states.incorrect.nodeColorToken", "order": 10, "visible": true }, { "id": "component.drawPassword.states.incorrect.lineColorToken", "order": 20, "visible": true } ] }
            ] }
          ]
        }
        """;

    private static string PasswordEditorLayoutJson() =>
        """
        {
          "cards": [
            { "id": "general", "label": "General", "subtitle": "Identity", "icon": "general", "order": 10, "visible": true, "defaultOpen": false, "groups": [
              { "id": "identity", "label": "Identity", "order": 10, "visible": true, "fields": [
                { "id": "core.name", "order": 10, "visible": true },
                { "id": "component.type", "order": 20, "visible": true },
                { "id": "core.notes", "order": 30, "visible": true }
              ] }
            ] },
            { "id": "layout", "label": "Layout", "subtitle": "Centered keypad and anchored blocks", "icon": "layout", "order": 20, "visible": true, "defaultOpen": false, "groups": [
              { "id": "layout", "label": "Layout", "order": 10, "visible": true, "fields": [
                { "id": "component.password.mode", "order": 10, "visible": true },
                { "id": "component.password.upperAnchor", "order": 20, "visible": true },
                { "id": "component.password.lowerAnchor", "order": 30, "visible": true },
                { "id": "component.password.labelIndicatorGapToken", "order": 40, "visible": true },
                { "id": "component.password.startGapToken", "order": 50, "visible": true },
                { "id": "component.password.upperGapToken", "order": 60, "visible": true },
                { "id": "component.password.lowerGapToken", "order": 70, "visible": true },
                { "id": "component.password.endGapToken", "order": 80, "visible": true },
                { "id": "component.password.iconBarHeight", "order": 90, "visible": true }
              ] }
            ] },
            { "id": "labels", "label": "Labels", "subtitle": "Text and variant for each result state", "icon": "label", "order": 30, "visible": true, "defaultOpen": false, "groupLayout": "verticalCards", "groups": [
              { "id": "initial", "label": "Initial", "order": 10, "visible": true, "fields": [
                { "id": "component.password.initialLabel.editor", "order": 10, "visible": true }
              ] },
              { "id": "correct", "label": "Correct", "order": 20, "visible": true, "fields": [
                { "id": "component.password.correctLabel.editor", "order": 10, "visible": true }
              ] },
              { "id": "incorrect", "label": "Incorrect", "order": 30, "visible": true, "fields": [
                { "id": "component.password.incorrectLabel.editor", "order": 10, "visible": true }
              ] }
            ] },
            { "id": "indicator", "label": "Code Indicator", "subtitle": "Password glyph block", "icon": "component", "order": 40, "visible": true, "defaultOpen": false, "groups": [
              { "id": "indicator", "label": "Code Indicator", "order": 10, "visible": true, "fields": [
                { "id": "component.password.indicator.editor", "order": 10, "visible": true }
              ] }
            ] },
            { "id": "modes", "label": "Modes", "subtitle": "Input component for each authentication mode", "icon": "keypad", "order": 50, "visible": true, "defaultOpen": false, "groupLayout": "verticalCards", "groups": [
              { "id": "keypad", "label": "Keypad", "order": 10, "visible": true, "fields": [
                { "id": "component.password.keypad.editor", "order": 10, "visible": true }
              ] },
              { "id": "fingerprint", "label": "Fingerprint", "order": 20, "visible": true, "fields": [
                { "id": "component.password.fingerprint.editor", "order": 10, "visible": true }
              ] },
              { "id": "faceRecognition", "label": "Face Recognition", "order": 30, "visible": true, "fields": [
                { "id": "component.password.faceRecognition.editor", "order": 10, "visible": true }
              ] },
              { "id": "drawPassword", "label": "Draw Password", "order": 40, "visible": true, "fields": [
                { "id": "component.password.drawPassword.editor", "order": 10, "visible": true }
              ] }
            ] },
            { "id": "iconBar", "label": "Icon Bar", "subtitle": "Optional actions", "icon": "component", "order": 60, "visible": true, "defaultOpen": false, "groups": [
              { "id": "iconBar", "label": "Icon Bar", "order": 10, "visible": true, "fields": [
                { "id": "component.password.iconBar.editor", "order": 10, "visible": true }
              ] }
            ] }
          ]
        }
        """;
}
