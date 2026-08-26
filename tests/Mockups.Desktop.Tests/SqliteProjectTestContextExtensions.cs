using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteProjectTestContextExtensions
{
    internal static IReadOnlyList<ProjectTreeNode> LoadProjectTree(
        this SqliteProjectTestContext engine) =>
        engine.Navigation.LoadProjectTree();

    internal static ProjectTreeNode AddChild(
        this SqliteProjectTestContext engine,
        ProjectTreeNode parent) =>
        engine.Children.AddChild(parent);

    internal static ProjectTreeNode AddImportedDevice(
        this SqliteProjectTestContext engine,
        ProjectTreeNode devicesRoot,
        DeviceImportDraft device) =>
        engine.Children.AddImportedDevice(devicesRoot, device);

    internal static ProjectTreeNode AddShot(
        this SqliteProjectTestContext engine,
        ProjectTreeNode episode,
        string actorId,
        int shotNumber) =>
        engine.Children.AddShot(
            episode,
            actorId,
            shotNumber);

    internal static ProjectTreeNode AddTheme(
        this SqliteProjectTestContext engine,
        ProjectTreeNode themesRoot,
        string family) =>
        engine.Children.AddTheme(themesRoot, family);

    internal static int SuggestShotNumber(
        this SqliteProjectTestContext engine,
        string episodeId) =>
        engine.Children.SuggestShotNumber(episodeId);

    internal static ProjectTreeNode Duplicate(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node) =>
        engine.NodeCommands.Duplicate(node);

    internal static void Delete(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node) =>
        engine.NodeCommands.Delete(node);

    internal static void UpdateNode(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node) =>
        engine.CoreFields.UpdateNode(node);

    internal static ProjectTreeNode RenameDirectNode(
        this SqliteProjectTestContext engine,
        ProjectTreeNode node,
        string name) =>
        engine.CoreFields.RenameDirectNode(node, name);

    internal static AppSettings GetAppSettings(
        this SqliteProjectTestContext engine,
        string appId) =>
        engine.Design.GetAppSettings(appId);

    internal static void UpdateAppField(
        this SqliteProjectTestContext engine,
        string appId,
        string fieldId,
        string value) =>
        engine.Design.UpdateAppField(
            appId,
            fieldId,
            value);

    internal static string GetAppConfigFieldValue(
        this SqliteProjectTestContext engine,
        string appId,
        string fieldId) =>
        engine.Design.GetAppConfigFieldValue(
            appId,
            fieldId);

    internal static string GetAppMetadataFieldValue(
        this SqliteProjectTestContext engine,
        string appId,
        string fieldId) =>
        engine.Design.GetAppMetadataFieldValue(
            appId,
            fieldId);

    internal static ModuleSettings GetModuleSettings(
        this SqliteProjectTestContext engine,
        string moduleId) =>
        engine.Design.GetModuleSettings(moduleId);

    internal static void UpdateModuleDesignPreviewJson(
        this SqliteProjectTestContext engine,
        string moduleId,
        string designPreviewJson) =>
        engine.Design.UpdateModuleDesignPreviewJson(
            moduleId,
            designPreviewJson);

    internal static AppSettings GetModuleAppSettings(
        this SqliteProjectTestContext engine,
        string moduleId) =>
        engine.Design.GetModuleAppSettings(moduleId);

    internal static string GetModuleConfigFieldValue(
        this SqliteProjectTestContext engine,
        string moduleId,
        string fieldId) =>
        engine.Design.GetModuleConfigFieldValue(
            moduleId,
            fieldId);

    internal static void UpdateModuleField(
        this SqliteProjectTestContext engine,
        string moduleId,
        string fieldId,
        string value) =>
        engine.Design.UpdateModuleField(
            moduleId,
            fieldId,
            value);

    internal static void UpdateShotField(
        this SqliteProjectTestContext engine,
        string shotId,
        string fieldId,
        string value) =>
        engine.ProductionRecordFields.UpdateShotField(
            shotId,
            fieldId,
            value);

    internal static ShotSettings GetShotSettings(
        this SqliteProjectTestContext engine,
        string shotId) =>
        engine.Production.GetShotSettings(shotId);

    internal static ProjectSettings GetProjectSettings(
        this SqliteProjectTestContext engine,
        string projectId) =>
        engine.Production.GetProjectSettings(projectId);

    internal static void UpdateProjectField(
        this SqliteProjectTestContext engine,
        string projectId,
        string fieldId,
        string value) =>
        engine.Production.UpdateProjectField(
            projectId,
            fieldId,
            value);

    internal static EpisodeSettings GetEpisodeSettings(
        this SqliteProjectTestContext engine,
        string episodeId) =>
        engine.Production.GetEpisodeSettings(episodeId);

    internal static void UpdateEpisodeField(
        this SqliteProjectTestContext engine,
        string episodeId,
        string fieldId,
        string value) =>
        engine.Production.UpdateEpisodeField(
            episodeId,
            fieldId,
            value);

    internal static ProductionOutputShotPlan GetProductionOutputShotPlan(
        this SqliteProjectTestContext engine,
        string shotId) =>
        ProductionOutputContract.ResolveManual(
            engine.Production.GetProductionOutputShotContext(shotId));

    internal static ProductionOutputShotContext GetProductionOutputShotContext(
        this SqliteProjectTestContext engine,
        string shotId) =>
        engine.Production.GetProductionOutputShotContext(shotId);
}
