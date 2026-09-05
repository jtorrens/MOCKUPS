using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorModalWindowPriority
{
    private static readonly TimeSpan[] OpeningPromotionDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(150),
        TimeSpan.FromMilliseconds(350),
    ];

    public static void Configure(Window dialog, Window owner)
    {
        List<OwnedWindowState>? displacedWindows = null;
        var activationPending = false;
        var closed = false;

        void PromoteDialog(bool requireActiveApplicationWindow)
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
                        || (requireActiveApplicationWindow
                            && !owner.IsActive
                            && !dialog.IsActive))
                    {
                        return;
                    }
                    dialog.Topmost = true;
                    dialog.Activate();
                },
                DispatcherPriority.Background);
        }

        void RestoreDialogAfterOwnerActivation(
            object? sender,
            EventArgs args) =>
            PromoteDialog(requireActiveApplicationWindow: true);

        void RestoreDialogAfterDeactivation(
            object? sender,
            EventArgs args) =>
            PromoteDialog(requireActiveApplicationWindow: true);

        dialog.Topmost = true;
        owner.Activated += RestoreDialogAfterOwnerActivation;
        dialog.Deactivated += RestoreDialogAfterDeactivation;
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
            PromoteDialog(requireActiveApplicationWindow: false);
            foreach (var delay in OpeningPromotionDelays)
            {
                DispatcherTimer.RunOnce(
                    () => PromoteDialog(
                        requireActiveApplicationWindow: true),
                    delay);
            }
        };
        dialog.Closed += (_, _) =>
        {
            closed = true;
            owner.Activated -= RestoreDialogAfterOwnerActivation;
            dialog.Deactivated -= RestoreDialogAfterDeactivation;
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
