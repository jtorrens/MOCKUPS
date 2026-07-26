using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ProductionOutput;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorProductionNavigationActions : IDisposable
{
    private readonly ProductionOutputNavigationAction _productionOutput;
    private readonly RenderQueueManager _queue;
    private readonly RenderQueueController _renderQueue;
    private readonly RenderQueueEditorSurface _renderQueueSurface;

    public EditorProductionNavigationActions(
        Window owner,
        Button actionButton,
        IProductionNavigationStore database,
        IProjectPathResolver projectPaths,
        ProductionOutputRootStore productionOutputRoots,
        Func<bool> isDark,
        Action<string> openProductionCard)
    {
        _productionOutput = new ProductionOutputNavigationAction(
            actionButton,
            productionOutputRoots,
            isDark,
            () => openProductionCard(
                ProductionOutputNavigationAction.CardSessionStateId));
        _queue = new RenderQueueManager();
        var snapshots = new RenderJobSnapshotFactory(
            database,
            projectPaths,
            productionOutputRoots);
        _renderQueue = new RenderQueueController(
            owner,
            database,
            projectPaths,
            _queue,
            snapshots);
        _renderQueueSurface = new RenderQueueEditorSurface(
            owner,
            _queue);
    }

    public void Refresh(string? projectId)
    {
        _productionOutput.Refresh(projectId);
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
