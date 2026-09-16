using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorNodeCommandController
{
    private readonly Window _owner;
    private readonly IEditorNodeCommandStore _database;
    private readonly IReferenceUsageQuery _referenceUsage;
    private readonly IEditorChildStore _children;
    private readonly IModuleInstanceCollectionStore _moduleInstances;
    private readonly IProjectPathResolver _projectPaths;
    private readonly EditorOperationCoordinator _operations;
    private readonly Func<bool> _isDark;
    private readonly Func<IReadOnlyList<ProjectTreeNode>> _treeRoots;
    private readonly Func<Task<bool>> _loadProjectTree;
    private readonly Func<ProjectTreeNode, Task> _reloadAndSelect;
    private readonly Func<ReferenceUsageDetail, Task> _navigateToUsage;
    private readonly IEditorShellMessageSink _messages;
    private readonly Func<string, ValueKind, Task<string?>>? _browsePath;

    public EditorNodeCommandController(
        Window owner,
        IEditorNodeCommandStore database,
        IReferenceUsageQuery referenceUsage,
        IEditorChildStore children,
        IModuleInstanceCollectionStore moduleInstances,
        IProjectPathResolver projectPaths,
        EditorOperationCoordinator operations,
        Func<bool> isDark,
        Func<IReadOnlyList<ProjectTreeNode>> treeRoots,
        Func<Task<bool>> loadProjectTree,
        Func<ProjectTreeNode, Task> reloadAndSelect,
        Func<ReferenceUsageDetail, Task> navigateToUsage,
        IEditorShellMessageSink messages,
        Func<string, ValueKind, Task<string?>>? browsePath = null)
    {
        _owner = owner;
        _database = database;
        _referenceUsage = referenceUsage;
        _children = children;
        _moduleInstances = moduleInstances;
        _projectPaths = projectPaths;
        _operations = operations;
        _isDark = isDark;
        _treeRoots = treeRoots;
        _loadProjectTree = loadProjectTree;
        _reloadAndSelect = reloadAndSelect;
        _navigateToUsage = navigateToUsage;
        _messages = messages;
        _browsePath = browsePath;
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
            await _operations.ExecuteAsync(
                () =>
                {
                    if (node.Kind == ProjectTreeNodeKind.ComponentVariant)
                    {
                        _database.ReplaceComponentVariantConfig(
                            node,
                            snapshot.ConfigJson);
                    }
                    else
                    {
                        _database.ReplaceModuleVariantConfig(
                            node,
                            snapshot.ConfigJson);
                    }
                });
            await _reloadAndSelect(node);
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
            _children,
            _moduleInstances,
            _projectPaths,
            _operations,
            ShowInfoDialog,
            _browsePath);
        var child = await workflow.TryAdd(parent);
        if (child is null) return;

        if (EditorAddOperationCatalog.Require(parent.Kind).Kind
            == EditorAddOperationKind.RefreshIconThemes)
        {
            await _loadProjectTree();
            return;
        }

        await _reloadAndSelect(child);
    }

    public async void DuplicateNode(ProjectTreeNode node)
    {
        if (node.Parent is null || !node.CanDuplicate) return;

        try
        {
            var definition = await _operations.ExecuteWithActivityAsync(
                "Preparing duplication…",
                () => _database.PrepareRecordDuplication(node));
            var draft = definition.RequiresConfirmation
                ? await new RecordCreationDialog(_owner).Show(definition)
                : new RecordCreationDraft(
                    definition.Id,
                    definition.Fields.ToDictionary(
                        (field) => field.Definition.Id,
                        (field) => field.Value,
                        StringComparer.Ordinal));
            if (draft is null) return;
            var copy = await _operations.ExecuteAsync(
                () => _database.Duplicate(node, draft));
            await _reloadAndSelect(copy);
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
            var renamed = await _operations.ExecuteAsync(
                () => _database.RenameDirectNode(node, nextName));
            await _reloadAndSelect(renamed);
        }
        catch (Exception exception)
        {
            _messages.Error($"Rename {node.Name}", exception);
        }
    }

    public async Task TransferProductionNode(
        ProjectTreeNode source,
        ProjectTreeNode target,
        ProductionHierarchyTransferMode mode)
    {
        try
        {
            var transferred = await _operations.ExecuteAsync(
                () => _database.TransferProductionNode(
                    source,
                    target,
                    mode));
            await _reloadAndSelect(transferred);
        }
        catch (OperationCanceledException)
        {
            // The editor session no longer owns the queued operation.
        }
        catch (Exception exception)
        {
            _messages.Error(
                $"{mode} {EditorNavigationMetadata.Title(source)}",
                exception);
        }
    }

    public void ReportProductionTransferGestureFailure(
        Exception exception) =>
        _messages.Error(
            "Start Production hierarchy transfer",
            exception);

    public async Task ToggleVariantLock(ProjectTreeNode node)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.ModuleVariant)
        {
            return;
        }

        try
        {
            var toggled = await _operations.ExecuteAsync(
                () => node.Kind == ProjectTreeNodeKind.ComponentVariant
                    ? _database.ToggleComponentVariantLock(node)
                    : _database.ToggleModuleVariantLock(node));
            await _reloadAndSelect(toggled);
        }
        catch (Exception exception)
        {
            _messages.Error($"Toggle variant lock {node.Name}", exception);
        }
    }

    public async Task DeleteNode(ProjectTreeNode node)
    {
        if (!node.CanDelete) return;

        var deleteNodeId = node.Id;
        if (!await _loadProjectTree())
        {
            return;
        }
        node = EditorNodeSelectionState.FindNodeById(_treeRoots(), deleteNodeId) ?? node;

        var usages = await _operations.ExecuteWithActivityAsync(
            "Checking reference usage…",
            () => _referenceUsage.GetReferenceUsageDetails(node));
        if (usages.Count > 0)
        {
            var selected = await new EditorReferenceUsageDialog(_owner, _isDark()).Show(node, usages);
            if (selected is not null)
            {
                await _navigateToUsage(selected);
            }
            return;
        }

        var confirmed = node.Kind == ProjectTreeNodeKind.Shot
            ? await Dialogs().ConfirmAction(
                "Delete Shot",
                $"Delete {node.Name}?",
                "The Shot and its Screens will be removed. Existing production output folders are retained and are never deleted automatically.",
                "Delete",
                width: 480,
                height: 240)
            : await Dialogs().ConfirmDelete(node);
        if (!confirmed) return;

        var nextSelection = node.Parent is null
            ? _treeRoots().FirstOrDefault((root) => root.Id != node.Id)
            : node.Parent;
        try
        {
            await _operations.ExecuteAsync(
                () => _database.Delete(node));
        }
        catch (Exception exception)
        {
            await ShowInfoDialog("Delete failed", exception.Message);
            return;
        }

        if (nextSelection is null)
        {
            await _loadProjectTree();
            return;
        }
        await _reloadAndSelect(nextSelection);
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
