using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.Common;

internal interface IEditorModalOcclusionParticipant
{
    void Occlude();
    void Restore();
}

internal static class EditorModalWindowScope
{
    private static readonly ConditionalWeakTable<Window, IEditorModalOcclusionParticipant[]> Hosts = new();
    private static readonly ConditionalWeakTable<Window, Lifecycle> Lifecycles = new();
    private static readonly Dictionary<Window, Window> ActiveChildren = [];

    public static bool IsModalActive => ActiveChildren.Count > 0;
    public static event Action? ModalActivityChanged;

    public static void RegisterHost(Window owner, params IEditorModalOcclusionParticipant[] participants)
    {
        Hosts.Remove(owner);
        Hosts.Add(owner, participants);
    }

    public static void OnOpened(Window dialog, Action action) =>
        Lifecycles.GetOrCreateValue(dialog).Opened += action;
    public static void OnClosed(Window dialog, Action action) =>
        Lifecycles.GetOrCreateValue(dialog).Closed += action;
    public static Task ShowDialog(Window dialog, Window owner) => ShowDialog<object>(dialog, owner);

    public static async Task<TResult?> ShowDialog<TResult>(Window dialog, Window owner)
    {
        while (ActiveChildren.TryGetValue(owner, out var child)) owner = child;
        var timer = Stopwatch.StartNew();
        var windows = ApplicationWindows(owner);
        CloseTransientSurfaces(windows.Prepend(owner));
        var displaced = windows.Select(window => (Window: window, window.Topmost, window.IsEnabled)).ToArray();
        var participants = Hosts.TryGetValue(owner, out var registered) ? registered : [];
        var occluded = new List<IEditorModalOcclusionParticipant>();
        void Opened(object? sender, EventArgs args)
        {
            PreviewDebugLog.Write("dialog.open", ("title", dialog.Title), ("ms", timer.Elapsed.TotalMilliseconds));
            if (Lifecycles.TryGetValue(dialog, out var lifecycle)) lifecycle.InvokeOpened();
        }
        void Closed(object? sender, EventArgs args)
        {
            if (Lifecycles.TryGetValue(dialog, out var lifecycle)) lifecycle.InvokeClosed();
        }
        dialog.Opened += Opened;
        dialog.Closed += Closed;
        try
        {
            foreach (var state in displaced)
            {
                state.Window.Topmost = false;
                state.Window.IsEnabled = false;
            }
            foreach (var participant in participants)
            {
                occluded.Add(participant);
                participant.Occlude();
            }
            // Native ownership keeps the modal above native WebViews without
            // capturing, hiding or reloading their resident presentation.
            ActiveChildren.Add(owner, dialog);
            ModalActivityChanged?.Invoke();
            return await dialog.ShowDialog<TResult>(owner);
        }
        finally
        {
            if (ActiveChildren.GetValueOrDefault(owner) == dialog) ActiveChildren.Remove(owner);
            ModalActivityChanged?.Invoke();
            dialog.Opened -= Opened;
            dialog.Closed -= Closed;
            foreach (var state in displaced.Reverse())
            {
                state.Window.IsEnabled = state.IsEnabled;
                state.Window.Topmost = state.Topmost;
            }
            foreach (var participant in occluded.AsEnumerable().Reverse()) participant.Restore();
        }
    }

    public static void Close(Window dialog) => dialog.Close();
    public static void Close<TResult>(Window dialog, TResult? result) => dialog.Close(result);

    public static async Task<TResult?> ShowPrepared<TPrepared, TResult>(
        Window dialog, Window owner, string message,
        Func<CancellationToken, Task<TPrepared>> prepare,
        Action<TPrepared> present)
    {
        using var cancellation = new CancellationTokenSource();
        var status = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        var progress = new ProgressBar { IsIndeterminate = true, Height = 4 };
        var cancel = new Button { Content = "Cancel", MinWidth = 92, HorizontalAlignment = HorizontalAlignment.Right };
        cancel.Click += (_, _) => Close(dialog);
        dialog.Content = new Border
        {
            Padding = new Avalonia.Thickness(22),
            Child = new StackPanel { Spacing = 18, Children = { status, progress, cancel } },
        };
        var loadingContent = dialog.Content;
        OnClosed(dialog, cancellation.Cancel);
        OnOpened(dialog, async () =>
        {
            var timer = Stopwatch.StartNew();
            try
            {
                // Let the loading surface paint before even a completed read
                // can replace it with a potentially large form.
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                cancellation.Token.ThrowIfCancellationRequested();
                var prepared = await prepare(cancellation.Token);
                if (!cancellation.IsCancellationRequested) present(prepared);
                PreviewDebugLog.Write("dialog.prepare", ("title", dialog.Title), ("ms", timer.Elapsed.TotalMilliseconds));
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (cancellation.IsCancellationRequested) return;
                dialog.Content = loadingContent;
                status.Text = error.Message;
                progress.IsVisible = false;
                cancel.Content = "Close";
                PreviewDebugLog.Write("dialog.prepare.failed", ("title", dialog.Title), ("ms", timer.Elapsed.TotalMilliseconds), ("error", error.Message));
            }
        });
        try { return await ShowDialog<TResult>(dialog, owner); }
        finally { cancellation.Cancel(); }
    }

    // The same native modal owns execution after confirmation. Closing is blocked
    // until the write and its UI refresh finish; failures remain visible here.
    public static async Task ExecuteConfirmed<TResult>(Window dialog, string message,
        Func<Task> execute, TResult result)
    {
        var status = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        var progress = new ProgressBar { IsIndeterminate = true, Height = 4 };
        var close = new Button { Content = "Close", MinWidth = 92, IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close(dialog);
        dialog.Content = new Border
        {
            Padding = new Thickness(22),
            Child = new StackPanel { Spacing = 18, Children = { status, progress, close } },
        };
        void PreventClose(object? sender, WindowClosingEventArgs args) => args.Cancel = true;
        dialog.Closing += PreventClose;
        try
        {
            await execute();
        }
        catch (Exception error)
        {
            status.Text = error.Message;
            progress.IsVisible = false;
            close.IsVisible = true;
            return;
        }
        finally
        {
            dialog.Closing -= PreventClose;
        }
        Close(dialog, result);
    }

    private static IReadOnlyList<Window> ApplicationWindows(Window owner)
    {
        var windows = new HashSet<Window>();
        AddOwnedWindows(owner, windows);
        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            foreach (var window in lifetime.Windows)
            {
                if (window.IsVisible && !ReferenceEquals(window, owner))
                {
                    windows.Add(window);
                }
            }
        }

        return windows.ToArray();
    }

    private static void AddOwnedWindows(
        Window owner,
        ISet<Window> windows)
    {
        foreach (var window in owner.OwnedWindows)
        {
            if (!window.IsVisible || !windows.Add(window))
            {
                continue;
            }

            AddOwnedWindows(window, windows);
        }
    }

    private static void CloseTransientSurfaces(IEnumerable<Window> windows)
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

            foreach (var popup in window.GetLogicalDescendants().OfType<Popup>())
            {
                popup.IsOpen = false;
            }
        }
    }

    private sealed class Lifecycle
    {
        public event Action? Opened;
        public event Action? Closed;
        public void InvokeOpened() => Opened?.Invoke();
        public void InvokeClosed() => Closed?.Invoke();
    }
}
