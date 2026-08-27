using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class PreviewAuthoringRefreshCoordinator
{
    private readonly Func<EditorWorkspace> _workspace;
    private readonly Action _refreshPreview;
    private readonly Func<Task> _refreshProductionAuthoring;

    public PreviewAuthoringRefreshCoordinator(
        Func<EditorWorkspace> workspace,
        Action refreshPreview,
        Func<Task> refreshProductionAuthoring)
    {
        _workspace = workspace;
        _refreshPreview = refreshPreview;
        _refreshProductionAuthoring = refreshProductionAuthoring;
    }

    public void Notify() => _ = NotifyAsync();

    internal async Task NotifyAsync()
    {
        if (_workspace() == EditorWorkspace.Production)
        {
            await _refreshProductionAuthoring();
            return;
        }

        _refreshPreview();
    }
}
