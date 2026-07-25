using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RenderQueueController
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;
    private readonly RenderQueueManager _queue;
    private readonly RenderJobSnapshotFactory _snapshots;
    private bool _dialogOpen;

    public RenderQueueController(
        Window owner,
        SpikeDatabase database,
        RenderQueueManager? queue = null,
        RenderJobSnapshotFactory? snapshots = null)
    {
        _owner = owner;
        _database = database;
        _queue = queue ?? new RenderQueueManager();
        _snapshots = snapshots ?? new RenderJobSnapshotFactory(database);
    }

    public EditorNavigationRowAction? NavigationAction(ProjectTreeNode node)
    {
        if (!OwnsNavigationAction(node)) return null;
        var available = false;
        try
        {
            var record = _database.GetShotManagerShotStructure(node.Id);
            available = record is not null
                && ShotManagerPortableStructure.Parse(
                    record.StructureJson,
                    $"Shot Manager Shot '{node.Id}' structure")
                    .OutputContracts.Count > 0;
        }
        catch
        {
            available = false;
        }
        return new EditorNavigationRowAction(
            available
                ? $"Add {node.Name} to Render Queue"
                : $"Open Render Queue for {node.Name} · output route unavailable",
            EditorIcons.Render,
            () => _ = OpenAsync(node),
            true);
    }

    internal static bool OwnsNavigationAction(
        ProjectTreeNode node) =>
        node.Kind == ProjectTreeNodeKind.Shot;

    private async Task OpenAsync(ProjectTreeNode shot)
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        try
        {
            await new RenderQueueDialog(
                _owner,
                _queue,
                _snapshots).Show(
                    shot,
                    LoadDraft);
        }
        finally
        {
            _dialogOpen = false;
        }

        async Task<RenderQueueShotDraft> LoadDraft(
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_queue.InitializationError))
            {
                throw new InvalidOperationException(
                    _queue.InitializationError);
            }
            return await _snapshots.LoadDraftAsync(
                shot,
                cancellationToken);
        }
    }
}
