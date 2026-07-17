using System;

namespace Mockups.DesktopEditorShell.EditorShell;

 [Flags]
internal enum EditorWorkspaceScope
{
    None = 0,
    Design = 1,
    Production = 2,
    Both = Design | Production,
}

internal static class EditorNavigationMetadata
{
    public static EditorWorkspaceScope WorkspaceScope(ProjectTreeNodeKind kind)
    {
        return kind switch
        {
            ProjectTreeNodeKind.Project => EditorWorkspaceScope.Both,
            ProjectTreeNodeKind.AppsRoot or ProjectTreeNodeKind.App or ProjectTreeNodeKind.Module or ProjectTreeNodeKind.ModuleVariant => EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.PaletteRoot or ProjectTreeNodeKind.PaletteColor => EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.IconThemesRoot or ProjectTreeNodeKind.IconTheme => EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.ComponentClassesRoot or ProjectTreeNodeKind.ComponentClassGroup
                or ProjectTreeNodeKind.ComponentClass or ProjectTreeNodeKind.ComponentPreset => EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.DevicesRoot or ProjectTreeNodeKind.Device => EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.ThemesRoot or ProjectTreeNodeKind.Theme => EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.ProductionFontsRoot or ProjectTreeNodeKind.ProductionFont => EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.EpisodesRoot or ProjectTreeNodeKind.Episode or ProjectTreeNodeKind.Shot
                or ProjectTreeNodeKind.ModuleInstance => EditorWorkspaceScope.Production,
            ProjectTreeNodeKind.ActorsRoot or ProjectTreeNodeKind.Actor => EditorWorkspaceScope.Production,
            ProjectTreeNodeKind.RenderPresetsRoot or ProjectTreeNodeKind.RenderPreset => EditorWorkspaceScope.Production,
            _ => EditorWorkspaceScope.None,
        };
    }

    public static bool IsWorkspaceSectionRoot(ProjectTreeNodeKind kind)
    {
        return kind is ProjectTreeNodeKind.AppsRoot
            or ProjectTreeNodeKind.ComponentClassesRoot
            or ProjectTreeNodeKind.ThemesRoot
            or ProjectTreeNodeKind.PaletteRoot
            or ProjectTreeNodeKind.IconThemesRoot
            or ProjectTreeNodeKind.ProductionFontsRoot
            or ProjectTreeNodeKind.DevicesRoot
            or ProjectTreeNodeKind.EpisodesRoot
            or ProjectTreeNodeKind.ActorsRoot
            or ProjectTreeNodeKind.RenderPresetsRoot;
    }

    public static int WorkspaceOrder(ProjectTreeNodeKind kind)
    {
        return kind switch
        {
            ProjectTreeNodeKind.AppsRoot => 10,
            ProjectTreeNodeKind.ComponentClassesRoot => 20,
            ProjectTreeNodeKind.ThemesRoot => 30,
            ProjectTreeNodeKind.PaletteRoot => 40,
            ProjectTreeNodeKind.IconThemesRoot => 50,
            ProjectTreeNodeKind.ProductionFontsRoot => 60,
            ProjectTreeNodeKind.DevicesRoot => 70,
            ProjectTreeNodeKind.EpisodesRoot => 10,
            ProjectTreeNodeKind.ActorsRoot => 20,
            ProjectTreeNodeKind.RenderPresetsRoot => 30,
            _ => 100,
        };
    }

    public static bool IsTopLevelSection(ProjectTreeNode node)
    {
        return node.Kind is ProjectTreeNodeKind.AppsRoot
            or ProjectTreeNodeKind.ProductionDataRoot
            or ProjectTreeNodeKind.SystemDataRoot;
    }

    public static int RootOrder(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.AppsRoot => 10,
            ProjectTreeNodeKind.EpisodesRoot => 20,
            ProjectTreeNodeKind.ProductionDataRoot => 30,
            ProjectTreeNodeKind.SystemDataRoot => 40,
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
            ProjectTreeNodeKind.ProductionDataRoot => "Actors, devices and production themes",
            ProjectTreeNodeKind.SystemDataRoot => "Icon sets, component variants, palette, fonts, media and render presets",
            ProjectTreeNodeKind.ProductionFontsRoot => "Approved production font families",
            ProjectTreeNodeKind.IconThemesRoot => "Semantic icon tokens shared by every set",
            ProjectTreeNodeKind.RenderPresetsRoot => "Reusable render output definitions",
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
            ProjectTreeNodeKind.AppsRoot => "Add app",
            ProjectTreeNodeKind.ComponentClassGroup => "Add component",
            ProjectTreeNodeKind.ComponentClass => "Add variant",
            ProjectTreeNodeKind.EpisodesRoot => "Add episode",
            ProjectTreeNodeKind.Episode => "Add shot",
            ProjectTreeNodeKind.Shot => "Add screen",
            ProjectTreeNodeKind.PaletteRoot => "Add palette color",
            ProjectTreeNodeKind.IconThemesRoot => "Add icon theme",
            ProjectTreeNodeKind.RenderPresetsRoot => "Add render preset",
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
