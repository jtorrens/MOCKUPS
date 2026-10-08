using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class PreviewAuthoringRefreshCoordinator
{
    private readonly Func<EditorWorkspace> _previewWorkspace;
    private readonly Action _refreshPreview;
    private readonly Func<Task<bool>> _refreshProductionSession;
    private readonly Func<Task> _refreshProductionAuthoring;

    public PreviewAuthoringRefreshCoordinator(
        Func<EditorWorkspace> previewWorkspace,
        Action refreshPreview,
        Func<Task<bool>> refreshProductionSession,
        Func<Task> refreshProductionAuthoring)
    {
        _previewWorkspace = previewWorkspace;
        _refreshPreview = refreshPreview;
        _refreshProductionSession = refreshProductionSession;
        _refreshProductionAuthoring = refreshProductionAuthoring;
    }

    public void Notify() => _ = NotifyAsync();

    internal async Task NotifyAsync()
    {
        if (_previewWorkspace() == EditorWorkspace.Production)
        {
            if (await _refreshProductionSession())
            {
                await _refreshProductionAuthoring();
            }
            return;
        }

        _refreshPreview();
    }
}
