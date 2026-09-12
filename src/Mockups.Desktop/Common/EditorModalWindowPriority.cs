using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorModalWindowPriority
{
    private static readonly TimeSpan PriorityMonitorInterval =
        TimeSpan.FromMilliseconds(100);

    public static void Configure(Window dialog, Window owner)
    {
        var rootOwner = RootOwner(owner);
        var displacedWindows = new Dictionary<Window, OwnedWindowState>();
        var priorityMonitor = new DispatcherTimer
        {
            Interval = PriorityMonitorInterval,
        };
        var activationPending = false;
        var closed = false;

        bool HasActiveOwnerFamilyWindow() =>
            OwnerFamily(rootOwner).Any((window) =>
                window.IsVisible && window.IsActive);

        bool HasVisibleOwnedWindow() =>
            OwnerFamily(dialog)
                .Skip(1)
                .Any((window) => window.IsVisible);

        void DisplaceCompetingWindows()
        {
            foreach (var window in OwnerFamily(rootOwner))
            {
                if (ReferenceEquals(window, rootOwner)
                    || ReferenceEquals(window, dialog)
                    || IsOwnedBy(window, dialog)
                    || !window.IsVisible)
                {
                    continue;
                }

                if (!displacedWindows.ContainsKey(window))
                {
                    displacedWindows.Add(
                        window,
                        new OwnedWindowState(
                            window,
                            window.Topmost,
                            window.IsEnabled));
                }

                window.Topmost = false;
                if (!IsOwnedBy(dialog, window))
                {
                    window.IsEnabled = false;
                }
            }
        }

        void PromoteDialog(bool requireActiveOwnerFamily)
        {
            if (closed || !dialog.IsVisible || activationPending)
            {
                return;
            }
            activationPending = true;
            Dispatcher.UIThread.Post(
                () =>
                {
                    activationPending = false;
                    if (closed
                        || !dialog.IsVisible
                        || HasVisibleOwnedWindow()
                        || (requireActiveOwnerFamily
                            && !HasActiveOwnerFamilyWindow()))
                    {
                        return;
                    }
                    dialog.Topmost = false;
                    dialog.Topmost = true;
                    dialog.Activate();
                },
                DispatcherPriority.Background);
        }

        void RestoreDialogAfterOwnerActivation(
            object? sender,
            EventArgs args) =>
            PromoteDialog(requireActiveOwnerFamily: true);

        void RestoreDialogAfterDeactivation(
            object? sender,
            EventArgs args) =>
            PromoteDialog(requireActiveOwnerFamily: true);

        void MonitorPriority(object? sender, EventArgs args)
        {
            if (closed || !dialog.IsVisible)
            {
                return;
            }

            DisplaceCompetingWindows();
            if (!dialog.Topmost || !dialog.IsActive)
            {
                PromoteDialog(requireActiveOwnerFamily: true);
            }
        }

        dialog.ShowActivated = true;
        dialog.Topmost = true;
        owner.Activated += RestoreDialogAfterOwnerActivation;
        dialog.Deactivated += RestoreDialogAfterDeactivation;
        priorityMonitor.Tick += MonitorPriority;
        dialog.Opened += (_, _) =>
        {
            DisplaceCompetingWindows();
            PromoteDialog(requireActiveOwnerFamily: false);
            priorityMonitor.Start();
        };
        dialog.Closed += (_, _) =>
        {
            closed = true;
            priorityMonitor.Stop();
            priorityMonitor.Tick -= MonitorPriority;
            owner.Activated -= RestoreDialogAfterOwnerActivation;
            dialog.Deactivated -= RestoreDialogAfterDeactivation;
            foreach (var displaced in displacedWindows.Values)
            {
                displaced.Window.Topmost = displaced.WasTopmost;
                displaced.Window.IsEnabled = displaced.WasEnabled;
            }
            displacedWindows.Clear();
        };
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
