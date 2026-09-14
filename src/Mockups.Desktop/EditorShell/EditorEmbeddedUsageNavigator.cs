using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorEmbeddedUsageNavigator
{
    private readonly IComponentDocumentStore _database;
    private readonly EditorOperationCoordinator _operations;
    private readonly Window _owner;
    private readonly Func<bool> _isDark;
    private readonly Func<string, bool> _selectNodeById;
    private readonly Func<Task<bool>> _loadProjectTree;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly Func<ProjectTreeNode, string, Task> _openEmbeddedComponentEditor;
    private readonly IEditorShellMessageSink _messages;

    public EditorEmbeddedUsageNavigator(
        IComponentDocumentStore database,
        EditorOperationCoordinator operations,
        Window owner,
        Func<bool> isDark,
        Func<string, bool> selectNodeById,
        Func<Task<bool>> loadProjectTree,
        Func<ProjectTreeNode?> selectedNode,
        Func<ProjectTreeNode, string, Task> openEmbeddedComponentEditor,
        IEditorShellMessageSink messages)
    {
        _database = database;
        _operations = operations;
        _owner = owner;
        _isDark = isDark;
        _selectNodeById = selectNodeById;
        _loadProjectTree = loadProjectTree;
        _selectedNode = selectedNode;
        _openEmbeddedComponentEditor = openEmbeddedComponentEditor;
        _messages = messages;
    }

    public async Task ShowForComponent(ProjectTreeNode node)
    {
        try
        {
            if (node.Kind != ProjectTreeNodeKind.ComponentClass)
            {
                return;
            }

            var variantNode = ActiveVariantNodeFor(node);
            var prepared = await _operations.ExecuteWithActivityAsync(
                "Preparing embedded usage…",
                () =>
                {
                    var settings = _database.GetComponentClassSettings(
                        node.Id);
                    var usages = _database.GetEmbeddedComponentUsages(
                        ProjectAncestor(node).Id,
                        settings.ComponentType,
                        node.Id);
                    var variantUsages = variantNode is null
                        ? []
                        : _database.GetComponentVariantReferenceUsageDetails(
                            variantNode);
                    return (Settings: settings,
                        Usages: usages,
                        VariantUsages: variantUsages);
                });
            var selected = await new EditorEmbeddedUsageDialog(_owner, _isDark()).Show(
                prepared.Settings.Name,
                prepared.Settings.ComponentType,
                prepared.Usages,
                variantNode?.Name,
                prepared.VariantUsages);
            if (selected is not null)
            {
                await NavigateToSelection(selected);
            }
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded structure {node.Name}", exception);
        }
    }

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent
                ?? throw new InvalidOperationException(
                    $"{node.Kind} has no Project context.");
        }
        return current;
    }

    public async Task ShowForEmbedded(ProjectTreeNode ownerNode, EmbeddedComponentSlotDefinition slot)
    {
        try
        {
            var usages = await _operations.ExecuteWithActivityAsync(
                "Preparing embedded usage…",
                () =>
                {
                    return _database.GetEmbeddedComponentUsages(
                        ProjectAncestor(ownerNode).Id,
                        slot.EmbeddedComponentType);
                });
            var selected = await new EditorEmbeddedUsageDialog(_owner, _isDark()).Show(
                slot.Label,
                slot.EmbeddedComponentType,
                usages);
            if (selected is not null)
            {
                await NavigateToSelection(selected);
            }
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded structure {slot.Label}", exception);
        }
    }

    private ProjectTreeNode? ActiveVariantNodeFor(ProjectTreeNode componentClassNode)
    {
        var selected = _selectedNode();
        return selected?.Kind == ProjectTreeNodeKind.ComponentVariant
               && selected.Parent?.Id.Equals(componentClassNode.Id, StringComparison.Ordinal) == true
            ? selected
            : componentClassNode.Children.FirstOrDefault((child) =>
                child.Kind == ProjectTreeNodeKind.ComponentVariant
                && VariantReferenceId.HasVariantId(child.Id, VariantEnvelopeContract.DefaultId))
              ?? componentClassNode.Children.FirstOrDefault((child) => child.Kind == ProjectTreeNodeKind.ComponentVariant);
    }

    private async Task NavigateToSelection(EditorEmbeddedUsageDialog.Selection selection)
    {
        if (selection.EmbeddedUsage is not null)
        {
            await NavigateToEmbeddedUsage(selection.EmbeddedUsage, selection.TargetNodeId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(selection.TargetNodeId))
        {
            await NavigateToNode(selection.TargetNodeId);
        }
    }

    public async Task NavigateToEmbeddedUsage(EmbeddedComponentUsage usage, string targetNodeId)
    {
        var nodeId = string.IsNullOrWhiteSpace(targetNodeId)
            ? usage.ParentComponentClassId
            : targetNodeId;
        if (!await NavigateToNode(nodeId))
        {
            return;
        }

        var node = _selectedNode();
        if (node is not null)
        {
            await _openEmbeddedComponentEditor(node, usage.SlotFieldId);
        }
    }

    private async Task<bool> NavigateToNode(string nodeId)
    {
        if (_selectNodeById(nodeId))
        {
            return true;
        }

        if (!await _loadProjectTree())
        {
            return false;
        }
        return _selectNodeById(nodeId);
    }
}
