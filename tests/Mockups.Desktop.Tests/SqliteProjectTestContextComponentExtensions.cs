using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteProjectTestContextComponentExtensions
{
    internal static ComponentClassSettings GetComponentClassSettings(
        this SqliteProjectTestContext engine,
        string componentClassId) =>
        engine.ComponentDocuments.GetComponentClassSettings(
            componentClassId);

    internal static ComponentClassSettings GetComponentVariantSettings(
        this SqliteProjectTestContext engine,
        ProjectTreeNode variantNode) =>
        engine.ComponentDocuments.GetComponentVariantSettings(
            variantNode);

    internal static void UpdateComponentClassDesignPreviewJson(
        this SqliteProjectTestContext engine,
        string componentClassId,
        string designPreviewJson) =>
        engine.Design.UpdateComponentClassDesignPreviewJson(
            componentClassId,
            designPreviewJson);

    internal static FieldValue CreateComponentClassFieldValue(
        this SqliteProjectTestContext engine,
        string componentClassId,
        string fieldId) =>
        engine.ComponentDocuments.CreateComponentClassFieldValue(
            componentClassId,
            fieldId);

    internal static FieldValue CreateComponentVariantFieldValue(
        this SqliteProjectTestContext engine,
        ProjectTreeNode variantNode,
        string fieldId) =>
        engine.ComponentDocuments.CreateComponentVariantFieldValue(
            variantNode,
            fieldId);

    internal static FieldValue CreateRuntimeComponentOverrideFieldValue(
        this SqliteProjectTestContext engine,
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
        this SqliteProjectTestContext engine,
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
        this SqliteProjectTestContext engine,
        JsonObject overrides,
        string fieldId,
        string value) =>
        engine.ComponentDocuments.UpdateRuntimeComponentOverride(
            overrides,
            fieldId,
            value);

    internal static void UpdateRuntimeComponentOverride(
        this SqliteProjectTestContext engine,
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
        this SqliteProjectTestContext engine,
        string componentClassId,
        string fieldId,
        string value) =>
        engine.ComponentDocuments.UpdateComponentClassField(
            componentClassId,
            fieldId,
            value);

    internal static void UpdateComponentVariantField(
        this SqliteProjectTestContext engine,
        ProjectTreeNode variantNode,
        string fieldId,
        string value) =>
        engine.ComponentDocuments.UpdateComponentVariantField(
            variantNode,
            fieldId,
            value);

    internal static FieldValue CreateEmbeddedComponentFieldValue(
        this SqliteProjectTestContext engine,
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
        this SqliteProjectTestContext engine,
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId) =>
        engine.ComponentDocuments.CreateEmbeddedComponentFieldValue(
            componentClassId,
            slots,
            embeddedFieldId);

    internal static FieldValue CreateEmbeddedComponentFieldValue(
        this SqliteProjectTestContext engine,
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId) =>
        engine.ComponentDocuments.CreateEmbeddedComponentFieldValue(
            ownerNode,
            slots,
            embeddedFieldId);

    internal static void UpdateEmbeddedComponentField(
        this SqliteProjectTestContext engine,
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
        this SqliteProjectTestContext engine,
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
        this SqliteProjectTestContext engine,
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value) =>
        engine.ComponentDocuments.UpdateEmbeddedComponentField(
            ownerNode,
            slots,
            embeddedFieldId,
            value);

    internal static ProjectTreeNode SaveComponentVariant(
        this SqliteProjectTestContext engine,
        ProjectTreeNode sourceNode,
        string name) =>
        engine.NodeCommands.SaveComponentVariant(sourceNode, name);

    internal static ProjectTreeNode RenameComponentVariant(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node,
        string name) =>
        engine.Design.RenameComponentVariant(node, name);

    internal static ProjectTreeNode ToggleComponentVariantLock(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node) =>
        engine.NodeCommands.ToggleComponentVariantLock(node);

    internal static void ReplaceComponentVariantConfig(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node,
        string configJson) =>
        engine.NodeCommands.ReplaceComponentVariantConfig(
            node,
            configJson);

    internal static IReadOnlyList<ComponentVariantReferenceUsage>
        GetComponentVariantReferenceUsageDetails(
            this SqliteProjectTestContext engine,
            ProjectTreeNode node) =>
        engine.ComponentDocuments
            .GetComponentVariantReferenceUsageDetails(node);
}
