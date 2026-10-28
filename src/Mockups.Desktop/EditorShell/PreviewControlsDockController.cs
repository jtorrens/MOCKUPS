using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

// Owns visual placement only. Each tab retains its exact live content and the
// existing Preview/session owners continue to supply all authoring state.
internal sealed class PreviewControlsDockController : IDisposable, IEditorModalOcclusionParticipant
{
    private readonly Window _owner;
    private readonly Border _surface;
    private readonly TabControl _tabs;
    private readonly Grid _previewGrid;
    private readonly GridSplitter _splitter;
    private readonly Button _detachButton;
    private readonly Func<bool> _isDark;
    private readonly Entry[] _entries;
    private readonly Border _dropCue;
    private readonly TextBlock _dropLabel;
    private double _dockedHeight = PreviewPanelLayoutPolicy.MinimumPreviewUtilityHeight;
    private double _textScale = 1;
    private bool _collapsed;
    private bool _disposed;
    private bool _occluded;
    private Drag? _drag;

    public PreviewControlsDockController(Window owner, Border surface, TabControl tabs,
        Grid previewGrid, GridSplitter splitter, Button detachButton, Func<bool> isDark)
    {
        _owner = owner;
        _surface = surface;
        _tabs = tabs;
        _previewGrid = previewGrid;
        _splitter = splitter;
        _detachButton = detachButton;
        _isDark = isDark;
        _entries = tabs.Items.Cast<TabItem>().Select(tab => new Entry(tab)).ToArray();
        _dropLabel = new TextBlock
        {
            Text = "Drop a tab here to dock",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _dropCue = new Border
        {
            Child = _dropLabel,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(2),
            IsHitTestVisible = false,
            IsVisible = false,
        };
        surface.Child = null;
        surface.Child = new Grid { Children = { tabs, _dropCue } };
        foreach (var entry in _entries)
        {
            entry.Tab.PropertyChanged += OnTabPropertyChanged;
            BindDragHandle(entry.Tab);
            ToolTip.SetTip(entry.Tab, "Drag this tab outside the panel to detach");
        }
        _detachButton.Background = Brushes.Transparent;
        _detachButton.BorderBrush = Brushes.Transparent;
        _detachButton.BorderThickness = new Thickness(0);
        _detachButton.Content = EditorIcons.Create(EditorIcons.Open, 15);
        _detachButton.Click += OnDetachClick;
        _tabs.SelectionChanged += OnSelectionChanged;
        _owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, true);
        RefreshLayout();
    }

    internal Window? FloatingWindow(TabItem tab) => EntryFor(tab).Window;
    internal bool IsDetached(TabItem tab) => EntryFor(tab).Detached;
    public event EventHandler<KeyEventArgs>? PreviewKeyDown;

    public void SelectTab(TabItem tab, bool activate = false)
    {
        var entry = EntryFor(tab);
        if (entry.Detached)
        {
            if (activate && !_occluded && tab.IsVisible) entry.Window?.Activate();
            EnsureDockSelection();
        }
        else if (tab.IsVisible) _tabs.SelectedItem = tab;
        RefreshLayout();
    }

    public void RefreshTheme()
    {
        foreach (var entry in _entries)
            if (entry.Window is { } window)
                EditorSukiWindowTheme.ApplyNeutralBackground(window, _isDark());
        _dropCue.BorderBrush = EditorUiVisuals.FocusBrush(_isDark());
        _dropCue.Background = EditorUiVisuals.SelectedBackgroundBrush(_isDark());
        _dropLabel.Foreground = EditorUiVisuals.SelectedTextBrush(_isDark());
    }

    public void ApplyTextScale(double scale)
    {
        _textScale = scale;
        foreach (var entry in _entries)
            if (entry.Detached && entry.Window is { } window) EditorUiTextScale.Apply(window, scale);
    }

    public void Occlude()
    {
        CancelDrag();
        _occluded = true;
        foreach (var entry in _entries.Where(entry => entry.Detached))
        {
            CaptureGeometry(entry);
            entry.Window?.Hide();
        }
    }

    public void Restore()
    {
        _occluded = false;
        if (_disposed) return;
        foreach (var entry in _entries.Where(entry => entry.Detached && entry.Tab.IsVisible))
            Show(entry);
    }

    public void Dispose()
    {
        if (_disposed) return;
        CancelDrag();
        _disposed = true;
        _detachButton.Click -= OnDetachClick;
        _tabs.SelectionChanged -= OnSelectionChanged;
        _owner.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        foreach (var entry in _entries)
        {
            entry.Revision++;
            entry.Tab.PropertyChanged -= OnTabPropertyChanged;
            UnbindDragHandle(entry.Tab);
            if (entry.Handle is { } handle) UnbindDragHandle(handle);
            entry.Host.Content = null;
            entry.Window?.Close();
        }
    }

    private Entry EntryFor(TabItem tab) => _entries.Single(entry => ReferenceEquals(entry.Tab, tab));

    private void OnDetachClick(object? sender, RoutedEventArgs args)
    {
        if (_tabs.SelectedItem is TabItem tab) Detach(EntryFor(tab));
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (ReferenceEquals(args.Source, _tabs)) RefreshLayout();
    }

    private void OnTabPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (sender is not TabItem tab) return;
        var entry = EntryFor(tab);
        if (args.Property == TabItem.HeaderProperty && entry.Window is { } window)
        {
            window.Title = tab.Header?.ToString();
            entry.Label!.Text = window.Title;
        }
        if (args.Property != Visual.IsVisibleProperty) return;
        if (entry.Detached)
        {
            if (tab.IsVisible && !_occluded) Show(entry);
            else
            {
                if (_drag?.Entry == entry) CancelDrag();
                CaptureGeometry(entry);
                entry.Window?.Hide();
            }
        }
        EnsureDockSelection();
        RefreshLayout();
    }

    private void EnsureDockSelection()
    {
        if (_tabs.SelectedItem is TabItem current && current.IsVisible && !EntryFor(current).Detached) return;
        _tabs.SelectedItem = _entries.FirstOrDefault(entry => !entry.Detached && entry.Tab.IsVisible)?.Tab;
    }

    private void Detach(Entry entry, PixelPoint? position = null)
    {
        if (_disposed || _occluded || entry.Detached || !entry.Tab.IsVisible) return;
        var scroll = CaptureScroll(entry.Content);
        entry.Tab.Content = null;
        entry.Detached = true;
        _tabs.Items.Remove(entry.Tab);
        EnsureDockSelection();
        RefreshLayout();
        EnsureWindow(entry);
        if (position is { } dropPosition) entry.Position = dropPosition;
        Show(entry);
        entry.Window!.Activate();
        Transfer(entry, () => entry.Host.Content = entry.Content, scroll);
    }

    private void Redock(Entry entry)
    {
        if (_disposed || !entry.Detached) return;
        var scroll = CaptureScroll(entry.Content);
        CaptureGeometry(entry);
        entry.Host.Content = null;
        entry.Detached = false;
        entry.Window?.Hide();
        var index = _entries.TakeWhile(candidate => candidate != entry).Count(candidate => !candidate.Detached);
        _tabs.Items.Insert(index, entry.Tab);
        if (entry.Tab.IsVisible) _tabs.SelectedItem = entry.Tab;
        EnsureDockSelection();
        RefreshLayout();
        Transfer(entry, () => entry.Tab.Content = entry.Content, scroll);
    }

    private void Transfer(Entry entry, Action attach, (ScrollViewer Viewer, Vector Offset)[] scroll)
    {
        var revision = ++entry.Revision;
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || entry.Revision != revision) return;
            attach();
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed || entry.Revision != revision) return;
                foreach (var state in scroll) state.Viewer.Offset = state.Offset;
                if (entry.Detached && entry.Window is { } window) EditorUiTextScale.Apply(window, _textScale);
            }, DispatcherPriority.Loaded);
        }, DispatcherPriority.Loaded);
    }

    private static (ScrollViewer Viewer, Vector Offset)[] CaptureScroll(Control content) =>
        content.GetVisualDescendants().Prepend(content).OfType<ScrollViewer>()
            .Select(viewer => (viewer, viewer.Offset)).ToArray();

    private void EnsureWindow(Entry entry)
    {
        if (entry.Window is not null) return;
        entry.Label = new TextBlock { Text = entry.Tab.Header?.ToString(), VerticalAlignment = VerticalAlignment.Center };
        var dock = EditorTimelineTransport.CreateNavigationButton(EditorIcons.Create(EditorIcons.Back, 15), "Return to panel");
        ToolTip.SetTip(dock, "Return to panel");
        dock.Click += (_, _) => Redock(entry);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { entry.Label, dock } };
        Grid.SetColumn(dock, 1);
        entry.Handle = new Border { Padding = new Thickness(12, 6), Background = Brushes.Transparent, Child = header };
        ToolTip.SetTip(entry.Handle, "Drag this header onto the original panel to dock");
        BindDragHandle(entry.Handle);
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { entry.Handle, entry.Host } };
        Grid.SetRow(entry.Host, 1);
        var window = new SukiWindow
        {
            Title = entry.Tab.Header?.ToString(), MinWidth = 560, MinHeight = 280,
            CanResize = true, CanFullScreen = false, CanPin = false,
            ShowInTaskbar = false, Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = layout,
        };
        EditorSukiWindowTheme.ApplyUtilityWindowChrome(window, _owner);
        window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, true);
        window.Closing += (_, args) =>
        {
            if (_disposed) return;
            args.Cancel = true;
            Redock(entry);
        };
        entry.Window = window;
    }

    private void Show(Entry entry)
    {
        var window = entry.Window!;
        window.Width = entry.Width;
        window.Height = entry.Height;
        window.Position = entry.Position ?? new PixelPoint(_owner.Position.X + 48, _owner.Position.Y + 72);
        window.Show(_owner);
    }

    private static void CaptureGeometry(Entry entry)
    {
        if (entry.Window is not { IsVisible: true } window) return;
        entry.Position = window.Position;
        entry.Width = window.Width;
        entry.Height = window.Height;
    }

    private void RefreshLayout()
    {
        var empty = !_entries.Any(entry => !entry.Detached && entry.Tab.IsVisible);
        var row = _previewGrid.RowDefinitions[0];
        if (empty && !_collapsed && row.ActualHeight > 0) _dockedHeight = row.ActualHeight;
        if (empty != _collapsed || row.Height.IsAuto)
        {
            row.MinHeight = empty ? 48 : PreviewPanelLayoutPolicy.MinimumPreviewUtilityHeight;
            row.Height = new GridLength(empty ? 48 : _dockedHeight);
        }
        _collapsed = empty;
        _surface.MinHeight = empty ? 48 : PreviewPanelLayoutPolicy.MinimumPreviewUtilityHeight;
        _previewGrid.RowDefinitions[1].Height = new GridLength(empty ? 0 : 10);
        _splitter.IsVisible = !empty;
        _tabs.IsVisible = !empty;
        _detachButton.IsEnabled = _tabs.SelectedItem is TabItem selected && selected.IsVisible;
        var description = _tabs.SelectedItem is TabItem active ? $"Detach {active.Header}" : "Detach selected tab";
        EditorAccessibility.Describe(_detachButton, description);
        ToolTip.SetTip(_detachButton, description);
        _dropCue.IsVisible = empty || _drag is { Started: true, OverDock: true };
        RefreshTheme();
    }

    private void BindDragHandle(Control handle)
    {
        handle.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, true);
        handle.PointerMoved += OnPointerMoved;
        handle.PointerReleased += OnPointerReleased;
        handle.PointerCaptureLost += OnPointerCaptureLost;
    }

    private void UnbindDragHandle(Control handle)
    {
        handle.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        handle.PointerMoved -= OnPointerMoved;
        handle.PointerReleased -= OnPointerReleased;
        handle.PointerCaptureLost -= OnPointerCaptureLost;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (_occluded || sender is not Control handle
            || !args.GetCurrentPoint(handle).Properties.IsLeftButtonPressed
            || args.Source is Visual source && source.GetSelfAndVisualAncestors().OfType<Button>().Any()) return;
        var entry = _entries.Single(candidate => candidate.Tab == handle || candidate.Handle == handle);
        _drag = new Drag(entry, handle, args.Pointer, handle.PointToScreen(args.GetPosition(handle)), entry.Window?.Position);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (_drag is not { } drag || sender != drag.Handle) return;
        var point = drag.Handle.PointToScreen(args.GetPosition(drag.Handle));
        if (!drag.Started)
        {
            var delta = point - drag.Start;
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 8) return;
            drag.Started = true;
            args.Pointer.Capture(drag.Handle);
        }
        drag.OverDock = IsOverDock(point);
        if (drag.Entry.Detached && drag.WindowOrigin is { } origin)
            drag.Entry.Window!.Position = origin + (point - drag.Start);
        RefreshLayout();
        args.Handled = true;
    }

    private bool IsOverDock(PixelPoint point) => _surface.IsEffectivelyVisible
        && new Rect(_surface.Bounds.Size).Contains(_surface.PointToClient(point));

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_drag is not { } drag || sender != drag.Handle) return;
        var point = drag.Handle.PointToScreen(args.GetPosition(drag.Handle));
        _drag = null;
        args.Pointer.Capture(null);
        if (drag.Started)
        {
            if (drag.Entry.Detached)
            {
                if (IsOverDock(point)) Redock(drag.Entry);
                else CaptureGeometry(drag.Entry);
            }
            else if (!IsOverDock(point)) Detach(drag.Entry, point);
            args.Handled = true;
        }
        RefreshLayout();
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs args) => CancelDrag();

    private void CancelDrag()
    {
        if (_drag is not { } drag) return;
        _drag = null;
        if (drag.Entry.Detached && drag.WindowOrigin is { } origin) drag.Entry.Window!.Position = origin;
        drag.Pointer.Capture(null);
        RefreshLayout();
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape && _drag is not null)
        {
            CancelDrag();
            args.Handled = true;
            return;
        }
        if (sender != _owner) PreviewKeyDown?.Invoke(sender, args);
    }

    private sealed class Entry(TabItem tab)
    {
        public TabItem Tab { get; } = tab;
        public Control Content { get; } = tab.Content as Control
            ?? throw new InvalidOperationException("A detachable tab requires one live Control.");
        public ContentControl Host { get; } = new()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        public SukiWindow? Window;
        public Border? Handle;
        public TextBlock? Label;
        public bool Detached;
        public int Revision;
        public PixelPoint? Position;
        public double Width = 720;
        public double Height = 420;
    }

    private sealed class Drag(Entry entry, Control handle, IPointer pointer, PixelPoint start, PixelPoint? windowOrigin)
    {
        public Entry Entry { get; } = entry;
        public Control Handle { get; } = handle;
        public IPointer Pointer { get; } = pointer;
        public PixelPoint Start { get; } = start;
        public PixelPoint? WindowOrigin { get; } = windowOrigin;
        public bool Started;
        public bool OverDock;
    }
}
