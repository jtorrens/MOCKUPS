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
}
