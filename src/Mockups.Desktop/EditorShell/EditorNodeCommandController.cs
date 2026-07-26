using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorNodeCommandController
{
    private readonly Window _owner;
    private readonly IEditorNodeCommandStore _database;
    private readonly IProjectPathResolver _projectPaths;
    private readonly Func<bool> _isDark;
    private readonly Func<IReadOnlyList<ProjectTreeNode>> _treeRoots;
    private readonly Func<Task<bool>> _loadProjectTree;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly Func<ReferenceUsageDetail, Task> _navigateToUsage;
    private readonly IEditorShellMessageSink _messages;

    public EditorNodeCommandController(
        Window owner,
        IEditorNodeCommandStore database,
        IProjectPathResolver projectPaths,
        Func<bool> isDark,
        Func<IReadOnlyList<ProjectTreeNode>> treeRoots,
        Func<Task<bool>> loadProjectTree,
        Action<ProjectTreeNode> reloadAndSelect,
        Func<ReferenceUsageDetail, Task> navigateToUsage,
        IEditorShellMessageSink messages)
    {
        _owner = owner;
        _database = database;
        _projectPaths = projectPaths;
        _isDark = isDark;
        _treeRoots = treeRoots;
        _loadProjectTree = loadProjectTree;
        _reloadAndSelect = reloadAndSelect;
        _navigateToUsage = navigateToUsage;
        _messages = messages;
    }

    public async Task SaveCurrentVariant(ProjectTreeNode node)
    {
        var variantName = await Dialogs().PromptText(
            "New Variant",
            "Variant name",
            $"{node.Name} copy");
        if (string.IsNullOrWhiteSpace(variantName))
        {
            return;
        }

        try
        {
            var variant = node.Kind switch
            {
                ProjectTreeNodeKind.ComponentVariant => _database.SaveComponentVariant(node, variantName),
                ProjectTreeNodeKind.ModuleVariant => _database.SaveModuleVariant(node, variantName),
                _ => throw new InvalidOperationException("Variants can only be saved from a selected variant."),
            };
            _reloadAndSelect(variant);
        }
        catch (Exception exception)
        {
            _messages.Error($"Create Variant from {node.Name}", exception);
        }
    }

    public async Task RestoreVariantSnapshot(ProjectTreeNode node, EditorVariantHistorySnapshot snapshot)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.ModuleVariant)
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
            if (node.Kind == ProjectTreeNodeKind.ComponentVariant)
                _database.ReplaceComponentVariantConfig(node, snapshot.ConfigJson);
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
        var workflow = new EditorAddChildWorkflow(
            _owner,
            _database,
            _projectPaths,
            ShowInfoDialog);
        var child = await workflow.TryAdd(parent);
        if (child is null) return;

        if (parent.Kind == ProjectTreeNodeKind.IconThemesRoot)
        {
            await _loadProjectTree();
            return;
        }

        _reloadAndSelect(child);
    }

    public async void DuplicateNode(ProjectTreeNode node)
    {
        if (node.Parent is null || !node.CanDuplicate) return;

        if (node.Kind == ProjectTreeNodeKind.Episode
            && _database.GetShotManagerEpisodeBinding(node.Id) is not null)
        {
            await ShowInfoDialog(
                "Episode duplication unavailable",
                "Shot Manager governs this Episode. Disconnect the Project before duplicating its local hierarchy.");
            return;
        }
        if (node.Kind == ProjectTreeNodeKind.Shot)
        {
            try
            {
                var episode = node.Parent;
                var draft = await new ShotCreationDialog(
                    _owner,
                    _database).Show(episode);
                if (draft is null) return;
                var copy =
                    _database.GetShotManagerEpisodeBinding(episode.Id) is null
                        ? _database.DuplicateShot(
                            node,
                            draft.ActorId,
                            draft.ShotNumber)
                        : await new ShotManagerShotCreationService(_database)
                            .CreateAsync(
                                episode,
                                draft.ActorId,
                                draft.ShotNumber,
                                node.Id);
                _reloadAndSelect(copy);
            }
            catch (Exception exception)
            {
                await ShowInfoDialog(
                    "Shot duplication failed",
                    exception.Message);
            }
            return;
        }
        try
        {
            var copy = _database.Duplicate(node);
            _reloadAndSelect(copy);
        }
        catch (Exception exception)
        {
            _messages.Error($"Duplicate {node.Name}", exception);
        }
    }

    public async Task RenameNode(ProjectTreeNode node)
    {
        if (!node.CanRenameDirectly)
        {
            return;
        }
        if (node.Kind == ProjectTreeNodeKind.Episode
            && _database.GetShotManagerEpisodeBinding(node.Id) is not null)
        {
            _messages.Warning(
                "Rename Episode",
                "Shot Manager governs this Episode name. Rename it there and synchronize.");
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

    public Task ToggleVariantLock(ProjectTreeNode node)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.ModuleVariant)
        {
            return Task.CompletedTask;
        }

        try
        {
            var toggled = node.Kind == ProjectTreeNodeKind.ComponentVariant
                ? _database.ToggleComponentVariantLock(node)
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
        if (!await _loadProjectTree())
        {
            return;
        }
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

        if (node.Kind == ProjectTreeNodeKind.Episode
            && _database.GetShotManagerEpisodeBinding(node.Id) is not null)
        {
            await ShowInfoDialog(
                "Episode deletion unavailable",
                "Shot Manager governs this Episode. Disconnect the Project before deleting it locally.");
            return;
        }
        var confirmed = node.Kind == ProjectTreeNodeKind.Shot
            && _database.GetShotManagerShotStructure(node.Id) is not null
            ? await Dialogs().ConfirmAction(
                "Delete Shot",
                $"Delete {node.Name}?",
                "The local Shot and its Screens will be removed. Its production folders are retained and are never deleted automatically.",
                "Delete",
                width: 480,
                height: 240)
            : await Dialogs().ConfirmDelete(node);
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
