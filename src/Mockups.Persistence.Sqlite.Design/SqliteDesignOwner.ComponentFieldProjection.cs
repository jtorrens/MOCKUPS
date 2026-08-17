using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using Microsoft.Data.Sqlite;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    internal FieldValue CreateComponentFieldValue(
        ComponentClassSettings settings,
        ComponentClassFieldDescriptor descriptor,
        IReadOnlyList<FieldOption>? options)
    {
        var value = descriptor.Id == "component.type"
            ? EditorUiText.IdentifierLabel(settings.ComponentType)
            : ComponentConfigFieldValue(
                settings.ConfigJson,
                descriptor);
        var isHighlighted = descriptor.ValueKind is
                ValueKind.EmbeddedComponent
                or ValueKind.ComponentVariant
                or ValueKind.ComponentVariantSlot
            && EmbeddedComponentSlotCatalog.TryGet(
                descriptor.Id,
                out var slot)
            && EmbeddedComponentHasOverrides(
                settings.ConfigJson,
                slot);

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
                ComponentInputBindings:
                    descriptor.ComponentInputBindings,
                StructuredCollection:
                    descriptor.StructuredCollection,
                RuntimeInputComponentVariantFieldId:
                    descriptor.RuntimeInputComponentVariantFieldId,
                Unit: descriptor.Unit,
                HelpText: descriptor.HelpText,
                ValuePattern: descriptor.ValuePattern,
                ValuePatternMessage: descriptor.ValuePatternMessage),
            value,
            IsHighlighted: isHighlighted);
    }

    internal static string ComponentConfigFieldValue(
        string configJson,
        ComponentClassFieldDescriptor descriptor)
    {
        if (descriptor.ValueKind == ValueKind.EmbeddedComponent)
        {
            return descriptor.DefaultValue;
        }

        var config = ParseJsonObject(configJson);
        var node = JsonPath.Get(config, descriptor.JsonPath);
        if (node is null)
        {
            return descriptor.DefaultValue;
        }

        var owner = $"Component field '{descriptor.Id}'";
        RuntimeInputValueKindContract.ValidateValue(
            descriptor.ValueKind,
            node,
            owner);
        ScalarValuePatternContract.Validate(
            descriptor.ValuePattern,
            descriptor.ValuePatternMessage,
            node,
            owner);
        return descriptor.ValueKind switch
        {
            ValueKind.Boolean =>
                BooleanText.Format(node.GetValue<bool>()),
            ValueKind.Integer
                or ValueKind.Decimal
                or ValueKind.HueDegrees
                or ValueKind.Alpha =>
                node.ToJsonString(),
            ValueKind.TypographyStyle
                or ValueKind.TypographySystemStyle =>
                TypographyStyleValue.Parse(node).ToJsonString(),
            ValueKind.AlignmentPlacement
                or ValueKind.Motion
                or ValueKind.MotionTiming
                or ValueKind.IconTokenList
                or ValueKind.ComponentInputBindings
                or ValueKind.ComponentVariantSlot
                or ValueKind.StructuredCollection
                or ValueKind.BehaviorTiming =>
                node.ToJsonString(),
            _ => node.GetValue<string>(),
        };
    }

    internal FieldValue CreateRuntimeComponentOverrideFieldValue(
        string baseConfigJson,
        JsonObject overrides,
        ComponentClassFieldDescriptor descriptor,
        IReadOnlyList<FieldOption>? options)
    {
        var inheritedValue = ComponentConfigFieldValue(
            baseConfigJson,
            descriptor);
        var hasOverride =
            JsonPath.Get(overrides, descriptor.JsonPath) is not null;
        var localValue = hasOverride
            ? ComponentConfigFieldValue(
                overrides.ToJsonString(),
                descriptor)
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
                Options: options,
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number,
                ComponentInputBindings:
                    descriptor.ComponentInputBindings,
                StructuredCollection:
                    descriptor.StructuredCollection,
                RuntimeInputComponentVariantFieldId:
                    descriptor.RuntimeInputComponentVariantFieldId,
                Unit: descriptor.Unit,
                HelpText: descriptor.HelpText,
                ValuePattern: descriptor.ValuePattern,
                ValuePatternMessage: descriptor.ValuePatternMessage),
            localValue,
            IsInherited: !hasOverride);
    }

    internal FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        ComponentClassFieldDescriptor descriptor,
        IReadOnlyList<FieldOption>? options)
    {
        if (slots.Count == 0)
        {
            return CreateRuntimeComponentOverrideFieldValue(
                baseConfigJson,
                overrides,
                descriptor,
                options);
        }

        using var connection = OpenConnection();
        var effectiveOwnerConfig = ParseJsonObject(baseConfigJson);
        ComponentConfigOverrideMerger.MergeInto(
            effectiveOwnerConfig,
            overrides);
        var inheritedConfig = EffectiveEmbeddedBaseConfig(
            connection,
            projectId,
            effectiveOwnerConfig,
            slots);
        var inheritedValue = ComponentConfigFieldValue(
            inheritedConfig.ToJsonString(),
            descriptor);
        var localOverrides = EmbeddedOverrides(
            overrides,
            slots,
            createIfMissing: false);
        var hasOverride = localOverrides is not null
            && JsonPath.Get(
                localOverrides,
                descriptor.JsonPath) is not null;
        var localValue = hasOverride && localOverrides is not null
            ? ComponentConfigFieldValue(
                localOverrides.ToJsonString(),
                descriptor)
            : inheritedValue;
        var isHighlighted = descriptor.ValueKind is
                ValueKind.EmbeddedComponent
                or ValueKind.ComponentVariant
            && EmbeddedComponentSlotCatalog.TryGet(
                descriptor.Id,
                out var nestedSlot)
            && EmbeddedComponentHasOverrides(
                overrides,
                [.. slots, nestedSlot]);
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
                ComponentInputBindings:
                    descriptor.ComponentInputBindings,
                StructuredCollection:
                    descriptor.StructuredCollection,
                RuntimeInputComponentVariantFieldId:
                    descriptor.RuntimeInputComponentVariantFieldId,
                Unit: descriptor.Unit,
                HelpText: descriptor.HelpText,
                ValuePattern: descriptor.ValuePattern,
                ValuePatternMessage: descriptor.ValuePatternMessage),
            localValue,
            IsInherited: !hasOverride,
            IsHighlighted: isHighlighted);
    }

    internal void UpdateRuntimeComponentOverride(
        JsonObject overrides,
        string fieldId,
        string value)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0)
        {
            return;
        }

        if (value == "inherited")
        {
            RemoveJsonValue(overrides, descriptor.JsonPath);
            return;
        }

        SetJsonValue(
            overrides,
            descriptor.JsonPath,
            ComponentConfigJsonValue(
                descriptor,
                value));
    }

    internal void UpdateRuntimeComponentOverride(
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value)
    {
        if (slots.Count == 0)
        {
            UpdateRuntimeComponentOverride(
                overrides,
                fieldId,
                value);
            return;
        }

        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0)
        {
            return;
        }

        var localOverrides = EmbeddedOverrides(
                overrides,
                slots,
                createIfMissing: true)
            ?? throw new InvalidOperationException(
                $"Missing runtime component override slot '{slots[^1].FieldId}'.");
        if (value.Equals("inherited", StringComparison.Ordinal)
            || descriptor.ValueKind == ValueKind.TypographyStyle
                && TypographyStyleValue.IsEmpty(value))
        {
            RemoveJsonValue(localOverrides, descriptor.JsonPath);
            return;
        }

        SetJsonValue(
            localOverrides,
            descriptor.JsonPath,
            ComponentConfigJsonValue(
                descriptor,
                value));
    }

    internal FieldValue CreateEmbeddedComponentFieldValue(
        ComponentClassSettings settings,
        string slotFieldId,
        string embeddedComponentType,
        ComponentClassFieldDescriptor descriptor,
        IReadOnlyList<FieldOption>? options)
    {
        var slot = EmbeddedComponentSlotCatalog.Get(slotFieldId);
        if (!slot.EmbeddedComponentType.Equals(
                embeddedComponentType,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Embedded component '{embeddedComponentType}' is not supported for slot '{slotFieldId}'.");
        }

        using var connection = OpenConnection();
        var config = ParseJsonObject(settings.ConfigJson);
        var inheritedConfig = EffectiveEmbeddedBaseConfig(
            connection,
            settings.ProjectId,
            config,
            [slot]);
        var inheritedValue = ComponentConfigFieldValue(
            inheritedConfig.ToJsonString(),
            descriptor);
        var overrides = EmbeddedOverrides(
            config,
            slot,
            createIfMissing: false);
        var hasOverride = overrides is not null
            && JsonPath.Get(
                overrides,
                descriptor.JsonPath) is not null;
        var localValue = hasOverride && overrides is not null
            ? ComponentConfigFieldValue(
                overrides.ToJsonString(),
                descriptor)
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
                Options: options,
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number,
                ComponentInputBindings:
                    descriptor.ComponentInputBindings,
                StructuredCollection:
                    descriptor.StructuredCollection,
                RuntimeInputComponentVariantFieldId:
                    descriptor.RuntimeInputComponentVariantFieldId,
                HelpText: descriptor.HelpText,
                ValuePattern: descriptor.ValuePattern,
                ValuePatternMessage: descriptor.ValuePatternMessage),
            localValue,
            IsInherited: !hasOverride);
    }

    internal FieldValue CreateEmbeddedComponentFieldValue(
        ProjectTreeNodeKind ownerKind,
        string projectId,
        string configJson,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        ComponentClassFieldDescriptor descriptor,
        IReadOnlyList<FieldOption>? options)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException(
                $"Embedded component field '{descriptor.Id}' needs at least one slot.");
        }

        using var connection = OpenConnection();
        var config = ParseJsonObject(configJson);
        var inheritedConfig = EffectiveEmbeddedBaseConfig(
            connection,
            projectId,
            config,
            slots);
        var inheritedValue = ComponentConfigFieldValue(
            inheritedConfig.ToJsonString(),
            descriptor);
        var overrides = EmbeddedOverrides(
            config,
            slots,
            createIfMissing: false);
        var hasOverride = overrides is not null
            && JsonPath.Get(
                overrides,
                descriptor.JsonPath) is not null;
        var localValue = hasOverride && overrides is not null
            ? ComponentConfigFieldValue(
                overrides.ToJsonString(),
                descriptor)
            : inheritedValue;
        var isHighlighted = descriptor.ValueKind is
                ValueKind.EmbeddedComponent
                or ValueKind.ComponentVariant
            && EmbeddedComponentSlotCatalog.TryGet(
                descriptor.Id,
                out var nestedSlot)
            && EmbeddedComponentHasOverrides(
                config,
                [.. slots, nestedSlot]);
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
                ComponentInputBindings:
                    descriptor.ComponentInputBindings,
                StructuredCollection:
                    descriptor.StructuredCollection,
                RuntimeInputComponentVariantFieldId:
                    descriptor.RuntimeInputComponentVariantFieldId,
                HelpText: descriptor.HelpText,
                ValuePattern: descriptor.ValuePattern,
                ValuePatternMessage: descriptor.ValuePatternMessage),
            localValue,
            IsInherited: !hasOverride,
            IsHighlighted: isHighlighted);
    }

    private static bool EmbeddedComponentHasOverrides(
        JsonObject config,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        var overrides = EmbeddedOverrides(
            config,
            slots,
            createIfMissing: false);
        return overrides is not null
            && HasEffectiveJsonValue(overrides);
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
                : JsonPath.Get(
                    currentContainer,
                    slots[index].SlotPath) as JsonObject;
            var variantReference = RequiredComponentVariantReference(
                slotNode,
                $"Embedded component slot '{slots[index].FieldId}'");
            var child = ParseJsonObject(
                GetComponentClassVariantConfigJson(
                    connection,
                    projectId,
                    slots[index].EmbeddedComponentType,
                    variantReference));
            var overrides = slotNode?["overrides"] as JsonObject;
            if (index < slots.Count - 1 && overrides is not null)
            {
                ComponentConfigOverrideMerger.MergeInto(
                    child,
                    overrides);
            }

            current = child;
            currentContainer = current;
        }

        return current ?? [];
    }
}
