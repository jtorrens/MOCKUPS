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
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record PreviewUtilityDockSite(
    string Id, string Label, Border Surface, TabControl Tabs, Grid Layout,
    int ContentRow, int SplitterRow, GridSplitter Splitter, Button DetachButton,
    double EmptyHeight, Grid? EmptyDropOverlay = null, bool AllowVerticalGroups = false);

// Owns visual placement only. Each tab retains its exact live content and the
// existing Preview/session owners continue to supply all authoring state.
internal sealed class PreviewControlsDockController : IDisposable, IEditorModalOcclusionParticipant
{
    private readonly Window _owner;
    private readonly Func<bool> _isDark;
    private readonly Entry[] _entries;
    private readonly List<DockSite> _sites;
    private double _textScale = 1;
    private bool _disposed;
    private bool _occluded;
    private Drag? _drag;
    private bool _restoringLayout;
    private bool _ensuringSelections;

    public PreviewControlsDockController(Window owner, Func<bool> isDark,
        params PreviewUtilityDockSite[] sites)
    {
        _owner = owner;
        _isDark = isDark;
        _sites = sites.Select(site => new DockSite(site)).ToList();
        _entries = _sites.SelectMany(site => site.Definition.Tabs.Items.Cast<TabItem>()
            .Select(tab => new Entry(tab, site))).ToArray();
        foreach (var site in _sites) RegisterSite(site);
        foreach (var entry in _entries)
        {
            entry.Tab.PropertyChanged += OnTabPropertyChanged;
            BindDragHandle(entry.Tab);
            ToolTip.SetTip(entry.Tab, "Drag to a group center to tab, a top/bottom edge to stack, or outside to float");
        }
        _owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, true);
        RefreshLayout();
    }

    public PreviewUtilityLayoutState CaptureLayout()
    {
        CancelDrag();
        foreach (var entry in _entries) CaptureGeometry(entry);
        return new PreviewUtilityLayoutState(
            _sites.Select(site => new PreviewUtilityGroupState(site.Id, site.StackRoot.Definition.Id,
                !site.Collapsed && site.Definition.Layout.RowDefinitions[site.Definition.ContentRow].ActualHeight > 0
                    ? site.Definition.Layout.RowDefinitions[site.Definition.ContentRow].ActualHeight : site.DockedHeight,
                site.PreferredTab?.Name ?? (site.Definition.Tabs.SelectedItem is TabItem selected ? selected.Name : null))).ToArray(),
            _entries.Select(entry => new PreviewUtilityTabState(entry.Tab.Name!, entry.Dock?.Id,
                entry.LastDock.Id, entry.Position?.X, entry.Position?.Y, entry.Width, entry.Height)).ToArray(),
            _sites.Where(site => site == site.StackRoot && site.Definition.AllowVerticalGroups)
                .ToDictionary(site => site.Definition.Id,
                    site => Math.Max(180, site.Definition.Layout.RowDefinitions[0].ActualHeight)));
    }

    public void RestoreLayout(PreviewUtilityLayoutState state)
    {
        var roots = _sites.Where(site => site == site.StackRoot).ToDictionary(site => site.Definition.Id);
        state.Validate(_entries.Select(entry => entry.Tab.Name!).ToArray(),
            roots.Keys.ToArray(), roots.Values.Where(site => site.Definition.AllowVerticalGroups)
                .Select(site => site.Definition.Id).ToArray());
        _restoringLayout = true;
        try
        {
            var groups = new Dictionary<string, DockSite>();
            foreach (var group in state.Groups)
            {
                var root = roots[group.RootId];
                var site = group.Id == group.RootId ? root : InsertGroup(root, 1);
                site.Id = group.Id;
                groups.Add(group.Id, site);
            }
            foreach (var saved in state.Tabs)
            {
                var entry = _entries.Single(entry => entry.Tab.Name == saved.Id);
                Move(entry, saved.GroupId is { } id ? groups[id] : null);
                entry.LastDock = groups[saved.ReturnGroupId];
                entry.Position = saved.X is { } x && saved.Y is { } y ? new PixelPoint(x, y) : null;
                entry.Width = saved.Width;
                entry.Height = saved.Height;
            }
            _sites.Clear();
            _sites.AddRange(state.Groups.Select(group => groups[group.Id]));
            foreach (var root in roots.Values.Where(site => site.Definition.AllowVerticalGroups))
            {
                RebuildStack(root);
                root.Definition.Layout.RowDefinitions[0].Height =
                    new GridLength(state.EditorHeights[root.Definition.Id], GridUnitType.Star);
            }
            foreach (var group in state.Groups)
            {
                var site = groups[group.Id];
                site.DockedHeight = group.Height;
                site.Initialized = false;
                site.Collapsed = !_entries.Any(entry => entry.Dock == site && entry.Tab.IsVisible);
                site.PreferredTab = _entries.SingleOrDefault(entry => entry.Tab.Name == group.SelectedTabId)?.Tab;
                site.Definition.Tabs.SelectedItem = site.PreferredTab;
            }
        }
        finally { _restoringLayout = false; }
        EnsureDockSelections();
        RefreshLayout();
        foreach (var entry in _entries.Where(entry => entry.Detached && entry.Tab.IsVisible)) Show(entry);
    }

    private void RegisterSite(DockSite site)
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
        if (_restoringLayout || !_sites.Any(site => ReferenceEquals(args.Source, site.Definition.Tabs))) return;
        if (!_ensuringSelections && sender is TabControl { SelectedItem: TabItem active })
            _sites.Single(site => site.Definition.Tabs == sender).PreferredTab = active;
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
        if (_restoringLayout) return;
        _ensuringSelections = true;
        try
        {
            foreach (var site in _sites)
            {
                var tabs = site.Definition.Tabs;
                if (site.PreferredTab is { IsVisible: true } preferred && EntryFor(preferred).Dock == site)
                    tabs.SelectedItem = preferred;
                else if (tabs.SelectedItem is not TabItem current || !current.IsVisible || EntryFor(current).Dock != site)
                    tabs.SelectedItem = _entries.FirstOrDefault(entry => entry.Dock == site && entry.Tab.IsVisible)?.Tab;
            }
        }
        finally { _ensuringSelections = false; }
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
        if (source?.PreferredTab == entry.Tab) source.PreferredTab = null;
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
            if (entry.Tab.IsVisible && !_restoringLayout)
            {
                Show(entry);
                entry.Window!.Activate();
            }
        }
        if (!_restoringLayout) PruneEmptyGroups();
        EnsureDockSelections();
        RefreshLayout();
        Transfer(entry, () =>
        {
            if (entry.Detached) entry.Host.Content = entry.Content;
            else entry.Tab.Content = entry.Content;
        }, scroll);
    }

    private sealed record DropTarget(DockSite Site, int Edge = 0);

    private DockSite InsertGroup(DockSite target, int edge)
    {
        var root = target.StackRoot;
        var template = root.Definition;
        var tabs = new TabControl
        {
            TabStripPlacement = Dock.Top, ItemsPanel = template.Tabs.ItemsPanel,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        var surface = new Border
        {
            Child = tabs, Background = template.Surface.Background,
            CornerRadius = template.Surface.CornerRadius, Margin = template.Surface.Margin,
        };
        var splitter = new GridSplitter
        {
            Height = 6, HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext,
        };
        var button = new Button
        {
            Width = 30, Height = 30, Padding = new Thickness(0), Margin = template.DetachButton.Margin,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
        };
        var site = new DockSite(template with
        {
            Surface = surface, Tabs = tabs, Splitter = splitter, DetachButton = button,
            EmptyDropOverlay = null,
        }) { Id = Guid.NewGuid().ToString("N"), StackRoot = root, Collapsed = true };
        var height = Math.Max(PreviewPanelLayoutPolicy.MinimumStackedUtilityHeight,
            target.Definition.Surface.Bounds.Height / 2);
        target.DockedHeight = height;
        site.DockedHeight = height;
        // Preserve the proportions chosen with dividers while inserting a new row pair.
        foreach (var existing in StackSites(root).Where(group => !group.Collapsed && group != target))
            existing.DockedHeight = existing.Definition.Layout.RowDefinitions[existing.Definition.ContentRow].ActualHeight;
        var editorRow = template.Layout.RowDefinitions[0];
        editorRow.Height = new GridLength(Math.Max(180, editorRow.ActualHeight), GridUnitType.Star);
        _sites.Insert(_sites.IndexOf(target) + (edge > 0 ? 1 : 0), site);
        RegisterSite(site);
        template.Layout.Children.Add(splitter);
        template.Layout.Children.Add(surface);
        template.Layout.Children.Add(button);
        RebuildStack(root);
        return site;
    }

    private IEnumerable<DockSite> StackSites(DockSite root) =>
        _sites.Where(site => site.StackRoot == root);

    private void RebuildStack(DockSite root)
    {
        var layout = root.Definition.Layout;
        while (layout.RowDefinitions.Count > 1) layout.RowDefinitions.RemoveAt(1);
        var rowIndex = 1;
        foreach (var site in StackSites(root).OrderBy(site => !_entries.Any(entry => entry.Dock == site && entry.Tab.IsVisible)))
        {
            layout.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Pixel));
            layout.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Pixel));
            site.Definition = site.Definition with { SplitterRow = rowIndex, ContentRow = rowIndex + 1 };
            Grid.SetRow(site.Definition.Splitter, rowIndex);
            Grid.SetRow(site.Definition.Surface, rowIndex + 1);
            Grid.SetRow(site.Definition.DetachButton, rowIndex + 1);
            site.Initialized = false;
            rowIndex += 2;
        }
        Grid.SetRowSpan(root.EmptyCue, layout.RowDefinitions.Count);
    }

    private void PruneEmptyGroups()
    {
        foreach (var site in _sites.Where(site => site != site.StackRoot
            && !_entries.Any(entry => entry.Dock == site || entry.LastDock == site)).ToArray())
        {
            var definition = site.Definition;
            definition.DetachButton.Click -= OnDetachClick;
            definition.Tabs.SelectionChanged -= OnSelectionChanged;
            definition.Layout.Children.Remove(definition.Surface);
            definition.Layout.Children.Remove(definition.Splitter);
            definition.Layout.Children.Remove(definition.DetachButton);
            _sites.Remove(site);
            RebuildStack(site.StackRoot);
        }
    }

    private void CommitDrop(Entry entry, DropTarget? target, PixelPoint point)
    {
        var destination = target?.Site;
        if (target is { Edge: not 0 })
        {
            if (entry.Dock == target.Site && target.Site.Definition.Tabs.Items.Count == 1) return;
            destination = InsertGroup(target.Site, target.Edge);
        }
        Move(entry, destination, point);
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
        ToolTip.SetTip(entry.Handle, "Drag to a group center to tab or a top/bottom edge to stack");
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
            if (args.CloseReason is WindowCloseReason.OwnerWindowClosing
                or WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
            {
                CaptureGeometry(entry);
                return;
            }
            args.Cancel = true;
            Place(entry, entry.LastDock);
        };
        entry.Window = window;
    }

    private void Show(Entry entry)
    {
        var window = entry.Window!;
        var position = entry.Position ?? new PixelPoint(_owner.Position.X + 48, _owner.Position.Y + 72);
        var screen = _owner.Screens.ScreenFromPoint(position) ?? _owner.Screens.ScreenFromWindow(_owner);
        if (screen is not null)
        {
            var bounds = screen.WorkingArea;
            entry.Width = Math.Max(window.MinWidth, Math.Min(entry.Width, bounds.Width / screen.Scaling));
            entry.Height = Math.Max(window.MinHeight, Math.Min(entry.Height, bounds.Height / screen.Scaling));
            position = new PixelPoint(
                Math.Clamp(position.X, bounds.X, Math.Max(bounds.X, bounds.Right - (int)Math.Ceiling(entry.Width * screen.Scaling))),
                Math.Clamp(position.Y, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - (int)Math.Ceiling(entry.Height * screen.Scaling))));
        }
        window.Width = entry.Width;
        window.Height = entry.Height;
        window.Position = position;
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
        if (_restoringLayout) return;
        foreach (var root in _sites.Where(site => site == site.StackRoot && site.Definition.AllowVerticalGroups))
        {
            var groups = StackSites(root).ToArray();
            if (!groups.Any(site => site.Collapsed == _entries.Any(entry => entry.Dock == site && entry.Tab.IsVisible))) continue;
            foreach (var site in groups.Where(site => site.Initialized && !site.Collapsed))
            {
                var height = site.Definition.Layout.RowDefinitions[site.Definition.ContentRow].ActualHeight;
                if (height > 0) site.DockedHeight = height;
            }
            RebuildStack(root);
        }
        foreach (var site in _sites)
        {
            var definition = site.Definition;
            var empty = !_entries.Any(entry => entry.Dock == site && entry.Tab.IsVisible);
            var row = definition.Layout.RowDefinitions[definition.ContentRow];
            if (empty && !site.Collapsed && row.ActualHeight > 0) site.DockedHeight = row.ActualHeight;
            var minimum = definition.AllowVerticalGroups
                ? PreviewPanelLayoutPolicy.MinimumStackedUtilityHeight
                : PreviewPanelLayoutPolicy.MinimumPreviewUtilityHeight;
            if (empty != site.Collapsed || !site.Initialized)
            {
                row.MinHeight = empty ? definition.EmptyHeight : minimum;
                row.Height = new GridLength(empty ? definition.EmptyHeight : site.DockedHeight,
                    !empty && definition.AllowVerticalGroups && StackSites(site.StackRoot).Count() > 1
                        ? GridUnitType.Star : GridUnitType.Pixel);
            }
            site.Initialized = true;
            site.Collapsed = empty;
            definition.Surface.MinHeight = empty ? definition.EmptyHeight : minimum;
            definition.Surface.IsVisible = !empty || definition.EmptyHeight > 0;
            definition.Layout.RowDefinitions[definition.SplitterRow].Height = new GridLength(empty ? 0 : 10);
            definition.Splitter.IsVisible = !empty;
            definition.Tabs.IsVisible = !empty;
            definition.DetachButton.IsVisible = !empty;
            definition.DetachButton.IsEnabled = definition.Tabs.SelectedItem is TabItem selected && selected.IsVisible;
            var description = definition.Tabs.SelectedItem is TabItem active ? $"Detach {active.Header}" : "Detach selected tab";
            EditorAccessibility.Describe(definition.DetachButton, description);
            ToolTip.SetTip(definition.DetachButton, description);
            var highlighted = _drag is { Started: true } drag && drag.Target?.Site == site
                && (drag.OriginDock != site || drag.Target.Edge != 0 && site.Definition.Tabs.Items.Count > 1);
            var edge = highlighted ? _drag!.Target!.Edge : 0;
            site.Cue.Height = edge == 0 ? double.NaN : 32;
            site.Cue.VerticalAlignment = edge < 0 ? VerticalAlignment.Top
                : edge > 0 ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
            ((TextBlock)site.Cue.Child!).Text = edge < 0 ? "Insert above" : edge > 0 ? "Insert below"
                : empty ? $"Drop here · {definition.Label}" : "Add as tab";
            site.Cue.IsVisible = empty && definition.EmptyHeight > 0 || !empty && highlighted;
            site.EmptyCue.IsVisible = empty && highlighted && definition.EmptyDropOverlay is not null;
            if (definition.EmptyDropOverlay is { } overlay)
                site.EmptyCue.Height = EmptyDropHeight(overlay);
        }
        RefreshTheme();
        foreach (var site in _sites)
            if (_drag is not { Started: true } drag || drag.Target?.Site != site || drag.OriginDock == site && drag.Target.Edge == 0)
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
        // Own the stream before a fast move can leave the header. The threshold
        // still distinguishes a click from a drag without consuming selection.
        args.Pointer.Capture(handle);
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

    private DropTarget? DockAt(PixelPoint point)
    {
        foreach (var site in _sites)
        {
            var definition = site.Definition;
            if (site.Collapsed && definition.EmptyDropOverlay is { IsEffectivelyVisible: true } overlay
                && !StackSites(site).Any(group => !group.Collapsed))
            {
                var height = EmptyDropHeight(overlay);
                var bounds = new Rect(0, overlay.Bounds.Height - height, overlay.Bounds.Width, height);
                if (bounds.Contains(overlay.PointToClient(point))) return new DropTarget(site);
            }
            else if (definition.Surface.IsEffectivelyVisible)
            {
                var local = definition.Surface.PointToClient(point);
                var bounds = new Rect(definition.Surface.Bounds.Size);
                if (definition.AllowVerticalGroups && !site.Collapsed)
                {
                    var band = Math.Min(48, bounds.Height / 4);
                    if (local.X >= 0 && local.X <= bounds.Width && local.Y >= -12 && local.Y <= bounds.Height + 12)
                        return new DropTarget(site, local.Y < band ? -1 : local.Y > bounds.Height - band ? 1 : 0);
                }
                else if (bounds.Contains(local)) return new DropTarget(site);
            }
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
            if (destination is { Edge: not 0 } || drag.Entry.Dock != destination?.Site)
                CommitDrop(drag.Entry, destination, point - drag.GrabOffset);
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
        if (drag.Pointer.Captured == drag.Handle) drag.Pointer.Capture(null);
        drag.Preview?.Close();
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs args)
    {
        // Capture loss bubbles from descendants while capture is being transferred
        // to the header. That transfer belongs to this drag, rather than cancelling it.
        if (_drag is { } drag && args.Pointer == drag.Pointer && args.Pointer.Captured != drag.Handle)
            CancelDrag();
    }

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
        public PreviewUtilityDockSite Definition { get; set; } = definition;
        public string Id { get; set; } = definition.Id;
        private DockSite? _stackRoot;
        public DockSite StackRoot { get => _stackRoot ?? this; set => _stackRoot = value; }
        public Border Cue { get; } = CreateCue(definition.Label);
        public Border EmptyCue { get; } = CreateCue(definition.Label);
        public double DockedHeight = PreviewPanelLayoutPolicy.MinimumPreviewUtilityHeight;
        public TabItem? PreferredTab;
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
        public DropTarget? Target;
    }
}
