using Mockups.DesktopEditorShell.Data;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorInlinePreviewControllerFactory
{
    public static IEditorInlinePreviewController Create(IActorPreviewRepository database, Func<bool> isDark)
    {
        return new ActorAvatarPreviewController(new ActorPreviewDataSource(database), isDark);
    }
}
