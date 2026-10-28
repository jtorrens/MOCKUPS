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
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record PreviewUtilityDockSite(
    string Label, Border Surface, TabControl Tabs, Grid Layout,
    int ContentRow, int SplitterRow, GridSplitter Splitter, Button DetachButton,
    double EmptyHeight, Grid? EmptyDropOverlay = null);

// Owns visual placement only. Each tab retains its exact live content and the
// existing Preview/session owners continue to supply all authoring state.
internal sealed class PreviewControlsDockController : IDisposable, IEditorModalOcclusionParticipant
{
    private readonly Window _owner;
    private readonly Func<bool> _isDark;
    private readonly Entry[] _entries;
    private readonly DockSite[] _sites;
    private double _textScale = 1;
    private bool _disposed;
    private bool _occluded;
    private Drag? _drag;

    public PreviewControlsDockController(Window owner, Func<bool> isDark,
        params PreviewUtilityDockSite[] sites)
    {
        _owner = owner;
        _isDark = isDark;
        _sites = sites.Select(site => new DockSite(site)).ToArray();
        _entries = _sites.SelectMany(site => site.Definition.Tabs.Items.Cast<TabItem>()
            .Select(tab => new Entry(tab, site))).ToArray();
        foreach (var site in _sites)
        {
            var definition = site.Definition;
            definition.Surface.Child = null;
            definition.Surface.Child = new Grid { Children = { definition.Tabs, site.Cue } };
            if (definition.EmptyDropOverlay is { } overlay)
            {
                Grid.SetRowSpan(site.EmptyCue, overlay.RowDefinitions.Count);
                site.EmptyCue.VerticalAlignment = VerticalAlignment.Bottom;
                overlay.Children.Add(site.EmptyCue);
            }
            definition.DetachButton.Background = Brushes.Transparent;
            definition.DetachButton.BorderBrush = Brushes.Transparent;
            definition.DetachButton.BorderThickness = new Thickness(0);
            definition.DetachButton.Content = EditorIcons.Create(EditorIcons.Open, 15);
            definition.DetachButton.Click += OnDetachClick;
            definition.Tabs.SelectionChanged += OnSelectionChanged;
        }
        foreach (var entry in _entries)
        {
            entry.Tab.PropertyChanged += OnTabPropertyChanged;
            BindDragHandle(entry.Tab);
            entry.Tab.ContextMenu = CreatePlacementMenu(entry);
            ToolTip.SetTip(entry.Tab, "Drag to a tool panel or outside to float; right-click for placement");
        }
        _owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, true);
        RefreshLayout();
    }

    internal Window? FloatingWindow(TabItem tab) => EntryFor(tab).Window;
    internal bool IsDetached(TabItem tab) => EntryFor(tab).Detached;
    public event EventHandler<KeyEventArgs>? PreviewKeyDown;
    public event Action<TabItem>? SelectedTabChanged;

    public void SelectTab(TabItem tab, bool activate = false)
    {
        var entry = EntryFor(tab);
        if (entry.Detached)
        {
            if (activate && !_occluded && tab.IsVisible) entry.Window?.Activate();
            EnsureDockSelections();
        }
        else if (tab.IsVisible) entry.Dock!.Definition.Tabs.SelectedItem = tab;
        RefreshLayout();
    }

    public void RefreshTheme()
    {
        foreach (var entry in _entries)
            if (entry.Window is { } window)
                EditorSukiWindowTheme.ApplyNeutralBackground(window, _isDark());
        foreach (var site in _sites)
        {
            foreach (var cue in new[] { site.Cue, site.EmptyCue })
            {
                cue.BorderBrush = EditorUiVisuals.FocusBrush(_isDark());
                cue.Background = EditorUiVisuals.SelectedBackgroundBrush(_isDark());
                ((TextBlock)cue.Child!).Foreground = EditorUiVisuals.SelectedTextBrush(_isDark());
            }
        }
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
        foreach (var site in _sites)
        {
            site.Definition.DetachButton.Click -= OnDetachClick;
            site.Definition.Tabs.SelectionChanged -= OnSelectionChanged;
            site.Definition.EmptyDropOverlay?.Children.Remove(site.EmptyCue);
        }
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
        var site = _sites.Single(site => site.Definition.DetachButton == sender);
        if (site.Definition.Tabs.SelectedItem is TabItem tab) Place(EntryFor(tab), null);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_sites.Any(site => ReferenceEquals(args.Source, site.Definition.Tabs))) return;
        RefreshLayout();
        if (sender is TabControl { SelectedItem: TabItem selected }) SelectedTabChanged?.Invoke(selected);
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
        if (!tab.IsVisible && _drag?.Entry == entry) CancelDrag();
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
        EnsureDockSelections();
        RefreshLayout();
    }

    private void EnsureDockSelections()
    {
        foreach (var site in _sites)
        {
            var tabs = site.Definition.Tabs;
            if (tabs.SelectedItem is TabItem current && current.IsVisible && EntryFor(current).Dock == site) continue;
            tabs.SelectedItem = _entries.FirstOrDefault(entry => entry.Dock == site && entry.Tab.IsVisible)?.Tab;
        }
    }

    private void Place(Entry entry, DockSite? destination)
    {
        CancelDrag();
        Move(entry, destination);
    }

    // Every placement change uses one transfer, irrespective of source/destination.
    private void Move(Entry entry, DockSite? destination, PixelPoint? position = null)
    {
        if (_disposed || _occluded || entry.Dock == destination) return;
        entry.Tab.ContextMenu?.Close();
        entry.Handle?.ContextMenu?.Close();
        var scroll = CaptureScroll(entry.Content);
        var source = entry.Dock;
        entry.Revision++;
        entry.Tab.Content = null;
        entry.Host.Content = null;
        CaptureGeometry(entry);
        entry.Dock = destination;
        source?.Definition.Tabs.Items.Remove(entry.Tab);
        entry.Window?.Hide();
        if (destination is not null)
        {
            entry.LastDock = destination;
            var tabs = destination.Definition.Tabs;
            var index = _entries.TakeWhile(candidate => candidate != entry).Count(candidate => candidate.Dock == destination);
            tabs.Items.Insert(index, entry.Tab);
            if (entry.Tab.IsVisible) tabs.SelectedItem = entry.Tab;
        }
        else
        {
            EnsureWindow(entry);
            if (position is { } dropPosition) entry.Position = dropPosition;
            if (entry.Tab.IsVisible)
            {
                Show(entry);
                entry.Window!.Activate();
            }
        }
        EnsureDockSelections();
        RefreshLayout();
        Transfer(entry, () =>
        {
            if (entry.Detached) entry.Host.Content = entry.Content;
            else entry.Tab.Content = entry.Content;
        }, scroll);
    }

    private ContextMenu CreatePlacementMenu(Entry entry)
    {
        var menu = new ContextMenu();
        foreach (var site in _sites)
        {
            var item = new MenuItem { Header = $"Move to {site.Definition.Label}" };
            item.Click += (_, _) => Place(entry, site);
            menu.Opening += (_, _) => item.IsEnabled = entry.Dock != site;
            menu.Items.Add(item);
        }
        var detach = new MenuItem { Header = "Float tab" };
        detach.Click += (_, _) => Place(entry, null);
        menu.Opening += (_, _) => detach.IsEnabled = !entry.Detached;
        menu.Items.Add(detach);
        menu.Items.Add(new Separator());
        var reset = new MenuItem { Header = "Restore default tool layout" };
        reset.Click += (_, _) =>
        {
            CancelDrag();
            foreach (var candidate in _entries) Move(candidate, candidate.Home);
        };
        menu.Items.Add(reset);
        return menu;
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
        dock.Click += (_, _) => Place(entry, entry.LastDock);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { entry.Label, dock } };
        Grid.SetColumn(dock, 1);
        entry.Handle = new Border { Padding = new Thickness(12, 6), Background = Brushes.Transparent, Child = header };
        entry.Handle.ContextMenu = CreatePlacementMenu(entry);
        ToolTip.SetTip(entry.Handle, "Drag this header to a tool panel to dock; right-click for placement");
        BindDragHandle(entry.Handle);
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { entry.Handle, entry.Host } };
        Grid.SetRow(entry.Host, 1);
        var window = new SukiWindow
        {
            Title = entry.Tab.Header?.ToString(), MinWidth = 560, MinHeight = 280,
            CanResize = true, CanFullScreen = false, CanPin = false,
            IsTitleBarVisible = false, WindowDecorations = WindowDecorations.BorderOnly,
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
            Place(entry, entry.LastDock);
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
        foreach (var site in _sites)
        {
            var definition = site.Definition;
            var empty = !_entries.Any(entry => entry.Dock == site && entry.Tab.IsVisible);
            var row = definition.Layout.RowDefinitions[definition.ContentRow];
            if (empty && !site.Collapsed && row.ActualHeight > 0) site.DockedHeight = row.ActualHeight;
            if (empty != site.Collapsed || !site.Initialized)
            {
                row.MinHeight = empty ? definition.EmptyHeight : PreviewPanelLayoutPolicy.MinimumPreviewUtilityHeight;
                row.Height = new GridLength(empty ? definition.EmptyHeight : site.DockedHeight);
            }
            site.Initialized = true;
            site.Collapsed = empty;
            definition.Surface.MinHeight = empty ? definition.EmptyHeight : PreviewPanelLayoutPolicy.MinimumPreviewUtilityHeight;
            definition.Surface.IsVisible = !empty || definition.EmptyHeight > 0;
            definition.Layout.RowDefinitions[definition.SplitterRow].Height = new GridLength(empty ? 0 : 10);
            definition.Splitter.IsVisible = !empty;
            definition.Tabs.IsVisible = !empty;
            definition.DetachButton.IsVisible = !empty;
            definition.DetachButton.IsEnabled = definition.Tabs.SelectedItem is TabItem selected && selected.IsVisible;
            var description = definition.Tabs.SelectedItem is TabItem active ? $"Detach {active.Header}" : "Detach selected tab";
            EditorAccessibility.Describe(definition.DetachButton, description);
            ToolTip.SetTip(definition.DetachButton, description);
            var highlighted = _drag is { Started: true } drag && drag.Target == site && drag.OriginDock != site;
            site.Cue.IsVisible = empty && definition.EmptyHeight > 0 || !empty && highlighted;
            site.EmptyCue.IsVisible = empty && highlighted && definition.EmptyDropOverlay is not null;
            if (definition.EmptyDropOverlay is { } overlay)
                site.EmptyCue.Height = EmptyDropHeight(overlay);
        }
        RefreshTheme();
        foreach (var site in _sites)
            if (_drag is not { Started: true } drag || drag.Target != site || drag.OriginDock == site)
            {
                site.Cue.Background = Brushes.Transparent;
                site.Cue.BorderBrush = Brushes.Transparent;
            }
    }

    private static double EmptyDropHeight(Control overlay) => Math.Min(
        PreviewPanelLayoutPolicy.MinimumPreviewUtilityHeight, overlay.Bounds.Height / 2);

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
        CancelDrag();
        var entry = _entries.Single(candidate => candidate.Tab == handle || candidate.Handle == handle);
        var start = handle.PointToScreen(args.GetPosition(handle));
        var origin = entry.Detached ? entry.Window!.Position : handle.PointToScreen(default);
        _drag = new Drag(entry, handle, args.Pointer, start, start - origin,
            _sites.Select(site => (site, site.Definition.Tabs.SelectedItem)).ToArray());
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (_drag is not { } drag || sender != drag.Handle) return;
        if (!args.GetCurrentPoint(drag.Handle).Properties.IsLeftButtonPressed)
        {
            CancelDrag();
            return;
        }
        var point = drag.Handle.PointToScreen(args.GetPosition(drag.Handle));
        if (!drag.Started)
        {
            // Threshold is in logical units, independent of display scaling.
            var delta = args.GetPosition(drag.Handle) - drag.Handle.PointToClient(drag.Start);
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 8) return;
            drag.Started = true;
            drag.Entry.Tab.ContextMenu?.Close();
            drag.Entry.Handle?.ContextMenu?.Close();
            ToolTip.SetIsOpen(drag.Handle, false);
            args.Pointer.Capture(drag.Handle);
            if (_drag != drag) return;
            drag.Preview = CreateDragPreview(drag);
            drag.Preview.Position = point - drag.GrabOffset;
            drag.Preview.Show(_owner);
        }
        drag.Target = DockAt(point);
        drag.Preview!.Position = point - drag.GrabOffset;
        RefreshLayout();
        args.Handled = true;
    }

    private Window CreateDragPreview(Drag drag) => new()
    {
        Width = Math.Max(180, drag.Handle.Bounds.Width),
        Height = 40,
        WindowDecorations = WindowDecorations.None,
        CanResize = false, ShowActivated = false, ShowInTaskbar = false, Topmost = true,
        IsHitTestVisible = false,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Content = new Border
        {
            Background = EditorSukiWindowTheme.NeutralBackgroundBrush(_isDark()),
            BorderBrush = EditorUiVisuals.FocusBrush(_isDark()), BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6),
            Child = new TextBlock
            {
                Text = drag.Entry.Tab.Header?.ToString(),
                Foreground = EditorUiVisuals.SelectedTextBrush(_isDark()),
                VerticalAlignment = VerticalAlignment.Center,
            },
        },
    };

    private DockSite? DockAt(PixelPoint point)
    {
        foreach (var site in _sites)
        {
            var definition = site.Definition;
            if (site.Collapsed && definition.EmptyDropOverlay is { IsEffectivelyVisible: true } overlay)
            {
                var height = EmptyDropHeight(overlay);
                var bounds = new Rect(0, overlay.Bounds.Height - height, overlay.Bounds.Width, height);
                if (bounds.Contains(overlay.PointToClient(point))) return site;
            }
            else if (definition.Surface.IsEffectivelyVisible
                && new Rect(definition.Surface.Bounds.Size).Contains(definition.Surface.PointToClient(point))) return site;
        }
        return null;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_drag is not { } drag || sender != drag.Handle) return;
        var point = drag.Handle.PointToScreen(args.GetPosition(drag.Handle));
        var destination = DockAt(point);
        FinishDrag(drag);
        if (drag.Started)
        {
            if (drag.Entry.Dock != destination) Move(drag.Entry, destination, point - drag.GrabOffset);
            else if (drag.Entry.Detached)
            {
                drag.Entry.Window!.Position = point - drag.GrabOffset;
                CaptureGeometry(drag.Entry);
            }
            args.Handled = true;
        }
        RefreshLayout();
    }

    private void FinishDrag(Drag drag)
    {
        _drag = null;
        if (drag.Started) drag.Pointer.Capture(null);
        drag.Preview?.Close();
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs args) => CancelDrag();

    private void CancelDrag()
    {
        if (_drag is not { } drag) return;
        FinishDrag(drag);
        if (drag.Started)
            foreach (var (site, selection) in drag.Selections)
                site.Definition.Tabs.SelectedItem = selection;
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

    private sealed class DockSite(PreviewUtilityDockSite definition)
    {
        public PreviewUtilityDockSite Definition { get; } = definition;
        public Border Cue { get; } = CreateCue(definition.Label);
        public Border EmptyCue { get; } = CreateCue(definition.Label);
        public double DockedHeight = PreviewPanelLayoutPolicy.MinimumPreviewUtilityHeight;
        public bool Collapsed;
        public bool Initialized;

        private static Border CreateCue(string label) => new()
        {
            Child = new TextBlock
            {
                Text = $"Drop here · {label}",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(2),
            IsHitTestVisible = false, IsVisible = false,
        };
    }

    private sealed class Entry(TabItem tab, DockSite home)
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
        public DockSite Home { get; } = home;
        public DockSite LastDock = home;
        public DockSite? Dock = home;
        public bool Detached => Dock is null;
        public int Revision;
        public PixelPoint? Position;
        public double Width = 720;
        public double Height = 420;
    }

    private sealed class Drag(Entry entry, Control handle, IPointer pointer, PixelPoint start,
        PixelVector grabOffset, (DockSite Site, object? Selection)[] selections)
    {
        public Entry Entry { get; } = entry;
        public Control Handle { get; } = handle;
        public IPointer Pointer { get; } = pointer;
        public PixelPoint Start { get; } = start;
        public PixelVector GrabOffset { get; } = grabOffset;
        public DockSite? OriginDock { get; } = entry.Dock;
        public (DockSite Site, object? Selection)[] Selections { get; } = selections;
        public Window? Preview;
        public bool Started;
        public DockSite? Target;
    }
}
