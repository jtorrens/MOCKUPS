using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
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
    private readonly Func<SpikeDatabase.ReferenceUsageDetail, Task> _navigateToUsage;
    private readonly IEditorShellMessageSink _messages;

    public EditorNodeCommandController(
        Window owner,
        SpikeDatabase database,
        Func<bool> isDark,
        Func<IReadOnlyList<ProjectTreeNode>> treeRoots,
        Action loadProjectTree,
        Action<ProjectTreeNode> reloadAndSelect,
        Func<SpikeDatabase.ReferenceUsageDetail, Task> navigateToUsage,
        IEditorShellMessageSink messages)
    {
        _owner = owner;
        _database = database;
        _isDark = isDark;
        _treeRoots = treeRoots;
        _loadProjectTree = loadProjectTree;
        _reloadAndSelect = reloadAndSelect;
        _navigateToUsage = navigateToUsage;
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
            var preset = node.Kind switch
            {
                ProjectTreeNodeKind.ComponentPreset => _database.SaveComponentPreset(node, presetName),
                ProjectTreeNodeKind.ModuleVariant => _database.SaveModuleVariant(node, presetName),
                _ => throw new InvalidOperationException("Variants can only be saved from a selected variant."),
            };
            _reloadAndSelect(preset);
        }
        catch (Exception exception)
        {
            _messages.Error($"Save variant {node.Name}", exception);
        }
    }

    public async Task RestoreComponentPresetSnapshot(ProjectTreeNode node, EditorVariantHistorySnapshot snapshot)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentPreset and not ProjectTreeNodeKind.ModuleVariant)
        {
            return;
        }

        if (node.IsLocked)
        {
            _messages.Warning("Restore variant", $"{node.Name} is locked.");
            return;
        }

        var confirmed = await Dialogs().ConfirmAction(
            "Restore variant",
            $"Restore {node.Name}?",
            $"This replaces the current variant values with the version saved at {snapshot.Label}.",
            "Restore");
        if (!confirmed)
        {
            return;
        }

        try
        {
            if (node.Kind == ProjectTreeNodeKind.ComponentPreset)
                _database.ReplaceComponentPresetConfig(node, snapshot.ConfigJson);
            else
                _database.ReplaceModuleVariantConfig(node, snapshot.ConfigJson);
            _reloadAndSelect(node);
        }
        catch (Exception exception)
        {
            _messages.Error($"Restore variant {node.Name}", exception);
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
        if (node.Parent is null || !node.CanDuplicate) return;

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

    public Task ToggleComponentPresetLock(ProjectTreeNode node)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentPreset and not ProjectTreeNodeKind.ModuleVariant)
        {
            return Task.CompletedTask;
        }

        try
        {
            var toggled = node.Kind == ProjectTreeNodeKind.ComponentPreset
                ? _database.ToggleComponentPresetLock(node)
                : _database.ToggleModuleVariantLock(node);
            _reloadAndSelect(toggled);
        }
        catch (Exception exception)
        {
            _messages.Error($"Toggle variant lock {node.Name}", exception);
        }

        return Task.CompletedTask;
    }

    public async Task DeleteNode(ProjectTreeNode node)
    {
        if (node.Parent is null || !node.CanDelete) return;

        var deleteNodeId = node.Id;
        _loadProjectTree();
        node = EditorNodeSelectionState.FindNodeById(_treeRoots(), deleteNodeId) ?? node;
        if (node.Parent is null) return;

        var usages = _database.GetReferenceUsageDetails(node);
        if (usages.Count > 0)
        {
            var selected = await new EditorReferenceUsageDialog(_owner, _isDark()).Show(node, usages);
            if (selected is not null)
            {
                await _navigateToUsage(selected);
            }
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
