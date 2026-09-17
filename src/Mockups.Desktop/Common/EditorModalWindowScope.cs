using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorModalWindowScope
{
    public static void Configure(Window dialog, Window owner)
    {
        var rootOwner = RootOwner(owner);
        var displacedWindows = new Dictionary<Window, OwnedWindowState>();

        void DisplaceAuxiliaryWindows(object? sender, EventArgs args)
        {
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

                if (!displacedWindows.TryAdd(
                        window,
                        new OwnedWindowState(
                            window,
                            window.Topmost,
                            window.IsEnabled)))
                {
                    continue;
                }

                window.Topmost = false;
                window.IsEnabled = false;
            }
        }

        void RestoreAuxiliaryWindows(object? sender, EventArgs args)
        {
            dialog.Opened -= DisplaceAuxiliaryWindows;
            dialog.Opened -= ActivateDialog;
            dialog.Closed -= RestoreAuxiliaryWindows;
            foreach (var displaced in displacedWindows.Values)
            {
                displaced.Window.Topmost = displaced.WasTopmost;
                displaced.Window.IsEnabled = displaced.WasEnabled;
            }
            displacedWindows.Clear();
        }

        void ActivateDialog(object? sender, EventArgs args)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (dialog.IsVisible)
                    {
                        dialog.Activate();
                    }
                },
                DispatcherPriority.Input);
        }

        dialog.ShowActivated = true;
        dialog.Opened += DisplaceAuxiliaryWindows;
        dialog.Opened += ActivateDialog;
        dialog.Closed += RestoreAuxiliaryWindows;
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
