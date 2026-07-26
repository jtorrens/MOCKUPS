using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorNavigationMetadata
{
    public static EditorWorkspaceScope WorkspaceScope(ProjectTreeNodeKind kind)
        => EditorWorkspacePolicy.Scope(kind);

    public static bool IsWorkspaceSectionRoot(ProjectTreeNodeKind kind)
        => EditorWorkspacePolicy.IsSectionRoot(kind);

    public static int WorkspaceOrder(ProjectTreeNodeKind kind)
        => EditorWorkspacePolicy.SectionOrder(kind);

    public static bool IsTopLevelSection(ProjectTreeNode node)
    {
        return node.Kind is ProjectTreeNodeKind.AppsRoot
            or ProjectTreeNodeKind.RenderQueueRoot
            or ProjectTreeNodeKind.ProductionDataRoot
            or ProjectTreeNodeKind.SystemDataRoot;
    }

    public static int RootOrder(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.AppsRoot => 10,
            ProjectTreeNodeKind.EpisodesRoot => 20,
            ProjectTreeNodeKind.RenderQueueRoot => 30,
            ProjectTreeNodeKind.ProductionDataRoot => 40,
            ProjectTreeNodeKind.SystemDataRoot => 50,
            _ => 100,
        };
    }

    public static string SectionIcon(ProjectTreeNode sectionRoot)
    {
        return EditorIcons.ForNavigationTreeNode(sectionRoot);
    }

    public static string Title(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.Project => "Project",
            ProjectTreeNodeKind.RenderQueueRoot => "Render Queue",
            ProjectTreeNodeKind.ProductionDataRoot => "Production data",
            ProjectTreeNodeKind.SystemDataRoot => "System data",
            _ => node.Name,
        };
    }

    public static string Subtitle(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.Project => "Episodes, shots, screens and modules",
            ProjectTreeNodeKind.AppsRoot => "Apps and module defaults",
            ProjectTreeNodeKind.RenderQueueRoot => "Local render jobs and history",
            ProjectTreeNodeKind.ProductionDataRoot => "Actors, devices and production fonts",
            ProjectTreeNodeKind.SystemDataRoot => "Themes, icon sets, component variants, palette and media",
            ProjectTreeNodeKind.ProductionFontsRoot => "Approved production font families",
            ProjectTreeNodeKind.IconThemesRoot => "Semantic icon tokens shared by every set",
            ProjectTreeNodeKind.ComponentClassesRoot => "Reusable component defaults",
            ProjectTreeNodeKind.ComponentClassGroup => "Component class group",
            ProjectTreeNodeKind.ThemesRoot => "Visual theme definitions",
            _ => node.Notes,
        };
    }

    public static bool CollapseSiblingsWhenOpenedBySelection(ProjectTreeNode node)
    {
        return node.Kind is ProjectTreeNodeKind.ComponentClass;
    }

    public static bool ExpandChildrenWhenOpened(ProjectTreeNode node)
    {
        return node.Kind is ProjectTreeNodeKind.App
            or ProjectTreeNodeKind.Episode
            or ProjectTreeNodeKind.Shot;
    }

    public static bool IsUsed(ProjectTreeNode node)
    {
        return node.Kind == ProjectTreeNodeKind.ModuleInstance || node.IsUsed;
    }

    public static string AddChildLabel(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClassGroup => "Add component",
            ProjectTreeNodeKind.ComponentClass => "Add variant",
            ProjectTreeNodeKind.EpisodesRoot => "Add episode",
            ProjectTreeNodeKind.Episode => "Add shot",
            ProjectTreeNodeKind.Shot => "Add screen",
            ProjectTreeNodeKind.PaletteRoot => "Add palette color",
            ProjectTreeNodeKind.IconThemesRoot => "Add icon theme",
            ProjectTreeNodeKind.DevicesRoot => "Add device",
            ProjectTreeNodeKind.ActorsRoot => "Add actor",
            ProjectTreeNodeKind.ThemesRoot => "Add theme",
            ProjectTreeNodeKind.ProductionFontsRoot => "Add production font",
            _ => "Add child",
        };
    }

    public static string HierarchicalIcon(ProjectTreeNode node)
    {
        return EditorIcons.ForNavigationTreeNode(node);
    }
}
