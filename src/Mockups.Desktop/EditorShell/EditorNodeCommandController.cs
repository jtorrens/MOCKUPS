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
    private readonly Action _refreshNavigation;
    private readonly Func<ReferenceUsageDetail, Task> _navigateToUsage;
    private readonly IEditorShellMessageSink _messages;
    private readonly Func<string, ValueKind, Task<string?>>? _browsePath;
    private string _copiedScreenId = "";
    private string _copiedScreenName = "";

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
        Action refreshNavigation,
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
        _refreshNavigation = refreshNavigation;
        _navigateToUsage = navigateToUsage;
        _messages = messages;
        _browsePath = browsePath;
    }

    public async Task RestoreVariantSnapshot(ProjectTreeNode node, EditorVariantHistorySnapshot snapshot)
    {
        using var activity = _operations.BeginActivity("Restoring Variant…");
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
        using var activity = _operations.BeginActivity("Adding item…");
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

        if (EditorAddOperationCatalog.Require(parent).Kind
            == EditorAddOperationKind.RefreshIconThemes)
        {
            await _loadProjectTree();
            return;
        }

        await _reloadAndSelect(child);
    }

    public async void DuplicateNode(ProjectTreeNode node)
    {
        using var activity = _operations.BeginActivity("Duplicating item…");
        if (node.Parent is null || !node.CanDuplicate) return;

        try
        {
            var draft = await new RecordCreationDialog(_owner).ShowPrepared(
                $"Duplicate {node.Name}", token => _operations.ExecuteAsync(
                    () => _database.PrepareRecordDuplication(node), token));
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
        using var activity = _operations.BeginActivity("Renaming item…");
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
        using var activity = _operations.BeginActivity("Transferring item…");
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

    public void CopyScreen(ProjectTreeNode screen)
    {
        if (screen.Kind != ProjectTreeNodeKind.ModuleInstance)
        {
            return;
        }

        _copiedScreenId = screen.Id;
        _copiedScreenName = screen.Name;
        _refreshNavigation();
        _messages.Info("Copy Screen", $"{screen.Name} is ready to paste into another Shot.");
    }

    public bool CanPasteScreen(ProjectTreeNode target)
    {
        var source = ResolveCopiedScreen();
        return source is not null
            && ProductionHierarchyTransferContract.CanTransfer(source, target);
    }

    public async Task PasteScreen(ProjectTreeNode target)
    {
        var source = ResolveCopiedScreen();
        if (source is null)
        {
            _messages.Warning(
                "Paste Screen",
                "The copied Screen is no longer available in this session.");
            return;
        }

        if (!ProductionHierarchyTransferContract.CanTransfer(source, target))
        {
            _messages.Warning(
                "Paste Screen",
                "A Screen can only be pasted into another Shot in the same Project.");
            return;
        }

        await TransferProductionNode(
            source,
            target,
            ProductionHierarchyTransferMode.Copy);
    }

    public string CopiedScreenName => _copiedScreenName;

    private ProjectTreeNode? ResolveCopiedScreen() =>
        string.IsNullOrWhiteSpace(_copiedScreenId)
            ? null
            : EditorNodeSelectionState.FindNodeById(
                _treeRoots(),
                _copiedScreenId) is { Kind: ProjectTreeNodeKind.ModuleInstance } screen
                ? screen
                : null;

    public void ReportProductionTransferGestureFailure(
        Exception exception) =>
        _messages.Error(
            "Start Production hierarchy transfer",
            exception);

    public async Task ToggleVariantLock(ProjectTreeNode node)
    {
        using var activity = _operations.BeginActivity("Updating Variant lock…");
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
        using var activity = _operations.BeginActivity("Deleting item…");
        if (!node.CanDelete) return;

        var decision = await Dialogs().PrepareDelete(node,
            token => _operations.ExecuteAsync(
                () => _referenceUsage.GetReferenceUsageDetails(node), token));
        if (decision?.Usage is { } usage)
        {
            await _navigateToUsage(usage);
            return;
        }
        if (decision?.Confirmed != true) return;

        var nextSelection = node.Parent is null
            ? _treeRoots().FirstOrDefault((root) => root.Id != node.Id)
            : node.Parent;
        try
        {
            await _operations.ExecuteAsync(() => _database.Delete(node));
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
