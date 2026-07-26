using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public ComponentClassSettings GetComponentClassSettings(
        string componentClassId) =>
        _designOwner.GetComponentClassSettings(componentClassId);

    public ComponentClassSettings GetComponentVariantSettings(
        ProjectTreeNode variantNode) =>
        _designOwner.GetComponentVariantSettings(variantNode);

    public void UpdateComponentClassDesignPreviewJson(
        string componentClassId,
        string designPreviewJson) =>
        _designOwner.UpdateComponentClassDesignPreviewJson(
            componentClassId,
            designPreviewJson);

    private ComponentClassSettings GetComponentVariantSettings(
        SqliteConnection connection,
        ProjectTreeNode variantNode) =>
        _designOwner.GetComponentVariantSettings(
            connection,
            variantNode);

    private JsonObject ComponentVariantConfigForUpdate(
        SqliteConnection connection,
        ProjectTreeNode variantNode,
        out string componentClassId,
        out JsonObject metadata) =>
        _designOwner.ComponentVariantConfigForUpdate(
            connection,
            variantNode,
            out componentClassId,
            out metadata);


    private ComponentClassSettings GetComponentClassSettings(
        SqliteConnection connection,
        string componentClassId) =>
        _designOwner.GetComponentClassSettings(
            connection,
            componentClassId);

    public FieldValue CreateComponentClassFieldValue(string componentClassId, string fieldId)
    {
        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var value = fieldId == "component.type"
            ? EditorUiText.IdentifierLabel(settings.ComponentType)
            : ComponentConfigFieldValue(settings.ConfigJson, descriptor);
        var options = ComponentClassFieldOptions(settings.ProjectId, descriptor);
        var isHighlighted = descriptor.ValueKind is
                ValueKind.EmbeddedComponent or ValueKind.ComponentVariant or ValueKind.ComponentVariantSlot
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
        var isHighlighted = descriptor.ValueKind is
                ValueKind.EmbeddedComponent or ValueKind.ComponentVariant or ValueKind.ComponentVariantSlot
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
        ComponentConfigOverrideMerger.MergeInto(effectiveOwnerConfig, overrides);
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
        SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value, descriptor.Id));
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
        SetJsonValue(localOverrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value, descriptor.Id));
    }

    public void UpdateComponentClassField(
        string componentClassId,
        string fieldId,
        string value) =>
        _designOwner.UpdateComponentClassField(
            componentClassId,
            fieldId,
            value);

    public void UpdateComponentVariantField(
        ProjectTreeNode variantNode,
        string fieldId,
        string value) =>
        _designOwner.UpdateComponentVariantField(
            variantNode,
            fieldId,
            value);

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
                SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value, descriptor.Id));
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
                SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value, descriptor.Id));
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
                    SetJsonValue(overrides, moduleDescriptor.JsonPath, ComponentConfigJsonValue(moduleDescriptor.ValueKind, value, moduleDescriptor.Id));
                if (ownerNode.Kind == ProjectTreeNodeKind.Module)
                    _designOwner.AppModuleRepository.UpdateModuleConfig(connection, ownerNode.Id, config.ToJsonString());
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
                SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value, descriptor.Id));
            }

            PersistComponentVariantUpdate(connection, ownerNode, componentClassId, config, metadata);
        }
    }

    private static IReadOnlyList<ComponentClassVariant> ComponentClassVariants(
        string metadataJson,
        string owner = "Component class metadata") =>
        SqliteDesignOwner.ComponentClassVariants(
            metadataJson,
            owner);

    private static string DefaultComponentVariantConfigJson(
        string metadataJson,
        string owner) =>
        SqliteDesignOwner.DefaultComponentVariantConfigJson(
            metadataJson,
            owner);

    private void PersistDefaultComponentConfig(
        SqliteConnection connection,
        string componentClassId,
        JsonObject config,
        JsonObject metadata) =>
        _designOwner.PersistDefaultComponentConfig(
            connection,
            componentClassId,
            config,
            metadata);

    private void PersistComponentVariantUpdate(
        SqliteConnection connection,
        ProjectTreeNode variantNode,
        string componentClassId,
        JsonObject config,
        JsonObject metadata) =>
        _designOwner.PersistComponentVariantUpdate(
            connection,
            variantNode,
            componentClassId,
            config,
            metadata);

    private IReadOnlyList<ComponentClassDefinitionRecord> QueryComponentClassRows(SqliteConnection connection) =>
        _designOwner.ComponentClassRepository.QueryAll(connection);
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

        var owner = $"Component field '{descriptor.Id}'";
        RuntimeInputValueKindContract.ValidateValue(descriptor.ValueKind, node, owner);
        return descriptor.ValueKind switch
        {
            ValueKind.Boolean => BoolToString(node.GetValue<bool>()),
            ValueKind.Integer or ValueKind.Decimal or ValueKind.HueDegrees or ValueKind.Alpha =>
                node.ToJsonString(),
            ValueKind.TypographyStyle or ValueKind.TypographySystemStyle =>
                TypographyStyleValue.Parse(node).ToJsonString(),
            ValueKind.AlignmentPlacement
                or ValueKind.Motion
                or ValueKind.MotionTiming
                or ValueKind.IconTokenList
                or ValueKind.IconSlots
                or ValueKind.ComponentInputBindings
                or ValueKind.ComponentVariantSlot
                or ValueKind.StructuredCollection
                or ValueKind.BehaviorTiming => node.ToJsonString(),
            _ => node.GetValue<string>(),
        };
    }

    private static JsonNode ComponentConfigJsonValue(
        ValueKind valueKind,
        string value,
        string fieldId) =>
        SqliteDesignOwner.ComponentConfigJsonValue(
            valueKind,
            value,
            fieldId);

    private static JsonObject? EmbeddedOverrides(JsonObject config, EmbeddedComponentSlotDefinition slot, bool createIfMissing)
    {
        var slotValue = JsonPath.Get(config, slot.SlotPath);
        JsonObject slotNode;
        if (slotValue is null)
        {
            if (!createIfMissing) return null;

            slotNode = [];
            JsonPath.Set(config, slot.SlotPath, slotNode);
        }
        else
        {
            slotNode = slotValue as JsonObject
                ?? throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' must be an object.");
        }

        if (slotNode.TryGetPropertyValue("overrides", out var overridesNode))
        {
            return overridesNode as JsonObject
                ?? throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' overrides must be an object.");
        }

        if (!createIfMissing) return null;

        var overrides = new JsonObject();
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
                ComponentConfigOverrideMerger.MergeInto(child, overrides);
            }

            current = child;
            currentContainer = current;
        }

        return current ?? [];
    }

}
