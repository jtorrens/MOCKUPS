using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorProductionNavigationActions : IDisposable
{
    private readonly ShotManagerProductionNavigationAction _shotManager;
    private readonly RenderQueueController _renderQueue;

    public EditorProductionNavigationActions(
        Window owner,
        Button actionButton,
        SpikeDatabase database,
        Func<bool> isDark,
        Action<string> openProductionCard)
    {
        _shotManager = new ShotManagerProductionNavigationAction(
            actionButton,
            database,
            isDark,
            () => openProductionCard("integration:shot-manager"));
        _renderQueue = new RenderQueueController(owner, database);
    }

    public void Refresh(string? projectId)
    {
        _shotManager.Refresh(projectId);
    }

    public EditorNavigationRowAction? NodeAction(ProjectTreeNode node)
    {
        return _renderQueue.NavigationAction(node);
    }

    public void Dispose()
    {
        _renderQueue.Dispose();
    }
}
