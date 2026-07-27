using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteProjectTestContextModuleVariantExtensions
{
    internal static ModuleSettings GetModuleVariantSettings(
        this SqliteProjectTestContext engine,
        ProjectTreeNode variantNode) =>
        engine.RecordFields.GetModuleVariantSettings(variantNode);

    internal static ModuleSettings GetModuleInstanceVariantSettings(
        this SqliteProjectTestContext engine,
        string moduleInstanceId) =>
        engine.Production.GetModuleInstanceVariantSettings(
            moduleInstanceId);

    internal static string GetModuleInstanceEffectiveContractJson(
        this SqliteProjectTestContext engine,
        string moduleInstanceId) =>
        engine.Production.GetModuleInstanceEffectiveContractJson(
            moduleInstanceId);

    internal static string GetModuleInstanceVariantReference(
        this SqliteProjectTestContext engine,
        string moduleInstanceId) =>
        engine.RecordFields.GetModuleInstanceVariantReference(
            moduleInstanceId);

    internal static string GetModuleInstanceVariantName(
        this SqliteProjectTestContext engine,
        string moduleInstanceId) =>
        engine.Production.GetModuleInstanceVariantName(
            moduleInstanceId);

    internal static IReadOnlyList<FieldOption> GetModuleVariantOptions(
        this SqliteProjectTestContext engine,
        string moduleId) =>
        engine.RecordFields.GetModuleVariantOptions(moduleId);

    internal static void UpdateModuleInstanceVariant(
        this SqliteProjectTestContext engine,
        string moduleInstanceId,
        string reference) =>
        engine.RecordFields.UpdateModuleInstanceField(
            moduleInstanceId,
            "moduleInstance.variant",
            reference);

    internal static ProjectTreeNode SaveModuleVariant(
        this SqliteProjectTestContext engine,
        ProjectTreeNode sourceNode,
        string name) =>
        engine.NodeCommands.SaveModuleVariant(sourceNode, name);

    internal static ProjectTreeNode RenameModuleVariant(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node,
        string name) =>
        engine.Design.RenameModuleVariant(node, name);

    internal static void DeleteModuleVariant(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node) =>
        engine.NodeCommands.Delete(node);

    internal static ProjectTreeNode ToggleModuleVariantLock(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node) =>
        engine.NodeCommands.ToggleModuleVariantLock(node);

    internal static void ReplaceModuleVariantConfig(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node,
        string configJson) =>
        engine.NodeCommands.ReplaceModuleVariantConfig(
            node,
            configJson);

    internal static void UpdateModuleVariantField(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node,
        string fieldId,
        string value) =>
        engine.RecordFields.UpdateModuleVariantField(
            node,
            fieldId,
            value);

    internal static string GetModuleVariantConfigFieldValue(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node,
        string fieldId) =>
        engine.RecordFields.GetModuleVariantConfigFieldValue(
            node,
            fieldId);
}
