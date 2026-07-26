using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

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
                Unit: descriptor.Unit),
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
                or ValueKind.IconSlots
                or ValueKind.ComponentInputBindings
                or ValueKind.ComponentVariantSlot
                or ValueKind.StructuredCollection
                or ValueKind.BehaviorTiming =>
                node.ToJsonString(),
            _ => node.GetValue<string>(),
        };
    }
}
