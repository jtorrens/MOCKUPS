using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorModalWindowScope
{
    public static async Task ShowDialog(Window dialog, Window owner)
    {
        var presentation = await Prepare(dialog, owner);
        try
        {
            await dialog.ShowDialog(presentation.Owner);
        }
        finally
        {
            presentation.Restore();
        }
    }

    public static async Task<TResult?> ShowDialog<TResult>(
        Window dialog,
        Window owner)
    {
        var presentation = await Prepare(dialog, owner);
        try
        {
            return await dialog.ShowDialog<TResult?>(
                presentation.Owner);
        }
        finally
        {
            presentation.Restore();
        }
    }

    private static async Task<ModalPresentation> Prepare(
        Window dialog,
        Window requestedOwner)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(requestedOwner);

        var windows = ApplicationWindows(requestedOwner, dialog);
        CloseTransientSurfaces(windows);

        var owner = requestedOwner.IsVisible
            ? requestedOwner
            : windows.FirstOrDefault(window => window.IsActive)
                ?? throw new InvalidOperationException(
                    "A modal dialog requires a visible application owner.");
        var displaced = windows
            .Where(window => !ReferenceEquals(window, owner))
            .Select(window => new WindowState(
                window,
                window.Topmost,
                window.IsEnabled))
            .ToArray();

        foreach (var state in displaced)
        {
            state.Window.Topmost = false;
            state.Window.IsEnabled = false;
        }

        void Restore()
        {
            foreach (var state in displaced.Reverse())
            {
                state.Window.IsEnabled = state.WasEnabled;
                state.Window.Topmost = state.WasTopmost;
            }
        }

        dialog.ShowActivated = true;

        // Popup roots are native windows on macOS. Give Avalonia one dispatcher
        // turn to remove them before it materializes the owned modal window.
        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                static () => { },
                DispatcherPriority.Background);
        }
        catch
        {
            Restore();
            throw;
        }

        return new ModalPresentation(
            owner,
            Restore);
    }

    private static IReadOnlyList<Window> ApplicationWindows(
        Window requestedOwner,
        Window dialog)
    {
        var result = new List<Window>();

        void Add(Window window)
        {
            if (ReferenceEquals(window, dialog)
                || result.Contains(window))
            {
                return;
            }

            result.Add(window);
            foreach (var child in window.OwnedWindows)
            {
                Add(child);
            }
        }

        var root = requestedOwner;
        while (root.Owner is Window parent)
        {
            root = parent;
        }
        Add(root);

        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            foreach (var window in lifetime.Windows)
            {
                Add(window);
            }
        }

        return result
            .Where(window => window.IsVisible)
            .ToArray();
    }

    private static void CloseTransientSurfaces(
        IEnumerable<Window> windows)
    {
        foreach (var window in windows)
        {
            var controls = window
                .GetVisualDescendants()
                .OfType<Control>()
                .Prepend(window)
                .Distinct()
                .ToArray();

            foreach (var control in controls)
            {
                if (ToolTip.GetIsOpen(control))
                {
                    ToolTip.SetIsOpen(control, false);
                }

                control.ContextFlyout?.Hide();
                FlyoutBase.GetAttachedFlyout(control)?.Hide();
            }

            foreach (var popup in window
                         .GetLogicalDescendants()
                         .OfType<Popup>())
            {
                popup.IsOpen = false;
            }
        }
    }

    private sealed record WindowState(
        Window Window,
        bool WasTopmost,
        bool WasEnabled);

    private sealed record ModalPresentation(
        Window Owner,
        Action Restore);
}
