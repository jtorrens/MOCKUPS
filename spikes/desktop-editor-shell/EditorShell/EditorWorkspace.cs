using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum EditorWorkspace
{
    Design,
    Production,
}

internal static class EditorWorkspaceNavigation
{
    private static readonly ProjectTreeNodeKind[] DesignSectionKinds =
    [
        ProjectTreeNodeKind.AppsRoot,
        ProjectTreeNodeKind.ComponentClassesRoot,
        ProjectTreeNodeKind.ThemesRoot,
        ProjectTreeNodeKind.PaletteRoot,
        ProjectTreeNodeKind.IconThemesRoot,
        ProjectTreeNodeKind.ProductionFontsRoot,
        ProjectTreeNodeKind.DevicesRoot,
    ];

    private static readonly ProjectTreeNodeKind[] ProductionSectionKinds =
    [
        ProjectTreeNodeKind.EpisodesRoot,
        ProjectTreeNodeKind.ActorsRoot,
        ProjectTreeNodeKind.RenderPresetsRoot,
    ];

    public static IReadOnlyList<ProjectTreeNode> SectionRoots(ProjectTreeNode project, EditorWorkspace workspace)
    {
        var kinds = workspace == EditorWorkspace.Design ? DesignSectionKinds : ProductionSectionKinds;
        return kinds
            .Select((kind) => FindFirst(project, kind))
            .Where((node) => node is not null)
            .Cast<ProjectTreeNode>()
            .ToList();
    }

    public static bool Contains(EditorWorkspace workspace, ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.Project) return true;
        return SectionRoots(ProjectAncestor(node), workspace)
            .Any((root) => root == node || IsAncestorOf(root, node));
    }

    public static ProjectTreeNode? FirstSelectable(IReadOnlyList<ProjectTreeNode> treeRoots, EditorWorkspace workspace)
    {
        foreach (var project in treeRoots)
        {
            foreach (var root in SectionRoots(project, workspace))
            {
                var node = DescendantsAndSelf(root).FirstOrDefault(EditorNodeSelectionState.CanSelectTreeNode);
                if (node is not null) return node;
            }
        }

        return null;
    }

    public static string Title(EditorWorkspace workspace) => workspace == EditorWorkspace.Design ? "Design" : "Production";

    public static EditorWorkspace Parse(string? value) =>
        string.Equals(value, "production", StringComparison.OrdinalIgnoreCase)
            ? EditorWorkspace.Production
            : EditorWorkspace.Design;

    public static string StorageValue(EditorWorkspace workspace) => workspace == EditorWorkspace.Design ? "design" : "production";

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Parent is not null) current = current.Parent;
        return current;
    }

    private static ProjectTreeNode? FindFirst(ProjectTreeNode node, ProjectTreeNodeKind kind) =>
        DescendantsAndSelf(node).FirstOrDefault((candidate) => candidate.Kind == kind);

    private static IEnumerable<ProjectTreeNode> DescendantsAndSelf(ProjectTreeNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in DescendantsAndSelf(child)) yield return descendant;
        }
    }

    private static bool IsAncestorOf(ProjectTreeNode ancestor, ProjectTreeNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current == ancestor) return true;
        }

        return false;
    }
}
