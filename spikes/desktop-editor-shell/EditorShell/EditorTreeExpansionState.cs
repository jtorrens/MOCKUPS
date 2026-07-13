using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorTreeExpansionState
{
    private readonly HashSet<string> _expandedNodeIds = [];

    public bool IsExpanded(ProjectTreeNode node)
    {
        return _expandedNodeIds.Contains(node.Id);
    }

    public void EnsureInitial(IReadOnlyList<ProjectTreeNode> treeRoots)
    {
        if (_expandedNodeIds.Count == 0 && treeRoots.Count > 0)
        {
            _expandedNodeIds.Add(treeRoots[0].Id);
        }
    }

    public void Toggle(ProjectTreeNode node)
    {
        if (node.Children.Count == 0) return;

        if (_expandedNodeIds.Contains(node.Id))
        {
            CollapseNodeAndDescendants(node);
            return;
        }

        CollapseVisibleNavigationPeers(node);
        _expandedNodeIds.Add(node.Id);
    }

    public void ExpandAncestors(ProjectTreeNode node)
    {
        var parent = node.Parent;
        while (parent is not null)
        {
            if (EditorNavigationMetadata.IsWorkspaceSectionRoot(parent.Kind))
            {
                CollapseWorkspaceSectionPeers(parent);
            }
            if (EditorNavigationMetadata.CollapseSiblingsWhenOpenedBySelection(parent))
            {
                CollapseSiblingNodes(parent);
            }

            _expandedNodeIds.Add(parent.Id);
            parent = parent.Parent;
        }
    }

    private void CollapseSiblingNodes(ProjectTreeNode node)
    {
        if (node.Parent is null) return;

        foreach (var sibling in node.Parent.Children.Where((child) => child.Id != node.Id))
        {
            CollapseNodeAndDescendants(sibling);
        }
    }

    private void CollapseVisibleNavigationPeers(ProjectTreeNode node)
    {
        if (EditorNavigationMetadata.IsWorkspaceSectionRoot(node.Kind))
        {
            CollapseWorkspaceSectionPeers(node);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Project)
        {
            foreach (var child in node.Children)
            {
                CollapseNodeAndDescendants(child);
            }
            return;
        }

        if (node.Parent?.Kind == ProjectTreeNodeKind.Project)
        {
            CollapseNodeAndDescendants(node.Parent);
            CollapseSiblingNodes(node);
            return;
        }

        CollapseSiblingNodes(node);
    }

    private void CollapseWorkspaceSectionPeers(ProjectTreeNode node)
    {
        var project = node;
        while (project.Parent is not null) project = project.Parent;
        foreach (var section in Descendants(project)
                     .Where((candidate) => EditorNavigationMetadata.IsWorkspaceSectionRoot(candidate.Kind))
                     .Where((candidate) => candidate.Id != node.Id))
        {
            CollapseNodeAndDescendants(section);
        }
    }

    private static IEnumerable<ProjectTreeNode> Descendants(ProjectTreeNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private void CollapseNodeAndDescendants(ProjectTreeNode node)
    {
        _expandedNodeIds.Remove(node.Id);
        foreach (var child in node.Children)
        {
            CollapseNodeAndDescendants(child);
        }
    }
}
