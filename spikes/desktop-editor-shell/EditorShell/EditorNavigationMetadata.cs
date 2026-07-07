namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorNavigationMetadata
{
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
        return sectionRoot.Kind switch
        {
            ProjectTreeNodeKind.ProductionDataRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.ProductionDataRoot),
            ProjectTreeNodeKind.SystemDataRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.SystemDataRoot),
            ProjectTreeNodeKind.EpisodesRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.Episode),
            ProjectTreeNodeKind.PaletteRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.PaletteColor),
            ProjectTreeNodeKind.IconThemesRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.IconTheme),
            ProjectTreeNodeKind.RenderPresetsRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.RenderPreset),
            ProjectTreeNodeKind.ComponentClassesRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.ComponentClass),
            ProjectTreeNodeKind.ComponentClassGroup => EditorIcons.ForTreeNode(ProjectTreeNodeKind.ComponentClass),
            ProjectTreeNodeKind.DevicesRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.Device),
            ProjectTreeNodeKind.ActorsRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.Actor),
            ProjectTreeNodeKind.ThemesRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.Theme),
            _ => EditorIcons.ForTreeNode(ProjectTreeNodeKind.App),
        };
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
}
