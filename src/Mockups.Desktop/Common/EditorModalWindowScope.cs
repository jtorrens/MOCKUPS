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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.Common;

internal interface IEditorModalOcclusionParticipant
{
    Task PrepareAsync();

    void Occlude();

    void Restore();
}

internal static class EditorModalWindowScope
{
    private static readonly ConditionalWeakTable<Window, ModalHost> Hosts = new();
    private static readonly Dictionary<Window, ModalSession> Sessions = [];
    private static readonly ConditionalWeakTable<Window, Lifecycle> Lifecycles = new();

    public static void RegisterHost(
        Window owner,
        Panel overlayHost,
        params IEditorModalOcclusionParticipant[] occlusionParticipants)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(overlayHost);
        ArgumentNullException.ThrowIfNull(occlusionParticipants);
        Hosts.Remove(owner);
        Hosts.Add(owner, new ModalHost(owner, overlayHost, occlusionParticipants));
    }

    public static void OnOpened(Window dialog, Action action) =>
        Lifecycles.GetOrCreateValue(dialog).Opened += action;

    public static void OnClosed(Window dialog, Action action) =>
        Lifecycles.GetOrCreateValue(dialog).Closed += action;

    public static async Task ShowDialog(Window dialog, Window owner)
    {
        await ShowDialog<object>(dialog, owner);
    }

    public static Task<TResult?> ShowDialog<TResult>(Window dialog, Window owner)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(owner);

        var host = ResolveHost(owner);
        var session = new ModalSession<TResult>(dialog, host);
        Sessions.Add(dialog, session);
        _ = OpenAsync(dialog, host, session);
        return session.Completion;
    }

    private static async Task OpenAsync(
        Window dialog,
        ModalHost host,
        ModalSession session)
    {
        try
        {
            await host.PushAsync(session);
            if (!session.IsClosed)
            {
                InvokeOpened(dialog);
            }
        }
        catch (Exception error)
        {
            Sessions.Remove(dialog);
            session.Fail(error);
        }
    }

    public static void Close(Window dialog) => Close<object>(dialog, null);

    public static void Close<TResult>(Window dialog, TResult? result)
    {
        if (!Sessions.Remove(dialog, out var session))
        {
            return;
        }

        session.Close(result);
        InvokeClosed(dialog);
    }

    private static ModalHost ResolveHost(Window requestedOwner)
    {
        if (Sessions.TryGetValue(requestedOwner, out var ownerSession))
        {
            return ownerSession.Host;
        }

        for (Window? current = requestedOwner;
             current is not null;
             current = current.Owner as Window)
        {
            if (Hosts.TryGetValue(current, out var host))
            {
                return host;
            }
        }

        throw new InvalidOperationException(
            "A modal dialog requires a registered application overlay host.");
    }

    private static void InvokeOpened(Window dialog)
    {
        if (!Lifecycles.TryGetValue(dialog, out var lifecycle))
        {
            return;
        }

        Dispatcher.UIThread.Post(
            lifecycle.InvokeOpened,
            DispatcherPriority.Loaded);
    }

    private static void InvokeClosed(Window dialog)
    {
        if (Lifecycles.TryGetValue(dialog, out var lifecycle))
        {
            lifecycle.InvokeClosed();
        }
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

    private sealed class ModalHost
    {
        private readonly Window _owner;
        private readonly Panel _overlayHost;
        private readonly IReadOnlyList<IEditorModalOcclusionParticipant> _occlusionParticipants;
        private readonly List<ModalSession> _stack = [];
        private WindowState[] _displaced = [];

        public ModalHost(
            Window owner,
            Panel overlayHost,
            IReadOnlyList<IEditorModalOcclusionParticipant> occlusionParticipants)
        {
            _owner = owner;
            _overlayHost = overlayHost;
            _occlusionParticipants = occlusionParticipants;
        }

        public Rect OwnerBounds => _owner.Bounds;
        public bool IsDark => EditorSukiWindowTheme.IsDark(_owner);

        public async Task PushAsync(ModalSession session)
        {
            if (_stack.Count == 0)
            {
                var windows = ApplicationWindows(_owner);
                CloseTransientSurfaces(windows.Prepend(_owner));
                foreach (var participant in _occlusionParticipants)
                {
                    await participant.PrepareAsync();
                }

                if (session.IsClosed)
                {
                    return;
                }

                _displaced = windows
                    .Select(window => new WindowState(
                        window,
                        window.Topmost,
                        window.IsEnabled))
                    .ToArray();
                foreach (var state in _displaced)
                {
                    state.Window.Topmost = false;
                    state.Window.IsEnabled = false;
                }

                foreach (var participant in _occlusionParticipants)
                {
                    participant.Occlude();
                }
            }
            else
            {
                _stack[^1].Surface.IsEnabled = false;
            }

            _stack.Add(session);
            _overlayHost.Children.Add(session.Surface);
            _overlayHost.IsVisible = true;
            session.FocusInitialControl();
        }

        public void Pop(ModalSession session)
        {
            if (!_stack.Remove(session))
            {
                return;
            }

            _overlayHost.Children.Remove(session.Surface);
            if (_stack.Count > 0)
            {
                _stack[^1].Surface.IsEnabled = true;
                _stack[^1].FocusInitialControl();
                return;
            }

            _overlayHost.IsVisible = false;
            foreach (var state in _displaced.Reverse())
            {
                state.Window.IsEnabled = state.WasEnabled;
                state.Window.Topmost = state.WasTopmost;
            }
            _displaced = [];
            foreach (var participant in _occlusionParticipants.Reverse())
            {
                participant.Restore();
            }
            _owner.Activate();
        }
    }

    private abstract class ModalSession
    {
        protected ModalSession(Window dialog, ModalHost host)
        {
            Dialog = dialog;
            Host = host;
            Surface = CreateSurface(dialog);
        }

        protected Window Dialog { get; }
        public ModalHost Host { get; }
        public Control Surface { get; }
        public bool IsClosed { get; private set; }
        public abstract void Close(object? result);
        public abstract void Fail(Exception error);

        protected bool MarkClosed()
        {
            if (IsClosed)
            {
                return false;
            }

            IsClosed = true;
            return true;
        }

        public void FocusInitialControl()
        {
            Dispatcher.UIThread.Post(
                () => Surface.GetVisualDescendants()
                    .OfType<InputElement>()
                    .FirstOrDefault(candidate => candidate.Focusable && candidate.IsEffectivelyEnabled)
                    ?.Focus(),
                DispatcherPriority.Input);
        }

        private Control CreateSurface(Window dialog)
        {
            var content = dialog.Content as Control
                ?? throw new InvalidOperationException(
                    $"Modal '{dialog.Title}' requires Control content.");
            dialog.Content = null;

            var close = new Button
            {
                Content = "×",
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                FontSize = 20,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
            };
            close.Click += (_, _) => EditorModalWindowScope.Close(dialog);

            var header = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Margin = new Thickness(18, 12, 12, 8),
                Children =
                {
                    new TextBlock
                    {
                        Text = dialog.Title,
                        FontSize = 17,
                        FontWeight = FontWeight.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    close,
                },
            };
            Grid.SetColumn(close, 1);

            var card = new Border
            {
                Width = Math.Min(Normalize(dialog.Width, 520), Math.Max(320, Host.OwnerBounds.Width - 48)),
                Height = Math.Min(Normalize(dialog.Height, 360), Math.Max(240, Host.OwnerBounds.Height - 48)),
                MinWidth = Math.Min(Normalize(dialog.MinWidth, 0), Math.Max(0, Host.OwnerBounds.Width - 48)),
                MinHeight = Math.Min(Normalize(dialog.MinHeight, 0), Math.Max(0, Host.OwnerBounds.Height - 48)),
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.Parse(
                    Host.IsDark ? "#4DFFFFFF" : "#33000000")),
                Background = dialog.Background
                    ?? EditorSukiWindowTheme.NeutralBackgroundBrush(Host.IsDark),
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 32,
                    OffsetY = 12,
                    Color = Color.Parse("#99000000"),
                }),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*"),
                    Children = { header, content },
                },
            };
            Grid.SetRow(content, 1);

            var backdrop = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#99000000")),
            };
            var surface = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Focusable = true,
                Children = { backdrop, card },
            };
            card.HorizontalAlignment = HorizontalAlignment.Center;
            card.VerticalAlignment = VerticalAlignment.Center;
            surface.KeyDown += (_, args) =>
            {
                if (args.Key != Key.Escape)
                {
                    return;
                }

                EditorModalWindowScope.Close(dialog);
                args.Handled = true;
            };
            return surface;
        }

        private static double Normalize(double value, double fallback) =>
            double.IsNaN(value) || double.IsInfinity(value) || value <= 0
                ? fallback
                : value;
    }

    private sealed class ModalSession<TResult> : ModalSession
    {
        private readonly TaskCompletionSource<TResult?> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ModalSession(Window dialog, ModalHost host)
            : base(dialog, host)
        {
        }

        public Task<TResult?> Completion => _completion.Task;

        public override void Close(object? result)
        {
            if (!MarkClosed())
            {
                return;
            }

            Host.Pop(this);
            _completion.TrySetResult(result is null ? default : (TResult)result);
        }

        public override void Fail(Exception error)
        {
            if (MarkClosed())
            {
                _completion.TrySetException(error);
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

    private sealed record WindowState(
        Window Window,
        bool WasTopmost,
        bool WasEnabled);

}
