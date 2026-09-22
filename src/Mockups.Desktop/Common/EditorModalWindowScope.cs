using Avalonia.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorModalWindowScope
{
    public static async Task ShowDialog(Window dialog, Window owner)
    {
        var displacedWindows = DisplaceAuxiliaryWindows(dialog, owner);
        dialog.ShowActivated = true;
        try
        {
            await dialog.ShowDialog(owner);
        }
        finally
        {
            RestoreAuxiliaryWindows(displacedWindows);
        }
    }

    public static async Task<TResult?> ShowDialog<TResult>(
        Window dialog,
        Window owner)
    {
        var displacedWindows = DisplaceAuxiliaryWindows(dialog, owner);
        dialog.ShowActivated = true;
        try
        {
            return await dialog.ShowDialog<TResult?>(owner);
        }
        finally
        {
            RestoreAuxiliaryWindows(displacedWindows);
        }
    }

    private static Dictionary<Window, OwnedWindowState>
        DisplaceAuxiliaryWindows(Window dialog, Window owner)
    {
        var rootOwner = RootOwner(owner);
        var displacedWindows = new Dictionary<Window, OwnedWindowState>();
        foreach (var window in OwnerFamily(rootOwner))
        {
            if (ReferenceEquals(window, rootOwner)
                || ReferenceEquals(window, owner)
                || ReferenceEquals(window, dialog)
                || IsOwnedBy(window, dialog)
                || !window.IsVisible)
            {
                continue;
            }

            displacedWindows.Add(
                window,
                new OwnedWindowState(
                    window,
                    window.Topmost,
                    window.IsEnabled));
            window.Topmost = false;
            window.IsEnabled = false;
        }
        return displacedWindows;
    }

    private static void RestoreAuxiliaryWindows(
        Dictionary<Window, OwnedWindowState> displacedWindows)
    {
        foreach (var displaced in displacedWindows.Values)
        {
            displaced.Window.Topmost = displaced.WasTopmost;
            displaced.Window.IsEnabled = displaced.WasEnabled;
        }
        displacedWindows.Clear();
    }

    private static Window RootOwner(Window window)
    {
        var current = window;
        while (current.Owner is Window parent)
        {
            current = parent;
        }
        return current;
    }

    private static IEnumerable<Window> OwnerFamily(Window root)
    {
        var pending = new Stack<Window>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;
            foreach (var child in current.OwnedWindows)
            {
                pending.Push(child);
            }
        }
    }

    private static bool IsOwnedBy(Window window, Window possibleOwner)
    {
        for (var current = window.Owner;
             current is Window currentWindow;
             current = currentWindow.Owner)
        {
            if (ReferenceEquals(currentWindow, possibleOwner))
            {
                return true;
            }
        }
        return false;
    }

    private sealed record OwnedWindowState(
        Window Window,
        bool WasTopmost,
        bool WasEnabled);
}
