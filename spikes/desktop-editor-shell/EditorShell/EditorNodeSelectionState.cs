using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorNodeSelectionState
{
    private readonly Dictionary<string, string> _lastComponentPresetNodeIds = new(StringComparer.Ordinal);

    public static bool CanSelectTreeNode(ProjectTreeNode node)
    {
        return node.CanOpenEditor || node.Kind == ProjectTreeNodeKind.ComponentPreset;
    }

    public ProjectTreeNode ResolveSelectionNode(ProjectTreeNode node)
    {
        return node.Kind == ProjectTreeNodeKind.ComponentClass
            ? PreferredPresetNode(node)
            : node;
    }

    public ProjectTreeNode PreferredPresetNode(ProjectTreeNode componentClassNode)
    {
        if (_lastComponentPresetNodeIds.TryGetValue(componentClassNode.Id, out var presetNodeId)
            && componentClassNode.Children.FirstOrDefault((child) => child.Id.Equals(presetNodeId, StringComparison.Ordinal)) is { } rememberedPreset)
        {
            return rememberedPreset;
        }

        return componentClassNode.Children.FirstOrDefault((child) =>
                child.Kind == ProjectTreeNodeKind.ComponentPreset
                && child.Id.EndsWith("::preset::default", StringComparison.Ordinal))
            ?? componentClassNode.Children.FirstOrDefault((child) => child.Kind == ProjectTreeNodeKind.ComponentPreset)
            ?? componentClassNode;
    }

    public void RememberComponentPresetSelection(ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.ComponentPreset
            && node.Parent?.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            _lastComponentPresetNodeIds[node.Parent.Id] = node.Id;
        }
    }

    public static ProjectTreeNode EditorNodeForSelection(ProjectTreeNode node)
    {
        return node.Kind == ProjectTreeNodeKind.ComponentPreset && node.Parent is not null
            ? node.Parent
            : node;
    }

    public static ProjectTreeNode? SelectedComponentClassNode(ProjectTreeNode? selectedNode)
    {
        var node = selectedNode is null ? null : EditorNodeForSelection(selectedNode);
        return node?.Kind == ProjectTreeNodeKind.ComponentClass ? node : null;
    }

    public static ProjectTreeNode ClosestEditableNode(ProjectTreeNode node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.CanOpenEditor)
            {
                return current;
            }
        }

        return node;
    }

    public static ProjectTreeNode? FindNodeById(IEnumerable<ProjectTreeNode> nodes, string nodeId)
    {
        foreach (var node in nodes)
        {
            if (node.Id == nodeId) return node;

            var child = FindNodeById(node.Children, nodeId);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }
}
