using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorNodeCommandController
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;
    private readonly Func<bool> _isDark;
    private readonly Func<IReadOnlyList<ProjectTreeNode>> _treeRoots;
    private readonly Action _loadProjectTree;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly IEditorShellMessageSink _messages;

    public EditorNodeCommandController(
        Window owner,
        SpikeDatabase database,
        Func<bool> isDark,
        Func<IReadOnlyList<ProjectTreeNode>> treeRoots,
        Action loadProjectTree,
        Action<ProjectTreeNode> reloadAndSelect,
        IEditorShellMessageSink messages)
    {
        _owner = owner;
        _database = database;
        _isDark = isDark;
        _treeRoots = treeRoots;
        _loadProjectTree = loadProjectTree;
        _reloadAndSelect = reloadAndSelect;
        _messages = messages;
    }

    public async Task SaveCurrentComponentPreset(ProjectTreeNode node)
    {
        var presetName = await Dialogs().PromptText(
            "Save variant",
            "Variant name",
            $"{node.Name} variant");
        if (string.IsNullOrWhiteSpace(presetName))
        {
            return;
        }

        try
        {
            var preset = _database.SaveComponentPreset(node, presetName);
            _reloadAndSelect(preset);
        }
        catch (Exception exception)
        {
            _messages.Error($"Save variant {node.Name}", exception);
        }
    }

    public async Task AddChild(ProjectTreeNode parent)
    {
        var workflow = new EditorAddChildWorkflow(_owner, _database, ShowInfoDialog);
        var child = await workflow.TryAdd(parent);
        if (child is null) return;

        if (parent.Kind == ProjectTreeNodeKind.IconThemesRoot)
        {
            _loadProjectTree();
            return;
        }

        _reloadAndSelect(child);
    }

    public void DuplicateNode(ProjectTreeNode node)
    {
        if (node.Parent is null) return;

        var copy = _database.Duplicate(node);
        _reloadAndSelect(copy);
    }

    public async Task RenameNode(ProjectTreeNode node)
    {
        if (!node.CanRenameDirectly)
        {
            return;
        }

        var nextName = await Dialogs().PromptText(
            "Rename",
            "Name",
            node.Name);
        if (string.IsNullOrWhiteSpace(nextName) || nextName.Trim().Equals(node.Name, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var renamed = _database.RenameDirectNode(node, nextName);
            _reloadAndSelect(renamed);
        }
        catch (Exception exception)
        {
            _messages.Error($"Rename {node.Name}", exception);
        }
    }

    public async Task DeleteNode(ProjectTreeNode node)
    {
        if (node.Parent is null) return;

        var deleteNodeId = node.Id;
        _loadProjectTree();
        node = EditorNodeSelectionState.FindNodeById(_treeRoots(), deleteNodeId) ?? node;
        if (node.Parent is null) return;

        var usages = _database.GetReferenceUsages(node);
        if (usages.Count > 0)
        {
            await ShowInfoDialog(
                "Cannot delete used item",
                $"{node.Name} is still used in:\n\n{string.Join(Environment.NewLine, usages.Take(12))}\n\nClean these references first, then delete it.");
            return;
        }

        var confirmed = await Dialogs().ConfirmDelete(node);
        if (!confirmed) return;

        var nextSelectionId = node.Parent.Id;
        try
        {
            _database.Delete(node);
        }
        catch (Exception exception)
        {
            await ShowInfoDialog("Delete failed", exception.Message);
            return;
        }

        var nextSelection = new ProjectTreeNode(
            node.Parent.Kind,
            nextSelectionId,
            node.Parent.Name,
            node.Parent.Notes,
            node.Parent.RecordClassId);
        _reloadAndSelect(nextSelection);
    }

    public Task ShowInfoDialog(string title, string message)
    {
        return Dialogs().ShowInfo(title, message);
    }

    private EditorDialogService Dialogs()
    {
        return new EditorDialogService(_owner, _isDark());
    }
}
