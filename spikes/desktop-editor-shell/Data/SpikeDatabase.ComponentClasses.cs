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
        return ComponentClassSettingsFrom(_componentClassRepository.Get(componentClassId));
    }

    public ComponentClassSettings GetComponentVariantSettings(ProjectTreeNode variantNode)
    {
        using var connection = OpenConnection();
        return GetComponentVariantSettings(connection, variantNode);
    }

    public void UpdateComponentClassDesignPreviewJson(string componentClassId, string designPreviewJson) =>
        _componentClassRepository.UpdateDesignPreview(componentClassId, designPreviewJson);

    private ComponentClassSettings GetComponentVariantSettings(SqliteConnection connection, ProjectTreeNode variantNode)
    {
        if (variantNode.Kind != ProjectTreeNodeKind.ComponentVariant
            || !TryParseComponentVariantNodeId(variantNode.Id, out var componentClassId, out var variantId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{variantNode.Id}'.");
        }

        var settings = GetComponentClassSettings(connection, componentClassId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        var variants = VariantEnvelopeContract.RequiredArray(
            metadata,
            "variants",
            $"Component class '{componentClassId}'");

        var variant = FindVariant(variants, variantId)
            ?? throw new InvalidOperationException($"Missing component variant '{variantId}'.");
        if (variant["config"] is not JsonObject configObject)
        {
            throw new InvalidOperationException($"Component variant '{variantId}' has no config.");
        }

        var config = configObject.ToJsonString();
        var variantName = JsonPath.String(variant, "name", variantId);

        return settings with
        {
            Name = string.IsNullOrWhiteSpace(variantName)
                ? settings.Name
                : $"{settings.Name} · {variantName}",
            ConfigJson = config,
        };
    }

    private JsonObject ComponentVariantConfigForUpdate(
        SqliteConnection connection,
        ProjectTreeNode variantNode,
        out string componentClassId,
        out JsonObject metadata)
    {
        if (variantNode.Kind != ProjectTreeNodeKind.ComponentVariant
            || !TryParseComponentVariantNodeId(variantNode.Id, out componentClassId, out var variantId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{variantNode.Id}'.");
        }

        var settings = GetComponentClassSettings(connection, componentClassId);
        metadata = ParseJsonObject(settings.MetadataJson);
        var variants = VariantEnvelopeContract.RequiredArray(
            metadata,
            "variants",
            $"Component class '{componentClassId}'");

        var variant = FindVariant(variants, variantId)
            ?? throw new InvalidOperationException($"Missing component variant '{variantId}'.");
        if (JsonBool(variant, ["locked"]))
        {
            throw new InvalidOperationException($"Component variant '{variantId}' is locked.");
        }

        if (variant["config"] is not JsonObject config)
        {
            throw new InvalidOperationException($"Component variant '{variantId}' has no config.");
        }

        return config;
    }


    private ComponentClassSettings GetComponentClassSettings(SqliteConnection connection, string componentClassId)
    {
        return ComponentClassSettingsFrom(_componentClassRepository.Get(connection, componentClassId));
    }

    private static ComponentClassSettings ComponentClassSettingsFrom(ComponentClassDefinitionRecord record)
    {
        return new ComponentClassSettings(
            record.ProjectId,
            record.ComponentType,
            record.RecordClassId,
            record.Name,
            record.Notes,
            DefaultComponentVariantConfigJson(record.MetadataJson, $"Component class '{record.Id}'"),
            record.DesignPreviewJson,
            record.MetadataJson);
    }

    public FieldValue CreateComponentClassFieldValue(string componentClassId, string fieldId)
    {
        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var value = fieldId == "component.type"
            ? EditorUiText.IdentifierLabel(settings.ComponentType)
            : ComponentConfigFieldValue(settings.ConfigJson, descriptor);
        var options = ComponentClassFieldOptions(settings.ProjectId, descriptor);
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentVariant
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
                RuntimeInputComponentVariantFieldId: descriptor.RuntimeInputComponentVariantFieldId,
                Unit: descriptor.Unit),
            value,
            IsHighlighted: isHighlighted);
    }

    public FieldValue CreateComponentVariantFieldValue(ProjectTreeNode variantNode, string fieldId)
    {
        var settings = GetComponentVariantSettings(variantNode);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var value = fieldId == "component.type"
            ? EditorUiText.IdentifierLabel(settings.ComponentType)
            : ComponentConfigFieldValue(settings.ConfigJson, descriptor);
        var options = ComponentClassFieldOptions(settings.ProjectId, descriptor);
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentVariant
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
                RuntimeInputComponentVariantFieldId: descriptor.RuntimeInputComponentVariantFieldId,
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
                RuntimeInputComponentVariantFieldId: descriptor.RuntimeInputComponentVariantFieldId,
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
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentVariant
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
                RuntimeInputComponentVariantFieldId: descriptor.RuntimeInputComponentVariantFieldId,
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
            CurrentComponentConfigContract.Validate(
                settings.ComponentType,
                config,
                $"Component class '{componentClassId}' config_json");
            SetDefaultComponentVariantConfig(metadata, config);
            _componentClassRepository.UpdateConfigAndMetadata(
                connection,
                componentClassId,
                config.ToJsonString(),
                metadata.ToJsonString());
        }
    }

    public void UpdateComponentVariantField(ProjectTreeNode variantNode, string fieldId, string value)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0)
        {
            return;
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var config = ComponentVariantConfigForUpdate(connection, variantNode, out var componentClassId, out var metadata);
            SetJsonValue(config, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            PersistComponentVariantUpdate(connection, variantNode, componentClassId, config, metadata);
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
                RuntimeInputComponentVariantFieldId: descriptor.RuntimeInputComponentVariantFieldId),
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
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentVariant
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
                RuntimeInputComponentVariantFieldId: descriptor.RuntimeInputComponentVariantFieldId),
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

        if (ownerNode.Kind != ProjectTreeNodeKind.ComponentVariant)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{ownerNode.Kind}'.");
        }

        if (slots.Count == 0)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var settings = GetComponentVariantSettings(ownerNode);
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
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentVariant
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
                RuntimeInputComponentVariantFieldId: descriptor.RuntimeInputComponentVariantFieldId),
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

        if (ownerNode.Kind != ProjectTreeNodeKind.ComponentVariant)
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
            var config = ComponentVariantConfigForUpdate(connection, ownerNode, out var componentClassId, out var metadata);
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

            PersistComponentVariantUpdate(connection, ownerNode, componentClassId, config, metadata);
        }
    }

    private static IReadOnlyList<ComponentClassVariant> ComponentClassVariants(
        string metadataJson,
        string owner = "Component class metadata")
    {
        var metadata = ParseJsonObject(metadataJson);
        return VariantEnvelopeContract.Read(metadata, "variants", owner)
            .Select((variant) => new ComponentClassVariant(
                variant.Id,
                variant.Name,
                variant.IsProtected,
                variant.IsLocked,
                variant.Config.ToJsonString()))
            .OrderBy((variant) => variant.Id.Equals(DefaultComponentVariantId, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy((variant) => variant.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string DefaultComponentVariantConfigJson(string metadataJson, string owner)
    {
        return ComponentClassVariants(metadataJson, owner)
            .Single((variant) => variant.Id.Equals(DefaultComponentVariantId, StringComparison.Ordinal))
            .ConfigJson;
    }

    private static void SetDefaultComponentVariantConfig(JsonObject metadata, JsonObject config)
    {
        var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", "Component class metadata");
        var defaultVariant = FindVariant(variants, DefaultComponentVariantId)
            ?? throw new InvalidOperationException("Component class has no Default variant.");
        defaultVariant["config"] = config.DeepClone();
    }

    private void PersistDefaultComponentConfig(
        SqliteConnection connection,
        string componentClassId,
        JsonObject config,
        JsonObject metadata)
    {
        var componentType = _componentClassRepository.Get(connection, componentClassId).ComponentType;
        CurrentComponentConfigContract.Validate(
            componentType,
            config,
            $"Component class '{componentClassId}' Default Variant config");
        SetDefaultComponentVariantConfig(metadata, config);
        _componentClassRepository.UpdateConfigAndMetadata(
            connection,
            componentClassId,
            config.ToJsonString(),
            metadata.ToJsonString());
    }

    private void PersistComponentVariantUpdate(
        SqliteConnection connection,
        ProjectTreeNode variantNode,
        string componentClassId,
        JsonObject config,
        JsonObject metadata)
    {
        if (!TryParseComponentVariantNodeId(variantNode.Id, out _, out var variantId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{variantNode.Id}'.");
        }
        var componentType = _componentClassRepository.Get(connection, componentClassId).ComponentType;
        CurrentComponentConfigContract.Validate(
            componentType,
            config,
            $"Component class '{componentClassId}' Variant '{variantId}' config");
        if (variantId.Equals(DefaultComponentVariantId, StringComparison.Ordinal))
        {
            PersistDefaultComponentConfig(connection, componentClassId, config, metadata);
            return;
        }
        _componentClassRepository.UpdateMetadata(connection, componentClassId, metadata.ToJsonString());
    }

    private IReadOnlyList<ComponentClassDefinitionRecord> QueryComponentClassRows(SqliteConnection connection) =>
        _componentClassRepository.QueryAll(connection);
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
            ValueKind.StructuredCollection => JsonPath.ParseRequiredArray(value, "Structured collection field value"),
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

    private JsonObject EffectiveEmbeddedBaseConfig(
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
            var variantReference = RequiredComponentVariantReference(
                slotNode,
                $"Embedded component slot '{slots[index].FieldId}'");
            var child = ParseJsonObject(GetComponentClassVariantConfigJson(
                connection,
                projectId,
                slots[index].EmbeddedComponentType,
                variantReference));
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
