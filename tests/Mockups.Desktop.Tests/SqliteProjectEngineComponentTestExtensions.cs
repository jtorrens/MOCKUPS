using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteProjectEngineComponentTestExtensions
{
    internal static ComponentClassSettings GetComponentClassSettings(
        this SqliteProjectEngine engine,
        string componentClassId) =>
        engine.ComponentDocuments.GetComponentClassSettings(
            componentClassId);

    internal static ComponentClassSettings GetComponentVariantSettings(
        this SqliteProjectEngine engine,
        ProjectTreeNode variantNode) =>
        engine.ComponentDocuments.GetComponentVariantSettings(
            variantNode);

    internal static void UpdateComponentClassDesignPreviewJson(
        this SqliteProjectEngine engine,
        string componentClassId,
        string designPreviewJson) =>
        engine.Design.UpdateComponentClassDesignPreviewJson(
            componentClassId,
            designPreviewJson);

    internal static FieldValue CreateComponentClassFieldValue(
        this SqliteProjectEngine engine,
        string componentClassId,
        string fieldId) =>
        engine.ComponentDocuments.CreateComponentClassFieldValue(
            componentClassId,
            fieldId);

    internal static FieldValue CreateComponentVariantFieldValue(
        this SqliteProjectEngine engine,
        ProjectTreeNode variantNode,
        string fieldId) =>
        engine.ComponentDocuments.CreateComponentVariantFieldValue(
            variantNode,
            fieldId);

    internal static FieldValue CreateRuntimeComponentOverrideFieldValue(
        this SqliteProjectEngine engine,
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        string fieldId) =>
        engine.ComponentDocuments
            .CreateRuntimeComponentOverrideFieldValue(
                projectId,
                baseConfigJson,
                overrides,
                fieldId);

    internal static FieldValue CreateRuntimeComponentOverrideFieldValue(
        this SqliteProjectEngine engine,
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId) =>
        engine.ComponentDocuments
            .CreateRuntimeComponentOverrideFieldValue(
                projectId,
                baseConfigJson,
                overrides,
                slots,
                fieldId);

    internal static void UpdateRuntimeComponentOverride(
        this SqliteProjectEngine engine,
        JsonObject overrides,
        string fieldId,
        string value) =>
        engine.ComponentDocuments.UpdateRuntimeComponentOverride(
            overrides,
            fieldId,
            value);

    internal static void UpdateRuntimeComponentOverride(
        this SqliteProjectEngine engine,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value) =>
        engine.ComponentDocuments.UpdateRuntimeComponentOverride(
            overrides,
            slots,
            fieldId,
            value);

    internal static void UpdateComponentClassField(
        this SqliteProjectEngine engine,
        string componentClassId,
        string fieldId,
        string value) =>
        engine.ComponentDocuments.UpdateComponentClassField(
            componentClassId,
            fieldId,
            value);

    internal static void UpdateComponentVariantField(
        this SqliteProjectEngine engine,
        ProjectTreeNode variantNode,
        string fieldId,
        string value) =>
        engine.ComponentDocuments.UpdateComponentVariantField(
            variantNode,
            fieldId,
            value);

    internal static FieldValue CreateEmbeddedComponentFieldValue(
        this SqliteProjectEngine engine,
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId) =>
        engine.ComponentDocuments.CreateEmbeddedComponentFieldValue(
            componentClassId,
            slotFieldId,
            embeddedComponentType,
            embeddedFieldId);

    internal static FieldValue CreateEmbeddedComponentFieldValue(
        this SqliteProjectEngine engine,
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId) =>
        engine.ComponentDocuments.CreateEmbeddedComponentFieldValue(
            componentClassId,
            slots,
            embeddedFieldId);

    internal static FieldValue CreateEmbeddedComponentFieldValue(
        this SqliteProjectEngine engine,
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId) =>
        engine.ComponentDocuments.CreateEmbeddedComponentFieldValue(
            ownerNode,
            slots,
            embeddedFieldId);

    internal static void UpdateEmbeddedComponentField(
        this SqliteProjectEngine engine,
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId,
        string value) =>
        engine.ComponentDocuments.UpdateEmbeddedComponentField(
            componentClassId,
            slotFieldId,
            embeddedComponentType,
            embeddedFieldId,
            value);

    internal static void UpdateEmbeddedComponentField(
        this SqliteProjectEngine engine,
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value) =>
        engine.ComponentDocuments.UpdateEmbeddedComponentField(
            componentClassId,
            slots,
            embeddedFieldId,
            value);

    internal static void UpdateEmbeddedComponentField(
        this SqliteProjectEngine engine,
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value) =>
        engine.ComponentDocuments.UpdateEmbeddedComponentField(
            ownerNode,
            slots,
            embeddedFieldId,
            value);
}
