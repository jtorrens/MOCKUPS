using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorNodeSelectionState
{
    private readonly Dictionary<string, string> _lastComponentVariantNodeIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _lastModuleVariantNodeIds = new(StringComparer.Ordinal);

    public static bool CanSelectTreeNode(ProjectTreeNode node)
    {
        return node.CanOpenEditor || node.Kind is ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant;
    }

    public ProjectTreeNode ResolveSelectionNode(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => PreferredVariantNode(node),
            ProjectTreeNodeKind.Module => PreferredModuleVariantNode(node),
            _ => node,
        };
    }

    public ProjectTreeNode PreferredVariantNode(ProjectTreeNode componentClassNode)
    {
        if (_lastComponentVariantNodeIds.TryGetValue(componentClassNode.Id, out var variantNodeId)
            && componentClassNode.Children.FirstOrDefault((child) => child.Id.Equals(variantNodeId, StringComparison.Ordinal)) is { } rememberedVariant)
        {
            return rememberedVariant;
        }

        return componentClassNode.Children.FirstOrDefault((child) =>
                child.Kind == ProjectTreeNodeKind.ComponentVariant
                && VariantReferenceId.HasVariantId(child.Id, "default"))
            ?? componentClassNode.Children.FirstOrDefault((child) => child.Kind == ProjectTreeNodeKind.ComponentVariant)
            ?? componentClassNode;
    }

    public void RememberComponentVariantSelection(ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.ComponentVariant
            && node.Parent?.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            _lastComponentVariantNodeIds[node.Parent.Id] = node.Id;
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
                && VariantReferenceId.HasVariantId(child.Id, "default"))
            ?? moduleNode.Children.FirstOrDefault((child) => child.Kind == ProjectTreeNodeKind.ModuleVariant)
            ?? moduleNode;
    }

    public IReadOnlyDictionary<string, string> ExportComponentVariantSelections()
    {
        return new Dictionary<string, string>(_lastComponentVariantNodeIds, StringComparer.Ordinal);
    }

    public void RestoreComponentVariantSelections(IReadOnlyDictionary<string, string>? selections)
    {
        _lastComponentVariantNodeIds.Clear();
        if (selections is null)
        {
            return;
        }

        foreach (var (componentClassId, variantNodeId) in selections)
        {
            if (string.IsNullOrWhiteSpace(componentClassId)
                || string.IsNullOrWhiteSpace(variantNodeId))
            {
                continue;
            }

            _lastComponentVariantNodeIds[componentClassId] = variantNodeId;
        }
    }

    public static ProjectTreeNode EditorNodeForSelection(ProjectTreeNode node)
    {
        return (node.Kind is ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant) && node.Parent is not null
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
