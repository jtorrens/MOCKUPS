using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorInlinePreviewControllerFactory
{
    public static IEditorInlinePreviewController Create(
        IActorPreviewRepository database,
        IProjectPathResolver projectPaths,
        Func<bool> isDark)
    {
        return new ActorAvatarPreviewController(
            new ActorPreviewDataSource(database),
            projectPaths,
            isDark);
    }
}
