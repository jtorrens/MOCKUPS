using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorProductionNavigationActions
{
    private readonly ShotManagerProductionNavigationAction _shotManager;

    public EditorProductionNavigationActions(
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
    }

    public void Refresh(string? projectId)
    {
        _shotManager.Refresh(projectId);
    }
}
