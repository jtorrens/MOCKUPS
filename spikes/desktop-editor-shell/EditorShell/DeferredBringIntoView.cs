using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DeferredBringIntoView
{
    public static void Request(Control control)
    {
        EventHandler? afterLayout = null;
        afterLayout = (_, _) =>
        {
            control.LayoutUpdated -= afterLayout;
            Dispatcher.UIThread.Post(() => EnsureVisible(control), DispatcherPriority.Render);
        };
        control.LayoutUpdated += afterLayout;
        Dispatcher.UIThread.Post(() => EnsureVisible(control), DispatcherPriority.Background);
        DispatcherTimer.RunOnce(() => EnsureVisible(control), TimeSpan.FromMilliseconds(50));
        DispatcherTimer.RunOnce(() => EnsureVisible(control), TimeSpan.FromMilliseconds(150));
    }

    private static void EnsureVisible(Control control)
    {
        // A card can live inside more than one scroll host. Move the nearest
        // host that can actually scroll vertically, not an outer shell host.
        var scrollViewer = control.GetVisualAncestors()
            .OfType<ScrollViewer>()
            .FirstOrDefault((candidate) => candidate.Extent.Height > candidate.Viewport.Height + 0.5)
            ?? control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer is null || control.Bounds.Height <= 0)
        {
            control.BringIntoView();
            return;
        }

        var transform = control.TransformToVisual(scrollViewer);
        if (transform is null)
        {
            control.BringIntoView();
            return;
        }

        var topLeft = transform.Value.Transform(new Point(0, 0));
        var bottomRight = transform.Value.Transform(new Point(control.Bounds.Width, control.Bounds.Height));
        var bounds = new Rect(topLeft, bottomRight);
        var viewportHeight = scrollViewer.Viewport.Height > 0
            ? scrollViewer.Viewport.Height
            : scrollViewer.Bounds.Height;
        if (viewportHeight <= 0)
        {
            control.BringIntoView();
            return;
        }
        var delta = 0.0;
        if (bounds.Height > viewportHeight)
        {
            delta = bounds.Top;
        }
        else if (bounds.Top < 0)
        {
            delta = bounds.Top;
        }
        else if (bounds.Bottom > viewportHeight)
        {
            delta = bounds.Bottom - viewportHeight;
        }

        if (System.Math.Abs(delta) < 0.5)
        {
            return;
        }

        var maximum = System.Math.Max(0, scrollViewer.Extent.Height - viewportHeight);
        var nextY = System.Math.Clamp(scrollViewer.Offset.Y + delta, 0, maximum);
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, nextY);
    }
}
