using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorNavigationMetadata
{
    public const string ProjectsRootId = "navigation:projects";

    public static EditorWorkspaceScope WorkspaceScope(ProjectTreeNodeKind kind)
        => EditorWorkspacePolicy.Scope(kind);

    public static bool IsWorkspaceSectionRoot(ProjectTreeNodeKind kind)
        => EditorWorkspacePolicy.IsSectionRoot(kind);

    public static int WorkspaceOrder(ProjectTreeNodeKind kind)
        => EditorWorkspacePolicy.SectionOrder(kind);

    public static bool IsTopLevelSection(ProjectTreeNode node)
    {
        return node.Kind is ProjectTreeNodeKind.ProjectsRoot
            or ProjectTreeNodeKind.AppsRoot
            or ProjectTreeNodeKind.RenderQueueRoot
            or ProjectTreeNodeKind.ExternalMediaRoot
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
            ProjectTreeNodeKind.ExternalMediaRoot => 40,
            ProjectTreeNodeKind.ProductionDataRoot => 50,
            ProjectTreeNodeKind.SystemDataRoot => 60,
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
            ProjectTreeNodeKind.ProjectsRoot => "Projects",
            ProjectTreeNodeKind.Project => node.Name,
            ProjectTreeNodeKind.RenderQueueRoot => "Render Queue",
            ProjectTreeNodeKind.ExternalMediaRoot => "External Media",
            ProjectTreeNodeKind.ProductionDataRoot => "Production data",
            ProjectTreeNodeKind.SystemDataRoot => "System data",
            _ => node.Name,
        };
    }

    public static string Subtitle(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.ProjectsRoot =>
                "Create and select Projects",
            ProjectTreeNodeKind.Project =>
                string.IsNullOrWhiteSpace(node.Notes)
                    ? "Project"
                    : node.Notes,
            ProjectTreeNodeKind.AppsRoot => "System Apps, Modules and Variants",
            ProjectTreeNodeKind.RenderQueueRoot => "Local render jobs and history",
            ProjectTreeNodeKind.ExternalMediaRoot => "Authored external files and folders",
            ProjectTreeNodeKind.ProductionDataRoot => "Actors, devices, fonts, themes and Palette values",
            ProjectTreeNodeKind.SystemDataRoot => "Icon sets, component variants and System Palette",
            ProjectTreeNodeKind.ProductionFontsRoot => "Approved production font families",
            ProjectTreeNodeKind.IconThemesRoot => "Semantic icon tokens shared by every set",
            ProjectTreeNodeKind.ComponentClassesRoot => "Reusable component defaults",
            ProjectTreeNodeKind.ComponentClassGroup => "Component class group",
            ProjectTreeNodeKind.ThemesRoot => "Visual theme definitions",
            ProjectTreeNodeKind.ProductionPaletteRoot => "Production RGB values for the System Palette",
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
        return EditorAddOperationCatalog.Require(node.Kind).Label;
    }

    public static string HierarchicalIcon(ProjectTreeNode node)
    {
        return EditorIcons.ForNavigationTreeNode(node);
    }
}
