using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
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
            var selected = await new EditorEmbeddedUsageDialog(_owner, _isDark()).Show(settings.Name, settings.ComponentType, usages);
            if (selected is not null)
            {
                await NavigateToEmbeddedUsage(selected);
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
                await NavigateToEmbeddedUsage(selected);
            }
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded structure {slot.Label}", exception);
        }
    }

    private async Task NavigateToEmbeddedUsage(SpikeDatabase.EmbeddedComponentUsage usage)
    {
        if (!_selectNodeById(usage.ParentComponentClassId))
        {
            _loadProjectTree();
            if (!_selectNodeById(usage.ParentComponentClassId))
            {
                return;
            }
        }

        var node = _selectedNode();
        if (node is not null)
        {
            await _openEmbeddedComponentEditor(node, usage.SlotFieldId);
        }
    }
}
