using Avalonia.Controls;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorModalWindowPriority
{
    public static void Configure(Window dialog, Window owner)
    {
        List<OwnedWindowState>? displacedWindows = null;
        dialog.Opened += (_, _) =>
        {
            displacedWindows = owner.OwnedWindows
                .Where((window) => !ReferenceEquals(window, dialog)
                    && window.IsVisible
                    && window.Topmost)
                .Select((window) => new OwnedWindowState(
                    window,
                    window.Topmost,
                    window.IsEnabled))
                .ToList();
            foreach (var displaced in displacedWindows)
            {
                displaced.Window.IsEnabled = false;
                displaced.Window.Topmost = false;
            }
            dialog.Topmost = true;
            dialog.Activate();
        };
        dialog.Closed += (_, _) =>
        {
            if (displacedWindows is null)
            {
                return;
            }
            foreach (var displaced in displacedWindows)
            {
                displaced.Window.Topmost = displaced.WasTopmost;
                displaced.Window.IsEnabled = displaced.WasEnabled;
            }
            displacedWindows = null;
        };
    }

    private sealed record OwnedWindowState(
        Window Window,
        bool WasTopmost,
        bool WasEnabled);
}
