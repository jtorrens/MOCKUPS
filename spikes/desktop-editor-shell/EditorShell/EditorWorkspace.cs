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
    public static IReadOnlyList<ProjectTreeNode> SectionRoots(ProjectTreeNode project, EditorWorkspace workspace)
    {
        return DescendantsAndSelf(project)
            .Where((node) => EditorNavigationMetadata.IsWorkspaceSectionRoot(node.Kind))
            .Where((node) => Includes(EditorNavigationMetadata.WorkspaceScope(node.Kind), workspace))
            .OrderBy((node) => EditorNavigationMetadata.WorkspaceOrder(node.Kind))
            .ToList();
    }

    public static bool Contains(EditorWorkspace workspace, ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.Project) return true;
        return Includes(EditorNavigationMetadata.WorkspaceScope(node.Kind), workspace);
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

    private static IEnumerable<ProjectTreeNode> DescendantsAndSelf(ProjectTreeNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in DescendantsAndSelf(child)) yield return descendant;
        }
    }

    private static bool Includes(EditorWorkspaceScope scope, EditorWorkspace workspace)
    {
        var target = workspace == EditorWorkspace.Design
            ? EditorWorkspaceScope.Design
            : EditorWorkspaceScope.Production;
        return (scope & target) != 0;
    }
}
