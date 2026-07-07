using Avalonia.Controls;
using Avalonia.Threading;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DeferredBringIntoView
{
    public static void Request(Control control)
    {
        Dispatcher.UIThread.Post(control.BringIntoView, DispatcherPriority.Loaded);
    }
}
