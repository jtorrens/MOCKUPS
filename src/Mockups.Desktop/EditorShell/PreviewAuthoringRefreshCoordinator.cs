using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class PreviewAuthoringRefreshCoordinator
{
    private readonly Func<EditorWorkspace> _workspace;
    private readonly Action _refreshPreview;
    private readonly Action _refreshProductionSession;

    public PreviewAuthoringRefreshCoordinator(
        Func<EditorWorkspace> workspace,
        Action refreshPreview,
        Action refreshProductionSession)
    {
        _workspace = workspace;
        _refreshPreview = refreshPreview;
        _refreshProductionSession = refreshProductionSession;
    }

    public void Notify()
    {
        if (_workspace() == EditorWorkspace.Production)
        {
            _refreshProductionSession();
            return;
        }

        _refreshPreview();
    }
}
