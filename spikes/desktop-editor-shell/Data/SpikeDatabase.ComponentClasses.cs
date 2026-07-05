using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public ComponentClassSettings GetComponentClassSettings(string componentClassId)
    {
        using var connection = OpenConnection();
        return GetComponentClassSettings(connection, componentClassId);
    }

    public string GetComponentClassBaseConfigsJson(string projectId)
    {
        using var connection = OpenConnection();
        var configs = new JsonObject();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT component_type, config_json
            FROM component_classes
            WHERE project_id = $projectId
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END, name
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var componentType = reader.GetString(0);
            if (configs.ContainsKey(componentType))
            {
                continue;
            }

            configs[componentType] = ParseJsonObject(ReadString(reader, 1));
        }

        return configs.ToJsonString();
    }

    private static string GetComponentClassBaseConfigJson(
        SqliteConnection connection,
        string projectId,
        string componentType)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT config_json
            FROM component_classes
            WHERE project_id = $projectId
              AND component_type = $componentType
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END, name
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$componentType", componentType);
        var configJson = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(configJson))
        {
            throw new InvalidOperationException($"Missing base component class '{componentType}' for project '{projectId}'.");
        }

        return configJson;
    }

    private IReadOnlyList<FieldOption> EmbeddedComponentOptions(string projectId, string recordClassId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM component_classes
            WHERE project_id = $projectId
              AND record_class_id = $recordClassId
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END, name
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$recordClassId", recordClassId);
        var name = command.ExecuteScalar() as string;
        return [new FieldOption(recordClassId, string.IsNullOrWhiteSpace(name) ? recordClassId : name)];
    }

    private static bool EmbeddedComponentHasOverrides(
        string configJson,
        EmbeddedComponentSlotDefinition slot)
    {
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        var overrides = EmbeddedOverrides(config, slot, createIfMissing: false);
        return overrides is not null && HasEffectiveJsonValue(overrides);
    }

    private static bool HasEffectiveJsonValue(JsonObject value)
    {
        foreach (var child in value)
        {
            if (child.Value is JsonObject childObject)
            {
                if (HasEffectiveJsonValue(childObject))
                {
                    return true;
                }

                continue;
            }

            if (child.Value is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static ComponentClassSettings GetComponentClassSettings(SqliteConnection connection, string componentClassId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json
            FROM component_classes
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", componentClassId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing component class '{componentClassId}'.");
        }

        return new ComponentClassSettings(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            ReadString(reader, 4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7));
    }

    public FieldValue CreateComponentClassFieldValue(string componentClassId, string fieldId)
    {
        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var value = fieldId == "component.type"
            ? ComponentTypeLabel(settings.ComponentType)
            : ComponentConfigFieldValue(settings.ConfigJson, descriptor);
        var options = descriptor.ValueKind == ValueKind.EmbeddedComponent
            ? EmbeddedComponentOptions(settings.ProjectId, descriptor.DefaultValue)
            : descriptor.Options;
        var isHighlighted = descriptor.ValueKind == ValueKind.EmbeddedComponent
            && EmbeddedComponentSlotCatalog.TryGet(fieldId, out var slot)
            && EmbeddedComponentHasOverrides(settings.ConfigJson, slot);

        return new FieldValue(
            new FieldDefinition(
                descriptor.Id,
                descriptor.Label,
                descriptor.ValueKind,
                descriptor.IsEditable,
                descriptor.DefaultValue,
                Options: options,
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number),
            value,
            IsHighlighted: isHighlighted);
    }

    public void UpdateComponentClassField(string componentClassId, string fieldId, string value)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0)
        {
            return;
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
            SetJsonValue(config, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson WHERE id = $id",
                ("$id", componentClassId),
                ("$configJson", config.ToJsonString()));
        }
    }

    public FieldValue CreateEmbeddedComponentFieldValue(
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId)
    {
        var slot = EmbeddedComponentSlotCatalog.Get(slotFieldId);
        if (!slot.EmbeddedComponentType.Equals(embeddedComponentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Embedded component '{embeddedComponentType}' is not supported for slot '{slotFieldId}'.");
        }

        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        using var connection = OpenConnection();
        var inheritedConfigJson = GetComponentClassBaseConfigJson(connection, settings.ProjectId, embeddedComponentType);
        var inheritedValue = ComponentConfigFieldValue(inheritedConfigJson, descriptor);
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
        var overrides = EmbeddedOverrides(config, slot, createIfMissing: false);
        var hasOverride = overrides is not null && GetJsonValue(overrides, descriptor.JsonPath) is not null;
        var localValue = hasOverride && overrides is not null
            ? ComponentConfigFieldValue(overrides.ToJsonString(), descriptor)
            : inheritedValue;

        return new FieldValue(
            new FieldDefinition(
                descriptor.Id,
                descriptor.Label,
                descriptor.ValueKind,
                descriptor.IsEditable,
                descriptor.DefaultValue,
                CanInherit: true,
                InheritedValue: inheritedValue,
                Options: descriptor.Options,
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number),
            localValue,
            IsInherited: !hasOverride);
    }

    public void UpdateEmbeddedComponentField(
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId,
        string value)
    {
        var slot = EmbeddedComponentSlotCatalog.Get(slotFieldId);
        if (!slot.EmbeddedComponentType.Equals(embeddedComponentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Embedded component '{embeddedComponentType}' is not supported for slot '{slotFieldId}'.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
            var overrides = EmbeddedOverrides(config, slot, createIfMissing: true)
                ?? throw new InvalidOperationException($"Missing embedded override slot '{slotFieldId}'.");

            if (value.Equals("inherited", StringComparison.Ordinal))
            {
                RemoveJsonValue(overrides, descriptor.JsonPath);
            }
            else
            {
                SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            }

            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson WHERE id = $id",
                ("$id", componentClassId),
                ("$configJson", config.ToJsonString()));
        }
    }

    private static void SeedComponentClassesIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM component_classes WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            foreach (var seed in ComponentSeedRows)
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
        }
    }

    private static void EnsureComponentClassConfigDefaults(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection))
        {
            var config = ParseJsonObject(string.IsNullOrWhiteSpace(row.ConfigJson) ? "{}" : row.ConfigJson);
            var defaults = ParseJsonObject(DefaultComponentClassConfigJson(row.ComponentType));
            var configChanged = NormalizeAvatarLabelPlacement(row.ComponentType, config);
            configChanged |= JsonPath.MergeMissing(config, defaults);
            configChanged |= NormalizeReliefIntensity(config, "reliefTopIntensity");
            configChanged |= NormalizeReliefIntensity(config, "reliefBottomIntensity");

            var designPreview = ParseJsonObject(string.IsNullOrWhiteSpace(row.DesignPreviewJson) ? "{}" : row.DesignPreviewJson);
            var designPreviewDefaults = ParseJsonObject(DefaultComponentDesignPreviewJson(row.ComponentType));
            var designPreviewChanged = JsonPath.MergeMissing(designPreview, designPreviewDefaults);
            designPreviewChanged |= EnsureComponentDesignPreviewText(row.ComponentType, designPreview);

            if (!configChanged && !designPreviewChanged)
            {
                continue;
            }

            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson, design_preview_json = $designPreviewJson WHERE id = $id",
                ("$id", row.Id),
                ("$configJson", config.ToJsonString()),
                ("$designPreviewJson", designPreview.ToJsonString()));
        }
    }

    private static bool NormalizeAvatarLabelPlacement(string componentType, JsonObject config)
    {
        if (componentType != "avatar")
        {
            return false;
        }

        var labelSlot = JsonPath.Get(config, ["avatar", "labelSlot"]) as JsonObject;
        if (labelSlot is null || labelSlot["placement"] is not null)
        {
            return false;
        }

        var position = JsonPath.String(labelSlot, "position", "bottom");
        var gap = JsonPath.Number(labelSlot, "gap", 4);
        labelSlot["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromLegacyPosition(position, gap).ToJsonString());
        return true;
    }

    private static bool NormalizeReliefIntensity(JsonObject config, string key)
    {
        var style = JsonPath.Get(config, ["style"]) as JsonObject;
        if (style is null)
        {
            return false;
        }

        var value = JsonPath.Number(style, key, 0);
        if (Math.Abs(value) <= 1)
        {
            return false;
        }

        style[key] = JsonValue.Create(Math.Clamp(value / 100, -1, 1));
        return true;
    }

    private static bool EnsureComponentDesignPreviewText(string componentType, JsonObject designPreview)
    {
        if (componentType != "avatar")
        {
            return false;
        }

        if (JsonPath.String(designPreview, "sampleSubtext", "").Trim().Length > 0)
        {
            return false;
        }

        designPreview["sampleSubtext"] = "Subtitle";
        return true;
    }

    private static List<ComponentClassRow> QueryComponentClassRows(SqliteConnection connection)
    {
        var rows = new List<ComponentClassRow>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json
            FROM component_classes
            ORDER BY component_type, name
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ComponentClassRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                ReadString(reader, 5),
                ReadString(reader, 6),
                ReadString(reader, 7),
                ReadString(reader, 8)));
        }

        return rows;
    }

    private static void EnsureComponentClassColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "component_classes", "notes", "TEXT NOT NULL DEFAULT ''");
    }

    private static string ComponentConfigFieldValue(string configJson, ComponentClassFieldDescriptor descriptor)
    {
        if (descriptor.ValueKind == ValueKind.EmbeddedComponent)
        {
            return descriptor.DefaultValue;
        }

        var config = ParseJsonObject(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        var node = GetJsonValue(config, descriptor.JsonPath);
        if (node is null)
        {
            return descriptor.DefaultValue;
        }

        return descriptor.ValueKind switch
        {
            ValueKind.Boolean => BoolToString(node is JsonValue value && value.TryGetValue<bool>(out var boolean) && boolean),
            ValueKind.Integer => JsonNumberString(config, descriptor.JsonPath, descriptor.DefaultValue),
            ValueKind.Decimal => JsonNumberString(config, descriptor.JsonPath, descriptor.DefaultValue),
            ValueKind.Alpha => JsonNumberString(config, descriptor.JsonPath, descriptor.DefaultValue),
            ValueKind.IntegerPair => node is JsonValue pairValue && pairValue.TryGetValue<string>(out var pairText)
                ? pairText
                : descriptor.DefaultValue,
            ValueKind.AlignmentPlacement => node is JsonObject
                ? node.ToJsonString()
                : descriptor.DefaultValue,
            ValueKind.IconSlots => node.ToJsonString(),
            _ => node is JsonValue stringValue && stringValue.TryGetValue<string>(out var text)
                ? text
                : node.ToJsonString().Trim('"'),
        };
    }

    private static JsonNode ComponentConfigJsonValue(ValueKind valueKind, string value)
    {
        return valueKind switch
        {
            ValueKind.Boolean => JsonValue.Create(StringToBool(value))!,
            ValueKind.Integer => NumberNode(value),
            ValueKind.Decimal => NumberNode(value),
            ValueKind.Alpha => NumberNode(value),
            ValueKind.AlignmentPlacement => JsonNode.Parse(value)
                ?? throw new InvalidOperationException("Alignment placement value must be valid JSON."),
            ValueKind.IconSlots => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? ComponentClassFieldCatalog.EmptyIconSlots : value)
                ?? JsonNode.Parse(ComponentClassFieldCatalog.EmptyIconSlots)!,
            _ => JsonValue.Create(value)!,
        };
    }

    private static JsonObject? EmbeddedOverrides(JsonObject config, EmbeddedComponentSlotDefinition slot, bool createIfMissing)
    {
        var slotNode = JsonPath.Get(config, slot.SlotPath) as JsonObject;
        if (slotNode is null)
        {
            if (!createIfMissing) return null;

            slotNode = [];
            JsonPath.Set(config, slot.SlotPath, slotNode);
        }

        if (slotNode["overrides"] is JsonObject overrides)
        {
            return overrides;
        }

        if (!createIfMissing) return null;

        overrides = [];
        slotNode["overrides"] = overrides;
        return overrides;
    }

    private static string ComponentTypeLabel(string componentType)
    {
        return componentType switch
        {
            "avatar" => "Avatar component",
            "textInputBar" => "Text input bar component",
            "keyboard" => "Keyboard component",
            "buttonIcon" => "Button icon component",
            "label" => "Label component",
            "audio" => "Audio component",
            "video" => "Video component",
            _ => componentType,
        };
    }

    private static ComponentSeedRow NewComponentSeed(string componentType, string recordClassId, string name)
    {
        return new ComponentSeedRow(
            componentType,
            recordClassId,
            name,
            DefaultComponentClassConfigJson(componentType),
            DefaultComponentDesignPreviewJson(componentType),
            JsonSerializer.Serialize(new { note = "Seeded reusable component class." }));
    }

    private static string DefaultComponentClassConfigJson(string componentType)
    {
        var config = new JsonObject
        {
            ["style"] = new JsonObject
            {
                ["shadowEnabled"] = false,
                ["reliefEnabled"] = false,
                ["borderWidth"] = 0,
                ["borderColorToken"] = "theme.borders.primary",
                ["cornerRadiusToken"] = componentType == "avatar" ? "theme.radii.avatar" : "theme.radii.surface",
                ["reliefAngle"] = -45,
                ["reliefExtent"] = 1,
                ["reliefSpread"] = 0,
                ["reliefTopIntensity"] = 0.12,
                ["reliefBottomIntensity"] = -0.1,
            },
        };

        switch (componentType)
        {
            case "avatar":
                config["avatar"] = new JsonObject
                {
                    ["defaultSize"] = 48,
                    ["cornerRadiusToken"] = "theme.radii.avatar",
                    ["labelSlot"] = new JsonObject
                    {
                        ["showLabel"] = false,
                        ["showSubtext"] = false,
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.Default.ToJsonString()),
                        ["overrides"] = new JsonObject(),
                    },
                };
                break;
            case "textInputBar":
                config["textInput"] = new JsonObject
                {
                    ["height"] = 44,
                    ["placeholder"] = "Message",
                    ["idleTextColorToken"] = "theme.colors.textSecondary",
                    ["cursorColorToken"] = "theme.cursor.color",
                    ["cursorWidth"] = 2,
                    ["cursorBlinkFrames"] = 18,
                };
                break;
            case "keyboard":
                config["keyboard"] = new JsonObject
                {
                    ["keyPadding"] = 4,
                    ["keyCornerRadius"] = 6,
                    ["keyShadowEnabled"] = false,
                    ["pressedEffect"] = "popup",
                    ["specialKeyTextScale"] = "0.65",
                    ["emojiScale"] = "1.2",
                    ["bottomIconSlots"] = JsonNode.Parse(ComponentClassFieldCatalog.EmptyIconSlots),
                };
                break;
            case "buttonIcon":
                config["buttonIcon"] = new JsonObject
                {
                    ["iconPadding"] = 6,
                    ["labelEnabled"] = false,
                    ["labelPosition"] = "bottom",
                    ["labelSize"] = 10,
                    ["labelPadding"] = 3,
                };
                break;
            case "label":
                config["label"] = new JsonObject
                {
                    ["dimensionMode"] = "content",
                    ["size"] = "120|32",
                    ["padding"] = "8|4",
                    ["backgroundColorToken"] = "theme.colors.background",
                    ["alpha"] = 1,
                    ["textColorToken"] = "theme.colors.textPrimary",
                    ["textSizeToken"] = "theme.typography.sizes.s",
                    ["textStyle"] = "normal",
                    ["textAlign"] = "center",
                    ["textGap"] = 2,
                    ["subtextColorToken"] = "theme.colors.textSecondary",
                    ["subtextSizeToken"] = "theme.typography.sizes.xs",
                    ["subtextStyle"] = "normal",
                };
                break;
            case "audio":
                config["audio"] = new JsonObject
                {
                    ["size"] = "230|54",
                    ["avatarPosition"] = "right",
                    ["avatarSize"] = 32,
                    ["textSize"] = 13,
                    ["playColorToken"] = "theme.icons.accent",
                    ["waveformColorToken"] = "theme.icons.primary",
                    ["knobSize"] = 10,
                };
                break;
            case "video":
                config["video"] = new JsonObject
                {
                    ["statusVisible"] = true,
                    ["statusHeight"] = 24,
                    ["statusIconSlots"] = JsonNode.Parse("""{"left":["app_camera"],"center":[],"right":[]}"""),
                    ["playOverlayVisible"] = true,
                    ["playColorToken"] = "theme.icons.accent",
                };
                break;
        }

        return config.ToJsonString();
    }

    private static string DefaultComponentDesignPreviewJson(string componentType)
    {
        return JsonSerializer.Serialize(new
        {
            componentType,
            sampleText = componentType switch
            {
                "label" => "Alex",
                "textInputBar" => "Message",
                "audio" => "0:05",
                "video" => "0:12",
                _ => "Sample",
            },
            sampleSubtext = componentType is "label" or "avatar" ? "Subtitle" : "",
            sampleSize = 256,
        });
    }

    private static readonly ComponentSeedRow[] ComponentSeedRows =
    [
        NewComponentSeed("avatar", "component.avatar", "Default Avatar"),
        NewComponentSeed("textInputBar", "component.text_input_bar", "Default Text Input Bar"),
        NewComponentSeed("keyboard", "component.keyboard", "Default Keyboard"),
        NewComponentSeed("buttonIcon", "component.button_icon", "Default Button Icon"),
        NewComponentSeed("label", "component.label", "Default Label"),
        NewComponentSeed("audio", "component.audio", "Default Audio"),
        NewComponentSeed("video", "component.video", "Default Video"),
    ];
}
