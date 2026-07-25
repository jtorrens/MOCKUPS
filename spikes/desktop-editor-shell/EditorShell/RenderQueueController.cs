using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RenderQueueController : IDisposable
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
        if (node.Kind != ProjectTreeNodeKind.Shot) return null;
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
                : $"{node.Name} has no Shot Manager output route",
            EditorIcons.Render,
            () => _ = OpenAsync(node),
            available);
    }

    private async Task OpenAsync(ProjectTreeNode shot)
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(_queue.InitializationError))
            {
                throw new InvalidOperationException(
                    _queue.InitializationError);
            }
            var draft = await _snapshots.LoadDraftAsync(shot);
            await new RenderQueueDialog(
                _owner,
                _queue,
                _snapshots).Show(draft);
        }
        catch (Exception exception)
        {
            await new EditorDialogService(
                _owner,
                _owner.ActualThemeVariant
                    != Avalonia.Styling.ThemeVariant.Light)
                .ShowInfo("Render Queue", exception.Message);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    public void Dispose()
    {
        _queue.Dispose();
    }
}
