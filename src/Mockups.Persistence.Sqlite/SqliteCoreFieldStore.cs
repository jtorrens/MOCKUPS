using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteCoreFieldStore
{
    private readonly SqliteProjectContext _context;
    private readonly SqliteDesignOwner _design;
    private readonly SqliteProductionOwner _production;
    private readonly SqliteResourceOwner _resources;

    internal SqliteCoreFieldStore(
        SqliteProjectContext context,
        SqliteDesignOwner design,
        SqliteProductionOwner production,
        SqliteResourceOwner resources)
    {
        _context = context;
        _design = design;
        _production = production;
        _resources = resources;
    }

    internal void UpdateNode(ProjectTreeNode node)
    {
        using var connection = _context.OpenConnection();
        switch (node.Kind)
        {
            case ProjectTreeNodeKind.Project:
                _production.ProjectEpisodeRepository.UpdateProjectNode(
                    connection,
                    node.Id,
                    node.Name,
                    node.Notes);
                return;
            case ProjectTreeNodeKind.Episode:
                _production.ProjectEpisodeRepository.UpdateEpisodeNode(
                    connection,
                    node.Id,
                    node.Name,
                    node.Notes);
                return;
            case ProjectTreeNodeKind.PaletteColor:
                _resources.PaletteRepository.UpdateNode(
                    connection,
                    node.Id,
                    node.Name,
                    node.Notes);
                return;
            case ProjectTreeNodeKind.Device:
                _resources.DeviceRepository.Rename(
                    connection,
                    node.Id,
                    node.Name);
                return;
            case ProjectTreeNodeKind.Actor:
                _resources.ActorRepository.Rename(
                    connection,
                    node.Id,
                    node.Name);
                return;
            case ProjectTreeNodeKind.Theme:
                _resources.ThemeRepository.Rename(
                    connection,
                    node.Id,
                    node.Name);
                return;
            case ProjectTreeNodeKind.ProductionFont:
                _resources.ProductionFontRepository.Rename(
                    connection,
                    node.Id,
                    node.Name);
                return;
            case ProjectTreeNodeKind.App:
                _design.AppModuleRepository.UpdateAppNode(
                    connection,
                    node.Id,
                    node.Name,
                    node.Notes);
                return;
            case ProjectTreeNodeKind.Module:
                _design.AppModuleRepository.UpdateModuleNode(
                    connection,
                    node.Id,
                    node.Name,
                    node.Notes);
                return;
            case ProjectTreeNodeKind.ComponentClass:
                _design.ComponentClassRepository.UpdateNode(
                    connection,
                    node.Id,
                    node.Name,
                    node.Notes);
                return;
            case ProjectTreeNodeKind.Shot:
                _production.ShotRepository.UpdateNode(
                    connection,
                    node.Id,
                    node.Name,
                    node.Notes);
                return;
            case ProjectTreeNodeKind.IconTheme:
                RenameIconTheme(connection, node);
                return;
        }
    }

    internal ProjectTreeNode RenameDirectNode(
        ProjectTreeNode node,
        string name) =>
        node.Kind switch
        {
            ProjectTreeNodeKind.Project =>
                RenameStoredNode(node, name),
            ProjectTreeNodeKind.App =>
                RenameApp(node, name),
            ProjectTreeNodeKind.ComponentClass =>
                _design.RenameComponentClass(node, name),
            ProjectTreeNodeKind.ComponentVariant =>
                _design.RenameComponentVariant(node, name),
            ProjectTreeNodeKind.Module =>
                _design.RenameModuleClass(node, name),
            ProjectTreeNodeKind.ModuleVariant =>
                _design.RenameModuleVariant(node, name),
            ProjectTreeNodeKind.ModuleInstance =>
                _production.RenameModuleInstance(node, name),
            ProjectTreeNodeKind.Episode
                or ProjectTreeNodeKind.Shot
                or ProjectTreeNodeKind.PaletteColor
                or ProjectTreeNodeKind.IconTheme
                or ProjectTreeNodeKind.Device
                or ProjectTreeNodeKind.Actor
                or ProjectTreeNodeKind.Theme
                or ProjectTreeNodeKind.ProductionFont =>
                RenameStoredNode(node, name),
            _ => throw new InvalidOperationException(
                $"Cannot rename {node.Kind} directly."),
        };

    private ProjectTreeNode RenameStoredNode(
        ProjectTreeNode node,
        string name)
    {
        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException(
                $"{node.Kind} name cannot be empty.");
        }

        var renamed = new ProjectTreeNode(
            node.Kind,
            node.Id,
            nextName,
            node.Notes,
            node.RecordClassId,
            node.Parent,
            node.ColorHex,
            node.IsUsed,
            node.IsProtected,
            node.IsLocked);
        UpdateNode(renamed);
        return renamed;
    }

    private ProjectTreeNode RenameApp(
        ProjectTreeNode node,
        string name)
    {
        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException(
                "App name cannot be empty.");
        }

        using var connection = _context.OpenConnection();
        _design.AppModuleRepository.RenameApp(
            connection,
            node.Id,
            nextName);
        return new ProjectTreeNode(
            ProjectTreeNodeKind.App,
            node.Id,
            nextName,
            node.Notes,
            node.RecordClassId,
            node.Parent,
            isUsed: node.IsUsed,
            isProtected: node.IsProtected,
            isLocked: node.IsLocked);
    }

    private void RenameIconTheme(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        ProjectTreeNode node)
    {
        var row = _resources.IconThemeRepository.Get(
            connection,
            node.Id);
        var renamedAssets = _resources.RenameIconThemeAssets(
            connection,
            row,
            node.Name);
        var metadata = SqliteResourceOwner.IconThemeMetadata(
            _resources.IconThemeAssetDirectory(
                connection,
                row.ProjectId,
                renamedAssets.AssetRoot),
            renamedAssets.Name);
        _resources.IconThemeRepository.UpdateIdentity(
            connection,
            node.Id,
            renamedAssets.Name,
            renamedAssets.AssetRoot,
            metadata.ToJsonString());
    }
}
