using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ComponentFieldOptionResolver
{
    private readonly IComponentFieldDesignOptionSource
        _designOptions;
    private readonly IComponentFieldResourceOptionSource
        _resourceOptions;

    internal ComponentFieldOptionResolver(
        IComponentFieldDesignOptionSource designOptions,
        IComponentFieldResourceOptionSource resourceOptions)
    {
        _designOptions = designOptions;
        _resourceOptions = resourceOptions;
    }

    internal IReadOnlyList<FieldOption>? Resolve(
        string projectId,
        ComponentClassFieldDescriptor descriptor) =>
        descriptor.ValueKind switch
        {
            ValueKind.EmbeddedComponent =>
                _designOptions.GetEmbeddedComponentOptions(
                    projectId,
                    descriptor.DefaultValue),
            ValueKind.ComponentVariant
                when EmbeddedComponentSlotCatalog.TryGet(
                    descriptor.Id,
                    out var slot) =>
                _designOptions.GetComponentVariantReferenceOptionsByType(
                    projectId,
                    slot.EmbeddedComponentType),
            ValueKind.ComponentVariant
                or ValueKind.ComponentVariantSlot
                when !string.IsNullOrWhiteSpace(
                    descriptor.ComponentVariantType) =>
                _designOptions.GetComponentVariantReferenceOptionsByType(
                    projectId,
                    descriptor.ComponentVariantType),
            ValueKind.OptionToken
                when !string.IsNullOrWhiteSpace(
                    descriptor.ComponentVariantType) =>
                _designOptions.GetComponentVariantReferenceOptionsByType(
                    projectId,
                    descriptor.ComponentVariantType),
            ValueKind.OptionToken
                when EmbeddedComponentVariantType(descriptor.Id)
                    is { } componentType =>
                _designOptions.GetComponentVariantReferenceOptionsByType(
                    projectId,
                    componentType),
            ValueKind.PaletteColorToken
                or ValueKind.PaletteColorPair
                or ValueKind.PaletteColorAlphaPair =>
                _resourceOptions.GetPaletteColorOptions(projectId),
            ValueKind.TypographyStyle =>
            [
                new FieldOption("theme", "Theme"),
                .. _resourceOptions.GetProductionFontOptions(
                    projectId,
                    "text"),
            ],
            _ => descriptor.Options,
        };

    private static string? EmbeddedComponentVariantType(string fieldId)
    {
        if (!fieldId.EndsWith(
                ".variantReference",
                StringComparison.Ordinal))
        {
            return null;
        }

        var slotEditorFieldId = string.Concat(
            fieldId.AsSpan(
                0,
                fieldId.Length - ".variantReference".Length),
            ".editor");
        return EmbeddedComponentSlotCatalog.TryGet(
            slotEditorFieldId,
            out var slot)
                ? slot.EmbeddedComponentType
                : null;
    }
}
