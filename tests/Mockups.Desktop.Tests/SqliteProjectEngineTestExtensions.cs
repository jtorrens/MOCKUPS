using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteProjectEngineTestExtensions
{
    internal static IReadOnlyList<ProjectTreeNode> LoadProjectTree(
        this SqliteProjectEngine engine) =>
        engine.Navigation.LoadProjectTree();

    internal static ProjectTreeNode AddChild(
        this SqliteProjectEngine engine,
        ProjectTreeNode parent) =>
        engine.Children.AddChild(parent);

    internal static ProjectTreeNode AddImportedDevice(
        this SqliteProjectEngine engine,
        ProjectTreeNode devicesRoot,
        DeviceImportDraft device) =>
        engine.Children.AddImportedDevice(devicesRoot, device);

    internal static ProjectTreeNode AddShot(
        this SqliteProjectEngine engine,
        ProjectTreeNode episode,
        string actorId,
        int shotNumber) =>
        engine.Children.AddShot(
            episode,
            actorId,
            shotNumber);

    internal static ProjectTreeNode AddTheme(
        this SqliteProjectEngine engine,
        ProjectTreeNode themesRoot,
        string family) =>
        engine.Children.AddTheme(themesRoot, family);

    internal static int SuggestShotNumber(
        this SqliteProjectEngine engine,
        string episodeId) =>
        engine.Children.SuggestShotNumber(episodeId);

    internal static ProjectTreeNode Duplicate(
        this SqliteProjectEngine engine,
        ProjectTreeNode node) =>
        engine.NodeCommands.Duplicate(node);

    internal static void Delete(
        this SqliteProjectEngine engine,
        ProjectTreeNode node) =>
        engine.NodeCommands.Delete(node);

    internal static void UpdateNode(
        this SqliteProjectEngine engine,
        ProjectTreeNode node) =>
        engine.CoreFields.UpdateNode(node);

    internal static ProjectTreeNode RenameDirectNode(
        this SqliteProjectEngine engine,
        ProjectTreeNode node,
        string name) =>
        engine.CoreFields.RenameDirectNode(node, name);

    internal static AppSettings GetAppSettings(
        this SqliteProjectEngine engine,
        string appId) =>
        engine.Design.GetAppSettings(appId);

    internal static void UpdateAppField(
        this SqliteProjectEngine engine,
        string appId,
        string fieldId,
        string value) =>
        engine.Design.UpdateAppField(
            appId,
            fieldId,
            value);

    internal static string GetAppConfigFieldValue(
        this SqliteProjectEngine engine,
        string appId,
        string fieldId) =>
        engine.Design.GetAppConfigFieldValue(
            appId,
            fieldId);

    internal static string GetAppMetadataFieldValue(
        this SqliteProjectEngine engine,
        string appId,
        string fieldId) =>
        engine.Design.GetAppMetadataFieldValue(
            appId,
            fieldId);

    internal static ModuleSettings GetModuleSettings(
        this SqliteProjectEngine engine,
        string moduleId) =>
        engine.Design.GetModuleSettings(moduleId);

    internal static void UpdateModuleDesignPreviewJson(
        this SqliteProjectEngine engine,
        string moduleId,
        string designPreviewJson) =>
        engine.Design.UpdateModuleDesignPreviewJson(
            moduleId,
            designPreviewJson);

    internal static AppSettings GetModuleAppSettings(
        this SqliteProjectEngine engine,
        string moduleId) =>
        engine.Design.GetModuleAppSettings(moduleId);

    internal static string GetModuleConfigFieldValue(
        this SqliteProjectEngine engine,
        string moduleId,
        string fieldId) =>
        engine.Design.GetModuleConfigFieldValue(
            moduleId,
            fieldId);

    internal static void UpdateModuleField(
        this SqliteProjectEngine engine,
        string moduleId,
        string fieldId,
        string value) =>
        engine.Design.UpdateModuleField(
            moduleId,
            fieldId,
            value);

    internal static ShotSettings GetShotSettings(
        this SqliteProjectEngine engine,
        string shotId) =>
        engine.Production.GetShotSettings(shotId);

    internal static string GetShotRenderName(
        this SqliteProjectEngine engine,
        string shotId) =>
        engine.Production.GetShotRenderName(shotId);

    internal static ProjectSettings GetProjectSettings(
        this SqliteProjectEngine engine,
        string projectId) =>
        engine.Production.GetProjectSettings(projectId);

    internal static void UpdateProjectField(
        this SqliteProjectEngine engine,
        string projectId,
        string fieldId,
        string value) =>
        engine.Production.UpdateProjectField(
            projectId,
            fieldId,
            value);

    internal static EpisodeSettings GetEpisodeSettings(
        this SqliteProjectEngine engine,
        string episodeId) =>
        engine.Production.GetEpisodeSettings(episodeId);

    internal static void UpdateEpisodeField(
        this SqliteProjectEngine engine,
        string episodeId,
        string fieldId,
        string value) =>
        engine.Production.UpdateEpisodeField(
            episodeId,
            fieldId,
            value);

    internal static ProductionOutputShotPlan GetProductionOutputShotPlan(
        this SqliteProjectEngine engine,
        string shotId) =>
        engine.Production.GetProductionOutputShotPlan(shotId);
}
