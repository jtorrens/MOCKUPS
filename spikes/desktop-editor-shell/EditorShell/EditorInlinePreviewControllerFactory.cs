using Mockups.DesktopEditorShell.Data;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorInlinePreviewControllerFactory
{
    public static IEditorInlinePreviewController Create(SpikeDatabase database, Func<bool> isDark)
    {
        return new ActorAvatarPreviewController(new ActorPreviewDataSource(database), isDark);
    }
}
