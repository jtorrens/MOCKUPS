using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteProjectEngineModuleVariantTestExtensions
{
    internal static ModuleSettings GetModuleVariantSettings(
        this SqliteProjectEngine engine,
        ProjectTreeNode variantNode) =>
        engine.RecordFields.GetModuleVariantSettings(variantNode);

    internal static ModuleSettings GetModuleInstanceVariantSettings(
        this SqliteProjectEngine engine,
        string moduleInstanceId) =>
        engine.Production.GetModuleInstanceVariantSettings(
            moduleInstanceId);

    internal static string GetModuleInstanceEffectiveContractJson(
        this SqliteProjectEngine engine,
        string moduleInstanceId) =>
        engine.Production.GetModuleInstanceEffectiveContractJson(
            moduleInstanceId);

    internal static string GetModuleInstanceVariantReference(
        this SqliteProjectEngine engine,
        string moduleInstanceId) =>
        engine.RecordFields.GetModuleInstanceVariantReference(
            moduleInstanceId);

    internal static string GetModuleInstanceVariantName(
        this SqliteProjectEngine engine,
        string moduleInstanceId) =>
        engine.Production.GetModuleInstanceVariantName(
            moduleInstanceId);

    internal static IReadOnlyList<FieldOption> GetModuleVariantOptions(
        this SqliteProjectEngine engine,
        string moduleId) =>
        engine.RecordFields.GetModuleVariantOptions(moduleId);

    internal static void UpdateModuleInstanceVariant(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string reference) =>
        engine.RecordFields.UpdateModuleInstanceField(
            moduleInstanceId,
            "moduleInstance.variant",
            reference);

    internal static ProjectTreeNode SaveModuleVariant(
        this SqliteProjectEngine engine,
        ProjectTreeNode sourceNode,
        string name) =>
        engine.NodeCommands.SaveModuleVariant(sourceNode, name);

    internal static ProjectTreeNode RenameModuleVariant(
        this SqliteProjectEngine engine,
        ProjectTreeNode node,
        string name) =>
        engine.Design.RenameModuleVariant(node, name);

    internal static void DeleteModuleVariant(
        this SqliteProjectEngine engine,
        ProjectTreeNode node) =>
        engine.NodeCommands.Delete(node);

    internal static ProjectTreeNode ToggleModuleVariantLock(
        this SqliteProjectEngine engine,
        ProjectTreeNode node) =>
        engine.NodeCommands.ToggleModuleVariantLock(node);

    internal static void ReplaceModuleVariantConfig(
        this SqliteProjectEngine engine,
        ProjectTreeNode node,
        string configJson) =>
        engine.NodeCommands.ReplaceModuleVariantConfig(
            node,
            configJson);

    internal static void UpdateModuleVariantField(
        this SqliteProjectEngine engine,
        ProjectTreeNode node,
        string fieldId,
        string value) =>
        engine.RecordFields.UpdateModuleVariantField(
            node,
            fieldId,
            value);

    internal static string GetModuleVariantConfigFieldValue(
        this SqliteProjectEngine engine,
        ProjectTreeNode node,
        string fieldId) =>
        engine.RecordFields.GetModuleVariantConfigFieldValue(
            node,
            fieldId);
}
