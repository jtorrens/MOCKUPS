using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public IReadOnlyList<EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        string projectId,
        string componentType,
        string? excludedComponentClassId = null) =>
        _designOwner.GetEmbeddedComponentUsages(
            projectId,
            componentType,
            excludedComponentClassId);

    public string GetEmbeddedComponentVariantName(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        _designOwner.GetEmbeddedComponentVariantName(
            componentClassId,
            slots);

    public string GetEmbeddedComponentVariantName(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        _designOwner.GetEmbeddedComponentVariantName(
            ownerNode,
            slots);

    public string GetComponentClassBaseConfigsJson(string projectId) =>
        _designOwner.GetComponentClassBaseConfigsJson(projectId);

    public string ValidateComponentVariantReferencesForPreview(
        string projectId,
        string configJson) =>
        _designOwner.ValidateComponentVariantReferencesForPreview(
            projectId,
            configJson);

    public IReadOnlyList<FieldOption> GetComponentClassOptionsByType(
        string projectId,
        string componentType,
        bool includeNone = false) =>
        _designOwner.GetComponentClassOptionsByType(
            projectId,
            componentType,
            includeNone);

    public IReadOnlyList<FieldOption>
        GetComponentVariantReferenceOptionsByType(
            string projectId,
            string componentType,
            bool includeNone = false) =>
        _designOwner.GetComponentVariantReferenceOptionsByType(
            projectId,
            componentType,
            includeNone);

    public IReadOnlyList<FieldOption>
        GetStatusBarComponentVariantOptions(string projectId) =>
        _designOwner.GetStatusBarComponentVariantOptions(projectId);

    public IReadOnlyList<FieldOption>
        GetNavigationBarComponentVariantOptions(string projectId) =>
        _designOwner.GetNavigationBarComponentVariantOptions(projectId);

    public IReadOnlyList<FieldOption> GetComponentVariantReferenceOptions(
        string projectId,
        string componentTypeSelector,
        bool includeNone = false) =>
        _designOwner.GetComponentVariantReferenceOptions(
            projectId,
            componentTypeSelector,
            includeNone);

    public JsonObject GetComponentVariantRuntimeInputs(
        string variantReference) =>
        _designOwner.GetComponentVariantRuntimeInputs(variantReference);

    public JsonObject GetComponentVariantRuntimeContract(
        string variantReference) =>
        _designOwner.GetComponentVariantRuntimeContract(
            variantReference);

    public IReadOnlyList<ComponentInputBindingDefinition>
        GetComponentVariantRuntimeInputBindings(
            string variantReference) =>
        _designOwner.GetComponentVariantRuntimeInputBindings(
            variantReference);

    public IReadOnlyList<RuntimeInputCollectionDefinition>
        GetComponentVariantRuntimeCollections(
            string variantReference) =>
        _designOwner.GetComponentVariantRuntimeCollections(
            variantReference);

    public JsonObject GetComponentVariantConfig(
        string variantReference) =>
        _designOwner.GetComponentVariantConfig(variantReference);

    public ComponentVariantSelectionSettings
        GetComponentVariantSelectionSettings(
            string variantReference) =>
        _designOwner.GetComponentVariantSelectionSettings(
            variantReference);

    public string GetRuntimeComponentVariantName(
        string variantReference,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        _designOwner.GetRuntimeComponentVariantName(
            variantReference,
            overrides,
            slots);

    public string ValidateComponentVariantReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty = false) =>
        _designOwner.ValidateComponentVariantReferenceValue(
            projectId,
            componentType,
            reference,
            allowEmpty);

    private IReadOnlyList<FieldOption>? ComponentClassFieldOptions(
        string projectId,
        ComponentClassFieldDescriptor descriptor) =>
        descriptor.ValueKind switch
        {
            ValueKind.EmbeddedComponent =>
                _designOwner.EmbeddedComponentOptions(
                    projectId,
                    descriptor.DefaultValue),
            ValueKind.ComponentVariant
                when EmbeddedComponentSlotCatalog.TryGet(
                    descriptor.Id,
                    out var slot) =>
                _designOwner.GetComponentVariantReferenceOptionsByType(
                    projectId,
                    slot.EmbeddedComponentType),
            ValueKind.ComponentVariant
                or ValueKind.ComponentVariantSlot
                when !string.IsNullOrWhiteSpace(
                    descriptor.ComponentVariantType) =>
                _designOwner.GetComponentVariantReferenceOptionsByType(
                    projectId,
                    descriptor.ComponentVariantType),
            ValueKind.OptionToken
                when !string.IsNullOrWhiteSpace(
                    descriptor.ComponentVariantType) =>
                _designOwner.GetComponentVariantReferenceOptionsByType(
                    projectId,
                    descriptor.ComponentVariantType),
            ValueKind.OptionToken
                when EmbeddedComponentVariantType(descriptor.Id)
                    is { } componentType =>
                _designOwner.GetComponentVariantReferenceOptionsByType(
                    projectId,
                    componentType),
            ValueKind.PaletteColorToken
                or ValueKind.PaletteColorPair
                or ValueKind.PaletteColorAlphaPair =>
                GetPaletteColorOptions(projectId),
            ValueKind.TypographyStyle =>
            [
                new FieldOption("theme", "Theme"),
                .. GetProductionFontOptions(projectId, "text"),
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
