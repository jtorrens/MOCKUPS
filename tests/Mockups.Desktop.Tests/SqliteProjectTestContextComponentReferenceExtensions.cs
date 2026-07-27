using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteProjectTestContextComponentReferenceExtensions
{
    internal static IReadOnlyList<EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        this SqliteProjectTestContext engine,

        string projectId,
        string componentType,
        string? excludedComponentClassId = null) =>
        engine.Design.GetEmbeddedComponentUsages(
            projectId,
            componentType,
            excludedComponentClassId);

    internal static string GetEmbeddedComponentVariantName(
        this SqliteProjectTestContext engine,

        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        engine.Design.GetEmbeddedComponentVariantName(
            componentClassId,
            slots);

    internal static string GetEmbeddedComponentVariantName(
        this SqliteProjectTestContext engine,

        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        engine.Design.GetEmbeddedComponentVariantName(
            ownerNode,
            slots);

    internal static string GetComponentClassBaseConfigsJson(
        this SqliteProjectTestContext engine,
        string projectId) =>
        engine.Design.GetComponentClassBaseConfigsJson(projectId);

    internal static string ValidateComponentVariantReferencesForPreview(
        this SqliteProjectTestContext engine,

        string projectId,
        string configJson) =>
        engine.Design.ValidateComponentVariantReferencesForPreview(
            projectId,
            configJson);

    internal static IReadOnlyList<FieldOption> GetComponentClassOptionsByType(
        this SqliteProjectTestContext engine,

        string projectId,
        string componentType,
        bool includeNone = false) =>
        engine.Design.GetComponentClassOptionsByType(
            projectId,
            componentType,
            includeNone);

    internal static IReadOnlyList<FieldOption>
        GetComponentVariantReferenceOptionsByType(
            this SqliteProjectTestContext engine,
            string projectId,
            string componentType,
            bool includeNone = false) =>
        engine.Design.GetComponentVariantReferenceOptionsByType(
            projectId,
            componentType,
            includeNone);

    internal static IReadOnlyList<FieldOption>
        GetStatusBarComponentVariantOptions(
            this SqliteProjectTestContext engine,
            string projectId) =>
        engine.Design.GetStatusBarComponentVariantOptions(projectId);

    internal static IReadOnlyList<FieldOption>
        GetNavigationBarComponentVariantOptions(
            this SqliteProjectTestContext engine,
            string projectId) =>
        engine.Design.GetNavigationBarComponentVariantOptions(projectId);

    internal static IReadOnlyList<FieldOption> GetComponentVariantReferenceOptions(
        this SqliteProjectTestContext engine,

        string projectId,
        string componentTypeSelector,
        bool includeNone = false) =>
        engine.Design.GetComponentVariantReferenceOptions(
            projectId,
            componentTypeSelector,
            includeNone);

    internal static JsonObject GetComponentVariantRuntimeInputs(
        this SqliteProjectTestContext engine,

        string variantReference) =>
        engine.Design.GetComponentVariantRuntimeInputs(variantReference);

    internal static JsonObject GetComponentVariantRuntimeContract(
        this SqliteProjectTestContext engine,

        string variantReference) =>
        engine.Design.GetComponentVariantRuntimeContract(
            variantReference);

    internal static IReadOnlyList<ComponentInputBindingDefinition>
        GetComponentVariantRuntimeInputBindings(
            this SqliteProjectTestContext engine,
            string variantReference) =>
        engine.Design.GetComponentVariantRuntimeInputBindings(
            variantReference);

    internal static IReadOnlyList<RuntimeInputCollectionDefinition>
        GetComponentVariantRuntimeCollections(
            this SqliteProjectTestContext engine,
            string variantReference) =>
        engine.Design.GetComponentVariantRuntimeCollections(
            variantReference);

    internal static JsonObject GetComponentVariantConfig(
        this SqliteProjectTestContext engine,

        string variantReference) =>
        engine.Design.GetComponentVariantConfig(variantReference);

    internal static ComponentVariantSelectionSettings
        GetComponentVariantSelectionSettings(
            this SqliteProjectTestContext engine,
            string variantReference) =>
        engine.Design.GetComponentVariantSelectionSettings(
            variantReference);

    internal static string GetRuntimeComponentVariantName(
        this SqliteProjectTestContext engine,

        string variantReference,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        engine.Design.GetRuntimeComponentVariantName(
            variantReference,
            overrides,
            slots);

    internal static string ValidateComponentVariantReferenceValue(
        this SqliteProjectTestContext engine,

        string projectId,
        string componentType,
        string reference,
        bool allowEmpty = false) =>
        engine.Design.ValidateComponentVariantReferenceValue(
            projectId,
            componentType,
            reference,
            allowEmpty);

}
