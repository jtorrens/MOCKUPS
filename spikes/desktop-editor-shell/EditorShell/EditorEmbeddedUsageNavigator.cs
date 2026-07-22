using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorEmbeddedUsageNavigator
{
    private readonly SpikeDatabase _database;
    private readonly Window _owner;
    private readonly Func<bool> _isDark;
    private readonly Func<string, bool> _selectNodeById;
    private readonly Action _loadProjectTree;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly Func<ProjectTreeNode, string, Task> _openEmbeddedComponentEditor;
    private readonly IEditorShellMessageSink _messages;

    public EditorEmbeddedUsageNavigator(
        SpikeDatabase database,
        Window owner,
        Func<bool> isDark,
        Func<string, bool> selectNodeById,
        Action loadProjectTree,
        Func<ProjectTreeNode?> selectedNode,
        Func<ProjectTreeNode, string, Task> openEmbeddedComponentEditor,
        IEditorShellMessageSink messages)
    {
        _database = database;
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

            var settings = _database.GetComponentClassSettings(node.Id);
            var usages = _database.GetEmbeddedComponentUsages(settings.ProjectId, settings.ComponentType, node.Id);
            var variantNode = ActiveVariantNodeFor(node);
            var variantUsages = variantNode is null
                ? []
                : _database.GetComponentVariantReferenceUsageDetails(variantNode);
            var selected = await new EditorEmbeddedUsageDialog(_owner, _isDark()).Show(
                settings.Name,
                settings.ComponentType,
                usages,
                variantNode?.Name,
                variantUsages);
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

    public async Task ShowForEmbedded(ProjectTreeNode ownerNode, EmbeddedComponentSlotDefinition slot)
    {
        try
        {
            var ownerSettings = _database.GetComponentClassSettings(ownerNode.Id);
            var usages = _database.GetEmbeddedComponentUsages(
                ownerSettings.ProjectId,
                slot.EmbeddedComponentType);
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
                && child.Id.EndsWith("::variant::default", StringComparison.Ordinal))
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
            NavigateToNode(selection.TargetNodeId);
        }
    }

    public async Task NavigateToEmbeddedUsage(SpikeDatabase.EmbeddedComponentUsage usage, string targetNodeId)
    {
        var nodeId = string.IsNullOrWhiteSpace(targetNodeId)
            ? usage.ParentComponentClassId
            : targetNodeId;
        if (!NavigateToNode(nodeId))
        {
            return;
        }

        var node = _selectedNode();
        if (node is not null)
        {
            await _openEmbeddedComponentEditor(node, usage.SlotFieldId);
        }
    }

    private bool NavigateToNode(string nodeId)
    {
        if (_selectNodeById(nodeId))
        {
            return true;
        }

        _loadProjectTree();
        return _selectNodeById(nodeId);
    }
}
