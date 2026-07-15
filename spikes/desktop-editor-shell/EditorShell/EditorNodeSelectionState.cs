using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorNodeSelectionState
{
    private readonly Dictionary<string, string> _lastComponentPresetNodeIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _lastModuleVariantNodeIds = new(StringComparer.Ordinal);

    public static bool CanSelectTreeNode(ProjectTreeNode node)
    {
        return node.CanOpenEditor || node.Kind is ProjectTreeNodeKind.ComponentPreset or ProjectTreeNodeKind.ModuleVariant;
    }

    public ProjectTreeNode ResolveSelectionNode(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => PreferredPresetNode(node),
            ProjectTreeNodeKind.Module => PreferredModuleVariantNode(node),
            _ => node,
        };
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
        if (node.Kind == ProjectTreeNodeKind.ModuleVariant
            && node.Parent?.Kind == ProjectTreeNodeKind.Module)
        {
            _lastModuleVariantNodeIds[node.Parent.Id] = node.Id;
        }
    }

    public ProjectTreeNode PreferredModuleVariantNode(ProjectTreeNode moduleNode)
    {
        if (_lastModuleVariantNodeIds.TryGetValue(moduleNode.Id, out var variantNodeId)
            && moduleNode.Children.FirstOrDefault((child) => child.Id == variantNodeId) is { } remembered)
            return remembered;
        return moduleNode.Children.FirstOrDefault((child) =>
                child.Kind == ProjectTreeNodeKind.ModuleVariant
                && child.Id.EndsWith("::variant::default", StringComparison.Ordinal))
            ?? moduleNode.Children.FirstOrDefault((child) => child.Kind == ProjectTreeNodeKind.ModuleVariant)
            ?? moduleNode;
    }

    public IReadOnlyDictionary<string, string> ExportComponentPresetSelections()
    {
        return new Dictionary<string, string>(_lastComponentPresetNodeIds, StringComparer.Ordinal);
    }

    public void RestoreComponentPresetSelections(IReadOnlyDictionary<string, string>? selections)
    {
        _lastComponentPresetNodeIds.Clear();
        if (selections is null)
        {
            return;
        }

        foreach (var (componentClassId, presetNodeId) in selections)
        {
            if (string.IsNullOrWhiteSpace(componentClassId)
                || string.IsNullOrWhiteSpace(presetNodeId))
            {
                continue;
            }

            _lastComponentPresetNodeIds[componentClassId] = presetNodeId;
        }
    }

    public static ProjectTreeNode EditorNodeForSelection(ProjectTreeNode node)
    {
        return (node.Kind is ProjectTreeNodeKind.ComponentPreset or ProjectTreeNodeKind.ModuleVariant) && node.Parent is not null
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
