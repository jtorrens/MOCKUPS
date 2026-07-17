using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public ComponentClassSettings GetComponentClassSettings(string componentClassId)
    {
        using var connection = OpenConnection();
        return GetComponentClassSettings(connection, componentClassId);
    }

    public ComponentClassSettings GetComponentPresetSettings(ProjectTreeNode presetNode)
    {
        using var connection = OpenConnection();
        return GetComponentPresetSettings(connection, presetNode);
    }

    public void UpdateComponentClassDesignPreviewJson(string componentClassId, string designPreviewJson)
    {
        ParseJsonObject(designPreviewJson);
        using var connection = OpenConnection();
        Execute(
            connection,
            "UPDATE component_classes SET design_preview_json = $json WHERE id = $id",
            ("$json", designPreviewJson),
            ("$id", componentClassId));
    }

    private static ComponentClassSettings GetComponentPresetSettings(SqliteConnection connection, ProjectTreeNode presetNode)
    {
        if (presetNode.Kind != ProjectTreeNodeKind.ComponentPreset
            || !TryParseComponentPresetNodeId(presetNode.Id, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{presetNode.Id}'.");
        }

        var settings = GetComponentClassSettings(connection, componentClassId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        var presets = VariantEnvelopeContract.RequiredArray(
            metadata,
            "presets",
            $"Component class '{componentClassId}'");

        var preset = FindPreset(presets, presetId)
            ?? throw new InvalidOperationException($"Missing component variant '{presetId}'.");
        if (preset["config"] is not JsonObject configObject)
        {
            throw new InvalidOperationException($"Component variant '{presetId}' has no config.");
        }

        var config = configObject.ToJsonString();
        var presetName = JsonPath.String(preset, "name", presetId);

        return settings with
        {
            Name = string.IsNullOrWhiteSpace(presetName)
                ? settings.Name
                : $"{settings.Name} · {presetName}",
            ConfigJson = config,
        };
    }

    private static JsonObject ComponentPresetConfigForUpdate(
        SqliteConnection connection,
        ProjectTreeNode presetNode,
        out string componentClassId,
        out JsonObject metadata)
    {
        if (presetNode.Kind != ProjectTreeNodeKind.ComponentPreset
            || !TryParseComponentPresetNodeId(presetNode.Id, out componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{presetNode.Id}'.");
        }

        var settings = GetComponentClassSettings(connection, componentClassId);
        metadata = ParseJsonObject(settings.MetadataJson);
        var presets = VariantEnvelopeContract.RequiredArray(
            metadata,
            "presets",
            $"Component class '{componentClassId}'");

        var preset = FindPreset(presets, presetId)
            ?? throw new InvalidOperationException($"Missing component variant '{presetId}'.");
        if (JsonBool(preset, ["locked"]))
        {
            throw new InvalidOperationException($"Component variant '{presetId}' is locked.");
        }

        if (preset["config"] is not JsonObject config)
        {
            throw new InvalidOperationException($"Component variant '{presetId}' has no config.");
        }

        return config;
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

        var metadataJson = ReadString(reader, 7);
        return new ComponentClassSettings(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            ReadString(reader, 4),
            DefaultComponentPresetConfigJson(metadataJson, $"Component class '{componentClassId}'"),
            ReadString(reader, 6),
            metadataJson);
    }

    public FieldValue CreateComponentClassFieldValue(string componentClassId, string fieldId)
    {
        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var value = fieldId == "component.type"
            ? ComponentTypeLabel(settings.ComponentType)
            : ComponentConfigFieldValue(settings.ConfigJson, descriptor);
        var options = ComponentClassFieldOptions(settings.ProjectId, descriptor);
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentPreset
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
                Number: descriptor.Number,
                ComponentInputBindings: descriptor.ComponentInputBindings,
                StructuredCollection: descriptor.StructuredCollection,
                RuntimeInputComponentPresetFieldId: descriptor.RuntimeInputComponentPresetFieldId,
                Unit: descriptor.Unit),
            value,
            IsHighlighted: isHighlighted);
    }

    public FieldValue CreateComponentPresetFieldValue(ProjectTreeNode presetNode, string fieldId)
    {
        var settings = GetComponentPresetSettings(presetNode);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var value = fieldId == "component.type"
            ? ComponentTypeLabel(settings.ComponentType)
            : ComponentConfigFieldValue(settings.ConfigJson, descriptor);
        var options = ComponentClassFieldOptions(settings.ProjectId, descriptor);
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentPreset
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
                Number: descriptor.Number,
                ComponentInputBindings: descriptor.ComponentInputBindings,
                StructuredCollection: descriptor.StructuredCollection,
                RuntimeInputComponentPresetFieldId: descriptor.RuntimeInputComponentPresetFieldId,
                Unit: descriptor.Unit),
            value,
            IsHighlighted: isHighlighted);
    }

    public FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        string fieldId)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var inheritedValue = ComponentConfigFieldValue(baseConfigJson, descriptor);
        var hasOverride = GetJsonValue(overrides, descriptor.JsonPath) is not null;
        var localValue = hasOverride
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
                Options: ComponentClassFieldOptions(projectId, descriptor),
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number,
                ComponentInputBindings: descriptor.ComponentInputBindings,
                StructuredCollection: descriptor.StructuredCollection,
                RuntimeInputComponentPresetFieldId: descriptor.RuntimeInputComponentPresetFieldId,
                Unit: descriptor.Unit),
            localValue,
            IsInherited: !hasOverride);
    }

    public FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId)
    {
        if (slots.Count == 0)
        {
            return CreateRuntimeComponentOverrideFieldValue(projectId, baseConfigJson, overrides, fieldId);
        }

        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        using var connection = OpenConnection();
        var effectiveOwnerConfig = ParseJsonObject(baseConfigJson);
        MergeOverride(effectiveOwnerConfig, overrides);
        var inheritedConfig = EffectiveEmbeddedBaseConfig(connection, projectId, effectiveOwnerConfig, slots);
        var inheritedValue = ComponentConfigFieldValue(inheritedConfig.ToJsonString(), descriptor);
        var localOverrides = EmbeddedOverrides(overrides, slots, createIfMissing: false);
        var hasOverride = localOverrides is not null && GetJsonValue(localOverrides, descriptor.JsonPath) is not null;
        var localValue = hasOverride && localOverrides is not null
            ? ComponentConfigFieldValue(localOverrides.ToJsonString(), descriptor)
            : inheritedValue;
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentPreset
            && EmbeddedComponentSlotCatalog.TryGet(fieldId, out var nestedSlot)
            && EmbeddedComponentHasOverrides(overrides, [.. slots, nestedSlot]);
        return new FieldValue(
            new FieldDefinition(
                descriptor.Id,
                descriptor.Label,
                descriptor.ValueKind,
                descriptor.IsEditable,
                descriptor.DefaultValue,
                CanInherit: true,
                InheritedValue: inheritedValue,
                Options: ComponentClassFieldOptions(projectId, descriptor),
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number,
                ComponentInputBindings: descriptor.ComponentInputBindings,
                StructuredCollection: descriptor.StructuredCollection,
                RuntimeInputComponentPresetFieldId: descriptor.RuntimeInputComponentPresetFieldId,
                Unit: descriptor.Unit),
            localValue,
            IsInherited: !hasOverride,
            IsHighlighted: isHighlighted);
    }

    public void UpdateRuntimeComponentOverride(
        JsonObject overrides,
        string fieldId,
        string value)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0) return;
        if (value == "inherited")
        {
            RemoveJsonValue(overrides, descriptor.JsonPath);
            return;
        }
        SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
    }

    public void UpdateRuntimeComponentOverride(
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value)
    {
        if (slots.Count == 0)
        {
            UpdateRuntimeComponentOverride(overrides, fieldId, value);
            return;
        }

        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0) return;
        var localOverrides = EmbeddedOverrides(overrides, slots, createIfMissing: true)
            ?? throw new InvalidOperationException($"Missing runtime component override slot '{slots[^1].FieldId}'.");
        if (value.Equals("inherited", StringComparison.Ordinal)
            || descriptor.ValueKind == ValueKind.TypographyStyle && TypographyStyleValue.IsEmpty(value))
        {
            RemoveJsonValue(localOverrides, descriptor.JsonPath);
            return;
        }
        SetJsonValue(localOverrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
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
            var config = ParseJsonObject(settings.ConfigJson);
            var metadata = ParseJsonObject(settings.MetadataJson);
            SetJsonValue(config, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            SetDefaultComponentPresetConfig(metadata, config);
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", componentClassId),
                ("$configJson", config.ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()));
        }
    }

    public void UpdateComponentPresetField(ProjectTreeNode presetNode, string fieldId, string value)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0)
        {
            return;
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var config = ComponentPresetConfigForUpdate(connection, presetNode, out var componentClassId, out var metadata);
            SetJsonValue(config, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            PersistComponentPresetUpdate(connection, presetNode, componentClassId, config, metadata);
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
        var config = ParseJsonObject(settings.ConfigJson);
        var inheritedConfigJson = EffectiveEmbeddedBaseConfig(connection, settings.ProjectId, config, [slot]).ToJsonString();
        var inheritedValue = ComponentConfigFieldValue(inheritedConfigJson, descriptor);
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
                Options: ComponentClassFieldOptions(settings.ProjectId, descriptor),
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number,
                ComponentInputBindings: descriptor.ComponentInputBindings,
                StructuredCollection: descriptor.StructuredCollection,
                RuntimeInputComponentPresetFieldId: descriptor.RuntimeInputComponentPresetFieldId),
            localValue,
            IsInherited: !hasOverride);
    }

    public FieldValue CreateEmbeddedComponentFieldValue(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        using var connection = OpenConnection();
        var config = ParseJsonObject(settings.ConfigJson);
        var inheritedConfig = EffectiveEmbeddedBaseConfig(connection, settings.ProjectId, config, slots);
        var inheritedValue = ComponentConfigFieldValue(inheritedConfig.ToJsonString(), descriptor);
        var overrides = EmbeddedOverrides(config, slots, createIfMissing: false);
        var hasOverride = overrides is not null && GetJsonValue(overrides, descriptor.JsonPath) is not null;
        var localValue = hasOverride && overrides is not null
            ? ComponentConfigFieldValue(overrides.ToJsonString(), descriptor)
            : inheritedValue;
        var options = ComponentClassFieldOptions(settings.ProjectId, descriptor);
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentPreset
            && EmbeddedComponentSlotCatalog.TryGet(embeddedFieldId, out var nestedSlot)
            && EmbeddedComponentHasOverrides(config, [.. slots, nestedSlot]);

        return new FieldValue(
            new FieldDefinition(
                descriptor.Id,
                descriptor.Label,
                descriptor.ValueKind,
                descriptor.IsEditable,
                descriptor.DefaultValue,
                CanInherit: true,
                InheritedValue: inheritedValue,
                Options: options,
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number,
                ComponentInputBindings: descriptor.ComponentInputBindings,
                StructuredCollection: descriptor.StructuredCollection,
                RuntimeInputComponentPresetFieldId: descriptor.RuntimeInputComponentPresetFieldId),
            localValue,
            IsInherited: !hasOverride,
            IsHighlighted: isHighlighted);
    }

    public FieldValue CreateEmbeddedComponentFieldValue(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
    {
        if (ownerNode.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            return CreateEmbeddedComponentFieldValue(ownerNode.Id, slots, embeddedFieldId);
        }

        if (ownerNode.Kind is ProjectTreeNodeKind.Module or ProjectTreeNodeKind.ModuleVariant)
        {
            if (slots.Count == 0) throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
            var moduleSettings = ownerNode.Kind == ProjectTreeNodeKind.Module
                ? GetModuleSettings(ownerNode.Id)
                : GetModuleVariantSettings(ownerNode);
            var moduleDescriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
            using var moduleConnection = OpenConnection();
            var moduleConfig = ParseJsonObject(moduleSettings.ConfigJson);
            var moduleInheritedConfig = EffectiveEmbeddedBaseConfig(moduleConnection, moduleSettings.ProjectId, moduleConfig, slots);
            var moduleInheritedValue = ComponentConfigFieldValue(moduleInheritedConfig.ToJsonString(), moduleDescriptor);
            var moduleOverrides = EmbeddedOverrides(moduleConfig, slots, createIfMissing: false);
            var moduleHasOverride = moduleOverrides is not null && GetJsonValue(moduleOverrides, moduleDescriptor.JsonPath) is not null;
            var moduleLocalValue = moduleHasOverride && moduleOverrides is not null
                ? ComponentConfigFieldValue(moduleOverrides.ToJsonString(), moduleDescriptor)
                : moduleInheritedValue;
            return new FieldValue(
                new FieldDefinition(moduleDescriptor.Id, moduleDescriptor.Label, moduleDescriptor.ValueKind, moduleDescriptor.IsEditable,
                    moduleDescriptor.DefaultValue, CanInherit: true, InheritedValue: moduleInheritedValue,
                    Options: ComponentClassFieldOptions(moduleSettings.ProjectId, moduleDescriptor), PairLabels: moduleDescriptor.PairLabels,
                    Number: moduleDescriptor.Number, ComponentInputBindings: moduleDescriptor.ComponentInputBindings),
                moduleLocalValue, IsInherited: !moduleHasOverride);
        }

        if (ownerNode.Kind != ProjectTreeNodeKind.ComponentPreset)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{ownerNode.Kind}'.");
        }

        if (slots.Count == 0)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var settings = GetComponentPresetSettings(ownerNode);
        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        using var connection = OpenConnection();
        var config = ParseJsonObject(settings.ConfigJson);
        var inheritedConfig = EffectiveEmbeddedBaseConfig(connection, settings.ProjectId, config, slots);
        var inheritedValue = ComponentConfigFieldValue(inheritedConfig.ToJsonString(), descriptor);
        var overrides = EmbeddedOverrides(config, slots, createIfMissing: false);
        var hasOverride = overrides is not null && GetJsonValue(overrides, descriptor.JsonPath) is not null;
        var localValue = hasOverride && overrides is not null
            ? ComponentConfigFieldValue(overrides.ToJsonString(), descriptor)
            : inheritedValue;
        var options = ComponentClassFieldOptions(settings.ProjectId, descriptor);
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentPreset
            && EmbeddedComponentSlotCatalog.TryGet(embeddedFieldId, out var nestedSlot)
            && EmbeddedComponentHasOverrides(config, [.. slots, nestedSlot]);

        return new FieldValue(
            new FieldDefinition(
                descriptor.Id,
                descriptor.Label,
                descriptor.ValueKind,
                descriptor.IsEditable,
                descriptor.DefaultValue,
                CanInherit: true,
                InheritedValue: inheritedValue,
                Options: options,
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number,
                ComponentInputBindings: descriptor.ComponentInputBindings,
                StructuredCollection: descriptor.StructuredCollection,
                RuntimeInputComponentPresetFieldId: descriptor.RuntimeInputComponentPresetFieldId),
            localValue,
            IsInherited: !hasOverride,
            IsHighlighted: isHighlighted);
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
            var config = ParseJsonObject(settings.ConfigJson);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var overrides = EmbeddedOverrides(config, slot, createIfMissing: true)
                ?? throw new InvalidOperationException($"Missing embedded override slot '{slotFieldId}'.");

            if (value.Equals("inherited", StringComparison.Ordinal)
                || descriptor.ValueKind == ValueKind.TypographyStyle && TypographyStyleValue.IsEmpty(value))
            {
                RemoveJsonValue(overrides, descriptor.JsonPath);
            }
            else
            {
                SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            }

            PersistDefaultComponentConfig(connection, componentClassId, config, metadata);
        }
    }

    public void UpdateEmbeddedComponentField(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var config = ParseJsonObject(settings.ConfigJson);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var overrides = EmbeddedOverrides(config, slots, createIfMissing: true)
                ?? throw new InvalidOperationException($"Missing embedded override slot '{slots[^1].FieldId}'.");

            if (value.Equals("inherited", StringComparison.Ordinal)
                || descriptor.ValueKind == ValueKind.TypographyStyle && TypographyStyleValue.IsEmpty(value))
            {
                RemoveJsonValue(overrides, descriptor.JsonPath);
            }
            else
            {
                SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            }

            PersistDefaultComponentConfig(connection, componentClassId, config, metadata);
        }
    }

    public void UpdateEmbeddedComponentField(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
    {
        if (ownerNode.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            UpdateEmbeddedComponentField(ownerNode.Id, slots, embeddedFieldId, value);
            return;
        }

        if (ownerNode.Kind is ProjectTreeNodeKind.Module or ProjectTreeNodeKind.ModuleVariant)
        {
            if (slots.Count == 0) throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
            var moduleDescriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
            lock (WriteGate)
            {
                using var connection = OpenConnection();
                var settings = ownerNode.Kind == ProjectTreeNodeKind.Module
                    ? GetModuleSettings(ownerNode.Id)
                    : GetModuleVariantSettings(ownerNode);
                var config = ParseJsonObject(settings.ConfigJson);
                var overrides = EmbeddedOverrides(config, slots, createIfMissing: true)
                    ?? throw new InvalidOperationException($"Missing embedded override slot '{slots[^1].FieldId}'.");
                if (value.Equals("inherited", StringComparison.Ordinal)
                    || moduleDescriptor.ValueKind == ValueKind.TypographyStyle && TypographyStyleValue.IsEmpty(value))
                    RemoveJsonValue(overrides, moduleDescriptor.JsonPath);
                else
                    SetJsonValue(overrides, moduleDescriptor.JsonPath, ComponentConfigJsonValue(moduleDescriptor.ValueKind, value));
                if (ownerNode.Kind == ProjectTreeNodeKind.Module)
                    _appModuleRepository.UpdateModuleConfig(connection, ownerNode.Id, config.ToJsonString());
                else
                    ReplaceModuleVariantConfig(ownerNode, config.ToJsonString());
            }
            return;
        }

        if (ownerNode.Kind != ProjectTreeNodeKind.ComponentPreset)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{ownerNode.Kind}'.");
        }

        if (slots.Count == 0)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var config = ComponentPresetConfigForUpdate(connection, ownerNode, out var componentClassId, out var metadata);
            var overrides = EmbeddedOverrides(config, slots, createIfMissing: true)
                ?? throw new InvalidOperationException($"Missing embedded override slot '{slots[^1].FieldId}'.");

            if (value.Equals("inherited", StringComparison.Ordinal)
                || descriptor.ValueKind == ValueKind.TypographyStyle && TypographyStyleValue.IsEmpty(value))
            {
                RemoveJsonValue(overrides, descriptor.JsonPath);
            }
            else
            {
                SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            }

            PersistComponentPresetUpdate(connection, ownerNode, componentClassId, config, metadata);
        }
    }

    private static IReadOnlyList<ComponentClassPreset> ComponentClassPresets(
        string metadataJson,
        string owner = "Component class metadata")
    {
        var metadata = ParseJsonObject(metadataJson);
        return VariantEnvelopeContract.Read(metadata, "presets", owner)
            .Select((preset) => new ComponentClassPreset(
                preset.Id,
                preset.Name,
                preset.IsProtected,
                preset.IsLocked,
                preset.Config.ToJsonString()))
            .OrderBy((preset) => preset.Id.Equals(DefaultComponentPresetId, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy((preset) => preset.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string DefaultComponentPresetConfigJson(string metadataJson, string owner)
    {
        return ComponentClassPresets(metadataJson, owner)
            .Single((preset) => preset.Id.Equals(DefaultComponentPresetId, StringComparison.Ordinal))
            .ConfigJson;
    }

    private static void SetDefaultComponentPresetConfig(JsonObject metadata, JsonObject config)
    {
        var presets = VariantEnvelopeContract.RequiredArray(metadata, "presets", "Component class metadata");
        var defaultPreset = FindPreset(presets, DefaultComponentPresetId)
            ?? throw new InvalidOperationException("Component class has no Default variant.");
        defaultPreset["config"] = config.DeepClone();
    }

    private static void PersistDefaultComponentConfig(
        SqliteConnection connection,
        string componentClassId,
        JsonObject config,
        JsonObject metadata)
    {
        SetDefaultComponentPresetConfig(metadata, config);
        Execute(
            connection,
            "UPDATE component_classes SET config_json = $configJson, metadata_json = $metadataJson WHERE id = $id",
            ("$id", componentClassId),
            ("$configJson", config.ToJsonString()),
            ("$metadataJson", metadata.ToJsonString()));
    }

    private static void PersistComponentPresetUpdate(
        SqliteConnection connection,
        ProjectTreeNode presetNode,
        string componentClassId,
        JsonObject config,
        JsonObject metadata)
    {
        if (!TryParseComponentPresetNodeId(presetNode.Id, out _, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{presetNode.Id}'.");
        }
        if (presetId.Equals(DefaultComponentPresetId, StringComparison.Ordinal))
        {
            PersistDefaultComponentConfig(connection, componentClassId, config, metadata);
            return;
        }
        Execute(
            connection,
            "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", componentClassId),
            ("$metadataJson", metadata.ToJsonString()));
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
    private static string ComponentConfigFieldValue(string configJson, ComponentClassFieldDescriptor descriptor)
    {
        if (descriptor.ValueKind == ValueKind.EmbeddedComponent)
        {
            return descriptor.DefaultValue;
        }

        var config = ParseJsonObject(configJson);
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
            ValueKind.Motion => node is JsonObject
                ? node.ToJsonString()
                : descriptor.DefaultValue,
            ValueKind.TypographyStyle => node is JsonObject
                ? node.ToJsonString()
                : descriptor.DefaultValue,
            ValueKind.IconTokenList => node is JsonArray
                ? node.ToJsonString()
                : descriptor.DefaultValue,
            ValueKind.IconSlots => node.ToJsonString(),
            ValueKind.ComponentInputBindings => node is JsonObject
                ? node.ToJsonString()
                : descriptor.DefaultValue,
            ValueKind.StructuredCollection => node is JsonArray
                ? node.ToJsonString()
                : descriptor.DefaultValue,
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
            ValueKind.Motion => JsonNode.Parse(value)
                ?? throw new InvalidOperationException("Motion value must be valid JSON."),
            ValueKind.TypographyStyle => JsonNode.Parse(value)
                ?? throw new InvalidOperationException("Typography style value must be valid JSON."),
            ValueKind.IconTokenList => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value)
                ?? new JsonArray(),
            ValueKind.IconSlots => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? ComponentClassFieldCatalog.EmptyIconSlots : value)
                ?? JsonNode.Parse(ComponentClassFieldCatalog.EmptyIconSlots)!,
            ValueKind.ComponentInputBindings => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "{}" : value)
                ?? new JsonObject(),
            ValueKind.StructuredCollection => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value)
                ?? new JsonArray(),
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

    private static JsonObject? EmbeddedOverrides(
        JsonObject config,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        bool createIfMissing)
    {
        JsonObject? currentConfig = config;
        JsonObject? overrides = null;
        foreach (var slot in slots)
        {
            if (currentConfig is null)
            {
                return null;
            }

            overrides = EmbeddedOverrides(currentConfig, slot, createIfMissing);
            currentConfig = overrides;
        }

        return overrides;
    }

    private static bool EmbeddedComponentHasOverrides(
        JsonObject config,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        var overrides = EmbeddedOverrides(config, slots, createIfMissing: false);
        return overrides is not null && HasEffectiveJsonValue(overrides);
    }

    private static JsonObject EffectiveEmbeddedBaseConfig(
        SqliteConnection connection,
        string projectId,
        JsonObject ownerConfig,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        JsonObject? currentContainer = ownerConfig;
        JsonObject? current = null;
        for (var index = 0; index < slots.Count; index++)
        {
            var slotNode = currentContainer is null
                ? null
                : JsonPath.Get(currentContainer, slots[index].SlotPath) as JsonObject;
            var presetId = JsonPath.String(slotNode ?? [], "presetId", DefaultComponentPresetId);
            var child = ParseJsonObject(GetComponentClassPresetConfigJson(
                connection,
                projectId,
                slots[index].EmbeddedComponentType,
                presetId));
            var overrides = slotNode?["overrides"] as JsonObject;
            if (index < slots.Count - 1 && overrides is not null)
            {
                MergeOverride(child, overrides);
            }

            current = child;
            currentContainer = current;
        }

        return current ?? [];
    }

    private static void MergeOverride(JsonObject target, JsonObject overrides)
    {
        foreach (var pair in overrides)
        {
            if (pair.Value is JsonObject overrideObject
                && target[pair.Key] is JsonObject targetObject)
            {
                MergeOverride(targetObject, overrideObject);
                continue;
            }

            target[pair.Key] = pair.Value?.DeepClone();
        }
    }

}
