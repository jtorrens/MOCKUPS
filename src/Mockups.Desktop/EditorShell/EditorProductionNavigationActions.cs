using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorProductionNavigationActions : IDisposable
{
    private readonly ShotManagerProductionNavigationAction _shotManager;
    private readonly RenderQueueManager _queue;
    private readonly RenderQueueController _renderQueue;
    private readonly RenderQueueEditorSurface _renderQueueSurface;

    public EditorProductionNavigationActions(
        Window owner,
        Button actionButton,
        IProductionNavigationStore database,
        Func<bool> isDark,
        Action<string> openProductionCard)
    {
        _shotManager = new ShotManagerProductionNavigationAction(
            actionButton,
            database,
            isDark,
            () => openProductionCard("integration:shot-manager"));
        _queue = new RenderQueueManager();
        var snapshots = new RenderJobSnapshotFactory(database);
        _renderQueue = new RenderQueueController(
            owner,
            database,
            _queue,
            snapshots);
        _renderQueueSurface = new RenderQueueEditorSurface(
            owner,
            _queue);
    }

    public void Refresh(string? projectId)
    {
        _shotManager.Refresh(projectId);
    }

    public EditorNavigationRowAction? NodeAction(ProjectTreeNode node)
    {
        return _renderQueue.NavigationAction(node);
    }

    public IReadOnlyList<InstantEditorCard>? EditorCards(
        ProjectTreeNode node)
    {
        return _renderQueueSurface.CreateCards(node);
    }

    public void Dispose()
    {
        _queue.Dispose();
    }
}
