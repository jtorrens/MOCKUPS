using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Mockups.DesktopEditorShell.EditorShell;

public interface IEditorNavigationDataSource
{
    IReadOnlyList<ProjectTreeNode> LoadProjectTree();
}

public enum EditorTreeLoadIntent
{
    Workspace,
    ActiveEditor,
}

public sealed class EditorTreeLoadOperation
{
    internal EditorTreeLoadOperation(
        long id,
        long baseRevision,
        EditorWorkspace workspace,
        EditorTreeLoadIntent intent,
        CancellationTokenSource cancellation)
    {
        Id = id;
        BaseRevision = baseRevision;
        Workspace = workspace;
        Intent = intent;
        Cancellation = cancellation;
        Token = cancellation.Token;
    }

    internal CancellationTokenSource Cancellation { get; }
    public long Id { get; }
    public long BaseRevision { get; }
    public EditorWorkspace Workspace { get; }
    public EditorTreeLoadIntent Intent { get; }
    public CancellationToken Token { get; }
}

public sealed class EditorWorkspaceCoordinator : IDisposable
{
    private readonly object _stateGate = new();
    private readonly IEditorNavigationDataSource _navigation;
    private readonly EditorNodeSelectionState _nodeSelection = new();
    private EditorSessionState _state = EditorSessionState.Empty;
    private EditorTreeLoadOperation? _activeTreeLoad;
    private long _nextTreeLoadId;
    private bool _disposed;

    public EditorWorkspaceCoordinator(IEditorNavigationDataSource navigation)
    {
        _navigation = navigation
            ?? throw new ArgumentNullException(nameof(navigation));
    }

    public EditorSessionState State
    {
        get
        {
            lock (_stateGate) return _state;
        }
    }

    public EditorSessionTransition Restore(EditorSessionRestoreState restored)
    {
        lock (_stateGate)
        {
            ThrowIfDisposed();
            InvalidateActiveTreeLoad();
            var previous = _state;
            _nodeSelection.RestoreComponentVariantSelections(
                restored.ComponentVariantSelections);
            var revision = previous.Revision + 1;
            _state = new EditorSessionState(
                previous.TreeRoots,
                restored.Workspace,
                restored.ProductionId,
                null,
                null,
                null,
                _nodeSelection.Snapshot(),
                Preview(restored.Workspace, null, revision),
                revision);
            return new EditorSessionTransition(
                "restore",
                previous,
                _state,
                EditorSessionEffects.Workspace
                    | EditorSessionEffects.Production);
        }
    }

    public EditorSessionTransition ReloadTree(
        string source = "tree-load",
        EditorTreeLoadIntent intent = EditorTreeLoadIntent.Workspace)
    {
        var operation = BeginTreeLoad(State.Workspace, intent);
        try
        {
            var roots = _navigation.LoadProjectTree();
            if (!TryCommitTreeLoad(
                    operation,
                    roots,
                    source,
                    out var transition))
            {
                throw new InvalidOperationException(
                    "The synchronous tree load became obsolete before it could commit.");
            }
            return transition;
        }
        catch
        {
            AbandonTreeLoad(operation);
            throw;
        }
    }

    public EditorSessionTransition SwitchWorkspace(
        EditorWorkspace workspace,
        string source = "workspace")
    {
        if (workspace == State.Workspace)
        {
            return Unchanged(source);
        }

        var operation = BeginTreeLoad(
            workspace,
            EditorTreeLoadIntent.Workspace);
        try
        {
            var roots = _navigation.LoadProjectTree();
            if (!TryCommitTreeLoad(
                    operation,
                    roots,
                    source,
                    out var transition))
            {
                throw new InvalidOperationException(
                    "The synchronous workspace load became obsolete before it could commit.");
            }
            return transition;
        }
        catch
        {
            AbandonTreeLoad(operation);
            throw;
        }
    }

    public EditorTreeLoadOperation BeginTreeLoad(
        EditorWorkspace workspace,
        EditorTreeLoadIntent intent = EditorTreeLoadIntent.Workspace)
    {
        lock (_stateGate)
        {
            ThrowIfDisposed();
            InvalidateActiveTreeLoad();
            var operation = new EditorTreeLoadOperation(
                ++_nextTreeLoadId,
                _state.Revision,
                workspace,
                intent,
                new CancellationTokenSource());
            _activeTreeLoad = operation;
            return operation;
        }
    }

    public bool TryCommitTreeLoad(
        EditorTreeLoadOperation operation,
        IReadOnlyList<ProjectTreeNode> treeRoots,
        string source,
        out EditorSessionTransition transition)
    {
        lock (_stateGate)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(_activeTreeLoad, operation)
                || operation.Token.IsCancellationRequested
                || operation.BaseRevision != _state.Revision)
            {
                transition = Unchanged(source);
                return false;
            }

            _activeTreeLoad = null;
            operation.Cancellation.Dispose();
            var previous = _state;
            var roots = treeRoots.ToArray();
            var productionId = ResolveProductionId(
                roots,
                previous.ProductionId);
            var selected = ResolveTreeSelection(
                roots,
                operation.Workspace,
                previous);
            if (selected is not null)
            {
                _nodeSelection.RememberVariantSelection(selected);
            }
            var workspaceSelections = Copy(previous.WorkspaceSelections);
            if (selected is not null)
            {
                workspaceSelections[operation.Workspace] = selected.Id;
            }
            var embedded = operation.Intent == EditorTreeLoadIntent.ActiveEditor
                ? RebaseEmbeddedEditor(previous.EmbeddedEditor, selected)
                : null;
            var revision = previous.Revision + 1;
            _state = new EditorSessionState(
                roots,
                operation.Workspace,
                productionId,
                selected,
                embedded,
                workspaceSelections,
                _nodeSelection.Snapshot(),
                Preview(operation.Workspace, selected, revision),
                revision);
            var effects = EditorSessionEffects.Navigation
                | EditorSessionEffects.Editor
                | EditorSessionEffects.PreviewSelection
                | EditorSessionEffects.PreviewOptions;
            if (operation.Workspace != previous.Workspace)
            {
                effects |= EditorSessionEffects.Workspace;
            }
            if (!productionId.Equals(
                    previous.ProductionId,
                    StringComparison.Ordinal))
            {
                effects |= EditorSessionEffects.Production;
            }
            transition = new EditorSessionTransition(
                source,
                previous,
                _state,
                effects);
            return true;
        }
    }

    public bool TrySelectNode(
        ProjectTreeNode node,
        string source,
        out EditorSessionTransition transition)
    {
        lock (_stateGate)
        {
            ThrowIfDisposed();
            var currentNode = EditorNodeSelectionState.FindNodeById(
                _state.TreeRoots,
                node.Id);
            if (currentNode is null)
            {
                transition = Unchanged(source);
                return false;
            }
            var selectable = EditorNodeSelectionState.CanSelectTreeNode(currentNode)
                ? currentNode
                : EditorNodeSelectionState.ClosestEditableNode(currentNode);
            selectable = _nodeSelection.ResolveSelectionNode(selectable);
            if (!EditorNodeSelectionState.CanSelectTreeNode(selectable)
                || !EditorWorkspaceNavigation.Contains(
                    _state.Workspace,
                    selectable))
            {
                transition = Unchanged(source);
                return false;
            }

            InvalidateActiveTreeLoad();
            var previous = _state;
            _nodeSelection.RememberVariantSelection(selectable);
            var workspaceSelections = Copy(previous.WorkspaceSelections);
            workspaceSelections[previous.Workspace] = selectable.Id;
            var revision = previous.Revision + 1;
            _state = new EditorSessionState(
                previous.TreeRoots,
                previous.Workspace,
                previous.ProductionId,
                selectable,
                null,
                workspaceSelections,
                _nodeSelection.Snapshot(),
                Preview(previous.Workspace, selectable, revision),
                revision);
            transition = new EditorSessionTransition(
                source,
                previous,
                _state,
                EditorSessionEffects.Navigation
                    | EditorSessionEffects.Editor
                    | EditorSessionEffects.PreviewSelection);
            return true;
        }
    }

    public bool TrySelectNodeById(
        string nodeId,
        string source,
        out EditorSessionTransition transition)
    {
        var node = EditorNodeSelectionState.FindNodeById(
            State.TreeRoots,
            nodeId);
        if (node is null)
        {
            transition = Unchanged(source);
            return false;
        }
        return TrySelectNode(node, source, out transition);
    }

    public bool TrySelectNodeInWorkspace(
        EditorWorkspace workspace,
        string nodeId,
        string source,
        out EditorSessionTransition transition)
    {
        lock (_stateGate)
        {
            ThrowIfDisposed();
            var node = EditorNodeSelectionState.FindNodeById(
                _state.TreeRoots,
                nodeId);
            if (node is null
                || !EditorWorkspaceNavigation.Contains(workspace, node))
            {
                transition = Unchanged(source);
                return false;
            }

            InvalidateActiveTreeLoad();
            var previous = _state;
            var selectable = EditorNodeSelectionState.CanSelectTreeNode(node)
                ? node
                : EditorNodeSelectionState.ClosestEditableNode(node);
            selectable = _nodeSelection.ResolveSelectionNode(selectable);
            _nodeSelection.RememberVariantSelection(selectable);
            var workspaceSelections = Copy(previous.WorkspaceSelections);
            if (previous.SelectedNode is not null)
            {
                workspaceSelections[previous.Workspace] =
                    previous.SelectedNode.Id;
            }
            workspaceSelections[workspace] = selectable.Id;
            var productionId = workspace == EditorWorkspace.Production
                ? Root(selectable).Id
                : previous.ProductionId;
            var revision = previous.Revision + 1;
            _state = new EditorSessionState(
                previous.TreeRoots,
                workspace,
                productionId,
                selectable,
                null,
                workspaceSelections,
                _nodeSelection.Snapshot(),
                Preview(workspace, selectable, revision),
                revision);
            var effects = EditorSessionEffects.Navigation
                | EditorSessionEffects.Editor
                | EditorSessionEffects.PreviewSelection;
            if (workspace != previous.Workspace)
            {
                effects |= EditorSessionEffects.Workspace;
            }
            if (!productionId.Equals(
                    previous.ProductionId,
                    StringComparison.Ordinal))
            {
                effects |= EditorSessionEffects.Production;
            }
            transition = new EditorSessionTransition(
                source,
                previous,
                _state,
                effects);
            return true;
        }
    }

    public bool TrySelectProduction(
        string productionId,
        string source,
        out EditorSessionTransition transition)
    {
        lock (_stateGate)
        {
            ThrowIfDisposed();
            var project = _state.TreeRoots.FirstOrDefault((candidate) =>
                candidate.Id.Equals(productionId, StringComparison.Ordinal));
            if (project is null)
            {
                transition = Unchanged(source);
                return false;
            }
            var selected = EditorWorkspaceNavigation.FirstSelectable(
                [project],
                EditorWorkspace.Production);
            if (selected is null)
            {
                transition = Unchanged(source);
                return false;
            }

            InvalidateActiveTreeLoad();
            selected = _nodeSelection.ResolveSelectionNode(selected);
            _nodeSelection.RememberVariantSelection(selected);
            var previous = _state;
            var workspaceSelections = Copy(previous.WorkspaceSelections);
            workspaceSelections[EditorWorkspace.Production] = selected.Id;
            var revision = previous.Revision + 1;
            _state = new EditorSessionState(
                previous.TreeRoots,
                EditorWorkspace.Production,
                project.Id,
                selected,
                null,
                workspaceSelections,
                _nodeSelection.Snapshot(),
                Preview(EditorWorkspace.Production, selected, revision),
                revision);
            var effects = EditorSessionEffects.Production
                | EditorSessionEffects.Navigation
                | EditorSessionEffects.Editor
                | EditorSessionEffects.PreviewSelection;
            if (previous.Workspace != EditorWorkspace.Production)
            {
                effects |= EditorSessionEffects.Workspace;
            }
            transition = new EditorSessionTransition(
                source,
                previous,
                _state,
                effects);
            return true;
        }
    }

    public EditorSessionTransition ShowEmbeddedEditor(
        EditorEmbeddedContext context,
        string source = "embedded")
    {
        lock (_stateGate)
        {
            ThrowIfDisposed();
            var owner = EditorNodeSelectionState.FindNodeById(
                    _state.TreeRoots,
                    context.OwnerNode.Id)
                ?? throw new InvalidOperationException(
                    $"Embedded owner '{context.OwnerNode.Id}' is not in the active tree.");
            if (_state.SelectedNode is null
                || !_state.SelectedNode.Id.Equals(
                    owner.Id,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Embedded editor context must belong to the active selection.");
            }

            InvalidateActiveTreeLoad();
            var previous = _state;
            var revision = previous.Revision + 1;
            var rebased = context with { OwnerNode = owner };
            _state = new EditorSessionState(
                previous.TreeRoots,
                previous.Workspace,
                previous.ProductionId,
                owner,
                rebased,
                previous.WorkspaceSelections,
                _nodeSelection.Snapshot(),
                Preview(previous.Workspace, owner, revision),
                revision);
            return new EditorSessionTransition(
                source,
                previous,
                _state,
                EditorSessionEffects.Editor
                    | EditorSessionEffects.PreviewSelection);
        }
    }

    public ProjectTreeNode ResolveSelectionNode(ProjectTreeNode node)
    {
        lock (_stateGate) return _nodeSelection.ResolveSelectionNode(node);
    }

    public ProjectTreeNode PreferredVariantNode(ProjectTreeNode node)
    {
        lock (_stateGate) return _nodeSelection.PreferredVariantNode(node);
    }

    public ProjectTreeNode PreferredModuleVariantNode(ProjectTreeNode node)
    {
        lock (_stateGate) return _nodeSelection.PreferredModuleVariantNode(node);
    }

    public bool IsCurrent(long revision, string ownerNodeId)
    {
        lock (_stateGate)
        {
            return !_disposed
                && _state.Revision == revision
                && _state.SelectedNode?.Id.Equals(
                    ownerNodeId,
                    StringComparison.Ordinal) == true;
        }
    }

    public void CancelPendingOperations()
    {
        lock (_stateGate) InvalidateActiveTreeLoad();
    }

    public void Dispose()
    {
        lock (_stateGate)
        {
            if (_disposed) return;
            InvalidateActiveTreeLoad();
            _disposed = true;
        }
    }

    private EditorSessionTransition Unchanged(string source)
    {
        lock (_stateGate)
        {
            return new EditorSessionTransition(
                source,
                _state,
                _state,
                EditorSessionEffects.None);
        }
    }

    private ProjectTreeNode? ResolveTreeSelection(
        IReadOnlyList<ProjectTreeNode> roots,
        EditorWorkspace workspace,
        EditorSessionState previous)
    {
        var selected = previous.SelectedNode is null
            ? null
            : EditorNodeSelectionState.FindNodeById(
                roots,
                previous.SelectedNode.Id);
        selected = IsValid(selected, workspace) ? selected : null;
        if (selected is null
            && previous.WorkspaceSelections.TryGetValue(
                workspace,
                out var selectionId))
        {
            var remembered = EditorNodeSelectionState.FindNodeById(
                roots,
                selectionId);
            selected = IsValid(remembered, workspace) ? remembered : null;
        }
        selected ??= EditorWorkspaceNavigation.FirstSelectable(roots, workspace)
            ?? roots.FirstOrDefault((node) => node.CanOpenEditor)
            ?? roots.FirstOrDefault();
        return selected is null
            ? null
            : _nodeSelection.ResolveSelectionNode(selected);
    }

    private static bool IsValid(
        ProjectTreeNode? node,
        EditorWorkspace workspace) =>
        node is not null
        && EditorNodeSelectionState.CanSelectTreeNode(node)
        && EditorWorkspaceNavigation.Contains(workspace, node);

    private static string ResolveProductionId(
        IReadOnlyList<ProjectTreeNode> roots,
        string productionId)
    {
        return roots.Any((project) =>
                project.Id.Equals(productionId, StringComparison.Ordinal))
            ? productionId
            : roots.FirstOrDefault()?.Id ?? "";
    }

    private static EditorEmbeddedContext? RebaseEmbeddedEditor(
        EditorEmbeddedContext? embedded,
        ProjectTreeNode? selected)
    {
        if (embedded is null
            || selected is null
            || !embedded.OwnerNode.Id.Equals(
                selected.Id,
                StringComparison.Ordinal))
        {
            return null;
        }
        return embedded with { OwnerNode = selected };
    }

    private static ProjectTreeNode Root(ProjectTreeNode node)
    {
        while (node.Parent is not null) node = node.Parent;
        return node;
    }

    private static Dictionary<EditorWorkspace, string> Copy(
        IReadOnlyDictionary<EditorWorkspace, string> source) =>
        new(source);

    private static PreviewSessionState Preview(
        EditorWorkspace workspace,
        ProjectTreeNode? selected,
        long revision) =>
        new(workspace, selected?.Id, revision);

    private void InvalidateActiveTreeLoad()
    {
        if (_activeTreeLoad is null) return;
        _activeTreeLoad.Cancellation.Cancel();
        _activeTreeLoad.Cancellation.Dispose();
        _activeTreeLoad = null;
    }

    private void AbandonTreeLoad(EditorTreeLoadOperation operation)
    {
        lock (_stateGate)
        {
            if (ReferenceEquals(_activeTreeLoad, operation))
            {
                InvalidateActiveTreeLoad();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
