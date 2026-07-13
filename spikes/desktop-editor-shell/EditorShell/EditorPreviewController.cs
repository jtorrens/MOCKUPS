using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorPreviewController
{
    private const int LoadingPreviewFrameThreshold = 0;
    private const int InitialPlaybackPreloadFrames = 32;
    private const int AheadPlaybackPreloadFrames = 16;
    private static readonly IBrush PreviewStatusIdleBrush = Brushes.Transparent;
    private static readonly IBrush PreviewStatusIdleBorder = new SolidColorBrush(Color.FromArgb(150, 210, 220, 232));
    private static readonly IBrush PreviewStatusLoadingBrush = new SolidColorBrush(Color.Parse("#2F80ED"));
    private static readonly IBrush PreviewStatusGoodBrush = new SolidColorBrush(Color.Parse("#2ECC71"));
    private static readonly IBrush PreviewStatusSlowBrush = new SolidColorBrush(Color.Parse("#E74C3C"));
    private readonly SpikeDatabase _database;
    private readonly Window _owner;
    private readonly EditorInstantComboBox _deviceComboBox;
    private readonly EditorInstantComboBox _themeComboBox;
    private readonly EditorInstantComboBox _modeComboBox;
    private readonly EditorInstantComboBox _orientationComboBox;
    private readonly EditorInstantComboBox _scaleComboBox = new()
    {
        MinWidth = 96,
        MinHeight = 36,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly EditorInstantComboBox _playbackRouteComboBox = new()
    {
        MinWidth = 190,
        MinHeight = 36,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly ToggleSwitch _marksToggle = new()
    {
        IsChecked = false,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly ToggleSwitch _canonicalFrameToggle = new()
    {
        IsChecked = false,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly Button _referenceButton = new()
    {
        Content = "Reference",
        MinHeight = 32,
    };
    private readonly EditorInstantComboBox _referenceViewComboBox = new()
    {
        MinWidth = 88,
        MinHeight = 32,
    };
    private readonly Slider _referenceSwipeSlider = new() { Minimum = 0, Maximum = 1, Value = 0.5, MinWidth = 72, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
    private readonly Slider _referenceOpacitySlider = new() { Minimum = 0, Maximum = 1, Value = 1, MinWidth = 72, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
    private readonly Slider _referenceAngleSlider = new() { Minimum = -45, Maximum = 45, Value = 0, MinWidth = 72, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
    private readonly StackPanel _referenceSplitControls = new() { Spacing = 8, IsVisible = false };
    private readonly StackPanel _shotTimelineControls = new()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal,
        Spacing = 7,
        IsVisible = false,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly StackPanel _shotHeaderTimelineControls = new()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal,
        Spacing = 8,
        IsVisible = false,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly Slider _shotFrameSlider = new()
    {
        Minimum = 0,
        Maximum = 0,
        Value = 0,
        TickFrequency = 1,
        MinWidth = 280,
        MaxWidth = 600,
    };
    private readonly EditorInstantComboBox _shotNavigationScopeComboBox = new()
    {
        MinWidth = 92,
        MinHeight = 30,
    };
    private readonly TextBlock _shotFrameText = new()
    {
        MinWidth = 70,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly Button _shotPreviousSlotButton = new() { Content = EditorIcons.Create(EditorIcons.TimelinePreviousInstance, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotPreviousKeyframeButton = new() { Content = EditorTimelineTransport.CreateKeyframeStepIcon(next: false), Width = 38, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotAbsoluteStartButton = new() { Content = EditorIcons.Create(EditorIcons.TimelineShotStart, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotPreviousFrameButton = new() { Content = EditorIcons.Create(EditorIcons.TimelinePreviousFrame, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotPlayButton = new() { Content = EditorIcons.Create(EditorIcons.Play, 16), Width = 42, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotNextFrameButton = new() { Content = EditorIcons.Create(EditorIcons.TimelineNextFrame, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotNextKeyframeButton = new() { Content = EditorTimelineTransport.CreateKeyframeStepIcon(next: true), Width = 38, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotNextSlotButton = new() { Content = EditorIcons.Create(EditorIcons.TimelineNextInstance, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotAbsoluteEndButton = new() { Content = EditorIcons.Create(EditorIcons.TimelineShotEnd, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly DispatcherTimer _shotPlaybackTimer = new() { Interval = TimeSpan.FromMilliseconds(20) };
    private readonly IEditorShellMessageSink _messages;
    private readonly Func<bool> _isDark;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly Func<EditorViewState?> _captureCurrentEditorViewState;
    private readonly Func<string, EditorViewState?, bool> _selectNodeById;
    private readonly TextBlock _designContextText;
    private readonly Button _designContextHistoryButton;
    private readonly Button _designContextAddHistoryButton;
    private readonly Button _designContextLockButton;
    private readonly Panel _previewTitle;
    private readonly Popup _designContextHistoryPopup;
    private readonly StackPanel _designContextHistoryItems = new() { Spacing = 1 };
    private readonly DesignWebPreviewPane _designPreviewPane = new();
    private readonly ComponentPreviewInputSession _designInputsPanel;
    private readonly ContentControl _previewBusyHost;
    private readonly StackPanel _productionContextHost = new()
    {
        Spacing = 7,
        Margin = new Thickness(0, 0, 0, 12),
        IsVisible = false,
    };
    private Grid? _previewSetupGrid;
    private Control? _orientationField;
    private readonly EditorLoadingScrim _previewLoadingScrim = new();
    private readonly ProductionPreviewRuntimeResolver _productionRuntimeResolver;
    private readonly ProductionShotContextService _productionShotContext;
    private readonly Border _previewPerformanceDot = new()
    {
        Width = 10,
        Height = 10,
        CornerRadius = new CornerRadius(5),
        Background = PreviewStatusIdleBrush,
        BorderBrush = PreviewStatusIdleBorder,
        BorderThickness = new Thickness(1),
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    public PreviewPlaybackState PlaybackState { get; } = new();
    private string? _projectId;
    private string? _selectedThemeId;
    public string? SelectedThemeId => _selectedThemeId;
    public event Action? ThemeChanged;

    public int ProductionScreenFrame(string moduleInstanceId)
    {
        var instance = _database.GetModuleInstanceSettings(moduleInstanceId);
        var start = ModuleInstanceStartFrame(instance.ShotId, moduleInstanceId);
        var duration = Math.Max(1, ModuleInstanceTimeline.DurationFrames(_database, moduleInstanceId));
        return Math.Clamp(_shotPreviewFrame - start, 0, duration - 1);
    }

    public void SetProductionScreenFrame(string moduleInstanceId, int localFrame)
    {
        var instance = _database.GetModuleInstanceSettings(moduleInstanceId);
        var start = ModuleInstanceStartFrame(instance.ShotId, moduleInstanceId);
        var duration = Math.Max(1, ModuleInstanceTimeline.DurationFrames(_database, moduleInstanceId));
        SetShotPreviewFrame(start + Math.Clamp(localFrame, 0, duration - 1), useSelectedScope: false);
    }

    public int ProductionShotFrame() => _shotPreviewFrame;

    public void SetProductionShotFrame(int frame) => SetShotPreviewFrame(frame, useSelectedScope: false);

    public void ToggleProductionPlayback() => ToggleShotPlayback();
    private PreviewNodeKey? _lastDesignPreviewNode;
    private PreviewNodeKey? _lastProductionPreviewNode;
    private PreviewNodeKey? _activeDesignPreviewNode;
    private PreviewNodeKey? _lockedDesignPreviewNode;
    private readonly List<DesignPreviewHistoryEntry> _designHistory = [];
    private readonly List<DesignPreviewHistoryEntry> _productionHistory = [];
    private EditorWorkspace _workspace = EditorWorkspace.Design;
    private string _selectedMode = "light";
    private string _selectedOrientation = "portrait";
    private string _selectedScale = "fit";
    private string _selectedPlaybackRoute = "html-all";
    private bool _showDesignMarks;
    private bool _showCanonicalFrame;
    private string _referenceSource = "";
    private string _referenceViewMode = "preview";
    private int _referenceStartPreviewFrame;
    private bool _isDesignPreviewContextLocked;
    private bool _isRefreshingOptions;
    private bool? _renderedLockState;
    private CancellationTokenSource? _previewLoadingCancellation;
    private CancellationTokenSource? _aheadPreloadCancellation;
    private readonly HashSet<string> _aheadPreloadedFrameKeys = new(StringComparer.Ordinal);
    private bool _isAheadPreloading;
    private readonly Dictionary<string, string> _rasterPlaybackFrames = new(StringComparer.Ordinal);
    private readonly List<string> _rasterPlaybackOrder = [];
    private string _rasterPlaybackSignature = "";
    private readonly ChromiumPreviewRasterizer _chromiumRasterizer = new();
    private string _rasterCacheDirectory = "";
    private PlaybackPerformanceRun? _playbackPerformanceRun;
    private IDisposable? _frameCacheReservation;
    private int _playbackSummaryGeneration;
    private int _shotPreviewFrame;
    private string _shotTimelineShotId = "";
    private string _shotTimelineContextNodeId = "";
    private string _shotNavigationScope = "shot";
    private bool _isUpdatingShotTimeline;
    private int? _pendingExplicitScreenFrame;
    private long _shotPlaybackStartedTimestamp;
    private int _shotPlaybackStartFrame;
    private bool _shotPlaybackIsPreparing;
    private IReadOnlyList<DesignPreviewPayload>? _pendingPlaybackFramesOverride;
    private string _activeProductionModuleInstanceId = "";

    public EditorPreviewController(
        SpikeDatabase database,
        EditorInstantComboBox deviceComboBox,
        EditorInstantComboBox themeComboBox,
        EditorInstantComboBox modeComboBox,
        EditorInstantComboBox orientationComboBox,
        IEditorShellMessageSink messages,
        ContentControl previewSetupHost,
        ContentControl previewControlsHost,
        ContentControl previewBusyHost,
        ContentControl designPreviewHost,
        TextBlock designContextText,
        Button designContextHistoryButton,
        Button designContextAddHistoryButton,
        Button designContextLockButton,
        Panel previewTitle,
        Func<bool> isDark,
        Func<ProjectTreeNode?> selectedNode,
        Func<EditorViewState?> captureCurrentEditorViewState,
        Func<string, EditorViewState?, bool> selectNodeById,
        Window owner)
    {
        _database = database;
        _owner = owner;
        _deviceComboBox = deviceComboBox;
        _themeComboBox = themeComboBox;
        _modeComboBox = modeComboBox;
        _orientationComboBox = orientationComboBox;
        _messages = messages;
        _isDark = isDark;
        _selectedNode = selectedNode;
        _captureCurrentEditorViewState = captureCurrentEditorViewState;
        _selectNodeById = selectNodeById;
        _designContextText = designContextText;
        _designContextHistoryButton = designContextHistoryButton;
        _designContextAddHistoryButton = designContextAddHistoryButton;
        _designContextLockButton = designContextLockButton;
        _previewTitle = previewTitle;
        _designContextHistoryPopup = CreateDesignContextHistoryPopup();
        _previewBusyHost = previewBusyHost;
        _productionRuntimeResolver = new ProductionPreviewRuntimeResolver(database);
        _productionShotContext = new ProductionShotContextService(database);
        _previewBusyHost.Content = _previewLoadingScrim;
        _previewBusyHost.IsVisible = false;
        _designInputsPanel = new ComponentPreviewInputSession(database, Refresh, PreparePlaybackFramesAsync);
        _designPreviewPane.FrameStatusChanged += OnDesignPreviewFrameStatusChanged;
        _designPreviewPane.ContextActionRequested += targetId =>
        {
            if (targetId == PreviewRetryTargetId) Refresh();
            else _selectNodeById(targetId, null);
        };
        _designInputsPanel.PlaybackStarted += OnPlaybackStarted;
        _designInputsPanel.PlaybackStopped += OnPlaybackStopped;
        _designInputsPanel.PlaybackBusyChanged += PlaybackState.SetBusy;
        _shotPlaybackTimer.Tick += (_, _) => AdvanceShotPlayback();

        _designContextHistoryButton.Content = EditorIcons.CreateSemantic("Recent design contexts", EditorIcons.Collapse, 15);
        _designContextAddHistoryButton.Content = EditorIcons.Create(EditorIcons.Add, 15);

        WrapPreviewSetup(previewSetupHost);
        CreatePreviewControls(previewControlsHost);
        designPreviewHost.Content = _designPreviewPane;
        AttachControlEvents();
        _designContextText.Cursor = new Cursor(StandardCursorType.Hand);
        _designContextText.PointerPressed += (_, _) => NavigateToActiveDesignContext();
        _designContextHistoryButton.Click += (_, _) => ToggleDesignContextHistory();
        _designContextAddHistoryButton.Click += (_, _) => AddCurrentDesignContextToHistory();
        _designContextLockButton.Click += (_, _) => ToggleDesignPreviewContextLock();
        AttachDesignContextHistoryPopup();
        UpdateDesignContextChrome(null);
    }

    private void AddCurrentDesignContextToHistory()
    {
        if (_workspace == EditorWorkspace.Production)
        {
            AddCurrentProductionContextToHistory();
            return;
        }
        var key = _activeDesignPreviewNode ?? _lockedDesignPreviewNode ?? _lastDesignPreviewNode;
        if (key is null)
        {
            return;
        }

        var payload = DesignPreviewPayloadFactory.Create(_database, key.ToNode(), _selectedThemeId, _selectedMode, _shotPreviewFrame);
        if (payload is null)
        {
            return;
        }

        var selected = _selectedNode();
        var viewState = selected is not null && PreviewNodeKey.From(selected).Equals(key)
            ? _captureCurrentEditorViewState()
            : null;
        AddDesignHistory(key, payload.Name, viewState);
        RefreshDesignContextHistoryChrome();
    }

    private void AddCurrentProductionContextToHistory()
    {
        var node = ProductionContextNode();
        if (node is null) return;
        var payload = DesignPreviewPayloadFactory.Create(
            _database,
            node,
            _selectedThemeId,
            _selectedMode,
            _shotPreviewFrame);
        if (payload is null) return;
        var key = PreviewNodeKey.From(node);
        var viewState = _selectedNode()?.Id == node.Id ? _captureCurrentEditorViewState() : null;
        AddHistory(_productionHistory, key, payload.Name, viewState);
        RefreshDesignContextHistoryChrome();
    }

    private Popup CreateDesignContextHistoryPopup()
    {
        return new Popup
        {
            PlacementTarget = _designContextHistoryButton,
            Placement = PlacementMode.Bottom,
            IsLightDismissEnabled = true,
            Child = new Border
            {
                MinWidth = 220,
                MaxWidth = 320,
                Padding = new Thickness(5),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.Parse(_isDark() ? "#24262B" : "#F7F7F8")),
                BorderBrush = new SolidColorBrush(Color.Parse(_isDark() ? "#46505E" : "#CDD3DC")),
                BorderThickness = new Thickness(1),
                Child = _designContextHistoryItems,
            },
        };
    }

    private void AttachDesignContextHistoryPopup()
    {
        if (_designContextHistoryButton.Parent is Panel parent
            && !parent.Children.Contains(_designContextHistoryPopup))
        {
            parent.Children.Add(_designContextHistoryPopup);
        }
    }

    private void ToggleDesignContextHistory()
    {
        if (_workspace == EditorWorkspace.Production)
        {
            if (_productionHistory.Count == 0) return;
            RenderProductionContextHistoryItems();
            _designContextHistoryPopup.IsOpen = !_designContextHistoryPopup.IsOpen;
            return;
        }
        if (_designHistory.Count == 0)
        {
            return;
        }

        RenderDesignContextHistoryItems();
        _designContextHistoryPopup.IsOpen = !_designContextHistoryPopup.IsOpen;
    }

    private void RenderProductionContextHistoryItems()
    {
        _designContextHistoryItems.Children.Clear();
        foreach (var entry in _productionHistory)
        {
            var subtitle = entry.Key.Kind == ProjectTreeNodeKind.Shot
                ? "Shot"
                : $"Screen · {_database.GetModuleInstanceSettings(entry.Key.Id).ShotId}";
            var button = new Button
            {
                Content = new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock { Text = entry.Name, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = subtitle, FontSize = 11, Opacity = 0.68 },
                    },
                },
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                MinHeight = 42,
                Padding = new Thickness(8, 5),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
            };
            button.Click += (_, _) =>
            {
                _designContextHistoryPopup.IsOpen = false;
                MoveHistoryToFront(_productionHistory, entry.Key.Id);
                _selectNodeById(entry.Key.Id, entry.ViewState);
                RefreshDesignContextHistoryChrome();
            };
            _designContextHistoryItems.Children.Add(button);
        }
    }

    private void RenderDesignContextHistoryItems()
    {
        _designContextHistoryItems.Children.Clear();
        foreach (var entry in _designHistory)
        {
            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = entry.Name,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                MinHeight = 30,
                Padding = new Thickness(8, 5),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            button.Click += (_, _) =>
            {
                _designContextHistoryPopup.IsOpen = false;
                MoveDesignHistoryToFront(entry.Key.Id);
                _selectNodeById(entry.Key.Id, entry.ViewState);
                RefreshDesignContextHistoryChrome();
            };
            _designContextHistoryItems.Children.Add(button);
        }
    }

    private void MoveDesignHistoryToFront(string nodeId)
    {
        var index = _designHistory.FindIndex((entry) => entry.Key.Id.Equals(nodeId, StringComparison.Ordinal));
        if (index <= 0)
        {
            return;
        }

        var entry = _designHistory[index];
        _designHistory.RemoveAt(index);
        _designHistory.Insert(0, entry);
    }

    private void AddDesignHistory(PreviewNodeKey key, string name, EditorViewState? viewState)
    {
        AddHistory(_designHistory, key, name, viewState);
    }

    private static void AddHistory(
        List<DesignPreviewHistoryEntry> history,
        PreviewNodeKey key,
        string name,
        EditorViewState? viewState)
    {
        history.RemoveAll((entry) => entry.Key.Id.Equals(key.Id, StringComparison.Ordinal));
        history.Insert(0, new DesignPreviewHistoryEntry(key, name, viewState));
        if (history.Count > 10) history.RemoveRange(10, history.Count - 10);
    }

    private static void MoveHistoryToFront(List<DesignPreviewHistoryEntry> history, string nodeId)
    {
        var index = history.FindIndex((entry) => entry.Key.Id.Equals(nodeId, StringComparison.Ordinal));
        if (index <= 0) return;
        var entry = history[index];
        history.RemoveAt(index);
        history.Insert(0, entry);
    }

    public IReadOnlyList<EditorDesignPreviewHistoryEntryState> ExportDesignHistoryState()
    {
        return _designHistory.Select((entry) => new EditorDesignPreviewHistoryEntryState
        {
            Kind = entry.Key.Kind,
            Id = entry.Key.Id,
            Name = entry.Name,
            ViewState = entry.ViewState is null ? null : EditorViewStateSnapshot.From(entry.ViewState),
        }).ToList();
    }

    public void RestoreDesignHistoryState(IReadOnlyList<EditorDesignPreviewHistoryEntryState>? entries)
    {
        _designHistory.Clear();
        if (entries is null)
        {
            RefreshDesignContextHistoryChrome();
            return;
        }

        foreach (var entry in entries
                     .Where((entry) => !string.IsNullOrWhiteSpace(entry.Id))
                     .Take(10))
        {
            var key = new PreviewNodeKey(entry.Kind, entry.Id);
            _designHistory.Add(new DesignPreviewHistoryEntry(
                key,
                string.IsNullOrWhiteSpace(entry.Name) ? entry.Id : entry.Name,
                entry.ViewState?.ToViewState()));
        }
        RefreshDesignContextHistoryChrome();
    }

    public IReadOnlyList<EditorDesignPreviewHistoryEntryState> ExportProductionHistoryState() =>
        _productionHistory.Select((entry) => new EditorDesignPreviewHistoryEntryState
        {
            Kind = entry.Key.Kind,
            Id = entry.Key.Id,
            Name = entry.Name,
            ViewState = entry.ViewState is null ? null : EditorViewStateSnapshot.From(entry.ViewState),
        }).ToList();

    public void RestoreProductionHistoryState(IReadOnlyList<EditorDesignPreviewHistoryEntryState>? entries)
    {
        _productionHistory.Clear();
        foreach (var entry in entries ?? [])
        {
            if (entry.Kind is not ProjectTreeNodeKind.Shot and not ProjectTreeNodeKind.ModuleInstance
                || string.IsNullOrWhiteSpace(entry.Id)) continue;
            _productionHistory.Add(new DesignPreviewHistoryEntry(
                new PreviewNodeKey(entry.Kind, entry.Id),
                string.IsNullOrWhiteSpace(entry.Name) ? entry.Id : entry.Name,
                entry.ViewState?.ToViewState()));
            if (_productionHistory.Count == 10) break;
        }
        RefreshDesignContextHistoryChrome();
    }

    public void SetWorkspace(EditorWorkspace workspace)
    {
        SetWorkspace(workspace, refresh: true);
    }

    public void SetWorkspaceWithoutRefresh(EditorWorkspace workspace)
    {
        SetWorkspace(workspace, refresh: false);
    }

    private void SetWorkspace(EditorWorkspace workspace, bool refresh)
    {
        if (_workspace == workspace) return;
        _workspace = workspace;
        _designContextHistoryPopup.IsOpen = false;
        StopShotPlayback();
        if (refresh) Refresh();
    }

    public string NativeHostLifecycleState() => _designPreviewPane.NativeHostLifecycleState();

    private void RefreshDesignContextHistoryChrome()
    {
        var history = _workspace == EditorWorkspace.Production ? _productionHistory : _designHistory;
        _designContextHistoryButton.Content = EditorIcons.CreateSemantic(
            _workspace == EditorWorkspace.Production ? "Recent production contexts" : "Recent design contexts",
            EditorIcons.Collapse,
            15);
        _designContextHistoryButton.IsEnabled = history.Count > 0;
        _designContextHistoryButton.Opacity = history.Count > 0 ? 1 : 0.38;
        var canAddCurrentContext = _activeDesignPreviewNode is not null
            || _lockedDesignPreviewNode is not null
            || _lastDesignPreviewNode is not null
            || (_workspace == EditorWorkspace.Production && ProductionContextNode() is not null);
        _designContextAddHistoryButton.IsEnabled = canAddCurrentContext;
        _designContextAddHistoryButton.Opacity = canAddCurrentContext ? 1 : 0.38;
        ToolTip.SetTip(
            _designContextHistoryButton,
            history.Count > 0
                ? _workspace == EditorWorkspace.Production ? "Recent production contexts" : "Recent design contexts"
                : _workspace == EditorWorkspace.Production ? "No recent production contexts" : "No recent design contexts");
        ToolTip.SetTip(
            _designContextAddHistoryButton,
            canAddCurrentContext ? "Add current design context to history" : "No design context to add");
    }

    private void WrapPreviewSetup(ContentControl previewSetupHost)
    {
        if (previewSetupHost.Content is not Control setupContent)
        {
            return;
        }

        previewSetupHost.Content = null;
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        header.Children.Add(EditorCardHeader.Create(
            "Preview setup",
            "Context and component inputs",
            EditorIcons.CreateSemantic("Preview setup", EditorIcons.Design, 18)));

        var actions = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        ToolTip.SetTip(_previewPerformanceDot, "Preview FPS and rendering status");
        actions.Children.Add(_previewPerformanceDot);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);

        previewSetupHost.Content = new GlassCard
        {
            Content = new InstantEditorCard(
                header,
                new Border
                {
                    Padding = new Thickness(12, 0, 12, 12),
                    Child = new StackPanel
                    {
                        Spacing = 0,
                        Children =
                        {
                            _productionContextHost,
                            setupContent,
                        },
                    },
                },
                isExpanded: true),
        };
        _previewSetupGrid = _deviceComboBox.Parent?.Parent as Grid;
        _orientationField = _orientationComboBox.Parent as Control;
    }

    private void CreatePreviewControls(ContentControl previewControlsHost)
    {
        ToolTip.SetTip(_marksToggle, "Show design markers");
        ToolTip.SetTip(_canonicalFrameToggle, "Show canonical 360 × 800 frame without the device layer");

        var primaryControls = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto"),
            ColumnSpacing = 10,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        var primaryControlItems = new Control[]
        {
            _scaleComboBox,
            _playbackRouteComboBox,
            LabeledToggle("Markers", _marksToggle),
            LabeledToggle("360", _canonicalFrameToggle),
            _referenceViewComboBox,
        };
        for (var index = 0; index < primaryControlItems.Length; index++)
        {
            Grid.SetColumn(primaryControlItems[index], index);
            primaryControls.Children.Add(primaryControlItems[index]);
        }

        var splitGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,*,*"),
            ColumnSpacing = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        splitGrid.Children.Add(_referenceButton);
        AddReferenceSlider(splitGrid, 1, "Swipe", _referenceSwipeSlider);
        AddReferenceSlider(splitGrid, 2, "Opacity", _referenceOpacitySlider);
        AddReferenceSlider(splitGrid, 3, "Angle", _referenceAngleSlider);
        _referenceSplitControls.Children.Add(splitGrid);
        _shotNavigationScopeComboBox.ItemsSource = new[]
        {
            new FieldOption("shot", "Shot"),
            new FieldOption("screen", "Screen"),
        };
        _shotNavigationScopeComboBox.SelectedItem = ((IEnumerable<FieldOption>)_shotNavigationScopeComboBox.ItemsSource)
            .First((option) => option.Value == _shotNavigationScope);
        EditorAccessibility.Describe(
            _shotNavigationScopeComboBox,
            "Preview navigation scope",
            "Choose whether the frame slider navigates the current Shot or Screen");
        EditorAccessibility.Describe(
            _shotFrameSlider,
            "Navigate preview frames",
            "Navigate frames in the selected Shot or Screen scope");
        _shotHeaderTimelineControls.Children.Add(_shotFrameSlider);
        _shotHeaderTimelineControls.Children.Add(_shotFrameText);
        var navigationRow = new Border
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Child = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 7,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Children =
                {
                    _shotNavigationScopeComboBox,
                    TimelineSeparator(),
                    TimelineButtonGroup(
                        _shotAbsoluteStartButton,
                        _shotPreviousSlotButton,
                        _shotPreviousKeyframeButton,
                        _shotPreviousFrameButton,
                        _shotPlayButton,
                        _shotNextFrameButton,
                        _shotNextKeyframeButton,
                        _shotNextSlotButton,
                        _shotAbsoluteEndButton),
                },
            },
        };
        foreach (var button in new[]
        {
            _shotAbsoluteStartButton,
            _shotPreviousSlotButton,
            _shotPreviousKeyframeButton,
            _shotPreviousFrameButton,
            _shotNextFrameButton,
            _shotNextKeyframeButton,
            _shotNextSlotButton,
            _shotAbsoluteEndButton,
        })
        {
            button.Background = Brushes.Transparent;
            button.BorderBrush = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
        }
        _shotPlayButton.Background = EditorSukiWindowTheme.AccentBrush();
        _shotPlayButton.Foreground = Brushes.White;
        _shotPlayButton.BorderBrush = Brushes.Transparent;
        _shotPlayButton.BorderThickness = new Thickness(0);
        var transportLeadingSeparator = TimelineSeparator(30);
        _shotTimelineControls.Children.Add(transportLeadingSeparator);
        _shotTimelineControls.Children.Add(navigationRow);
        EditorAccessibility.Describe(_shotPreviousKeyframeButton, "Previous animation keyframe in the current Screen");
        EditorAccessibility.Describe(_shotPreviousSlotButton, "Previous Screen");
        EditorAccessibility.Describe(_shotAbsoluteStartButton, "First Shot frame");
        EditorAccessibility.Describe(_shotPreviousFrameButton, "Previous frame");
        EditorAccessibility.Describe(_shotPlayButton, "Play or pause the selected scope");
        EditorAccessibility.Describe(_shotNextFrameButton, "Next frame");
        EditorAccessibility.Describe(_shotNextKeyframeButton, "Next animation keyframe in the current Screen");
        EditorAccessibility.Describe(_shotNextSlotButton, "Next Screen");
        EditorAccessibility.Describe(_shotAbsoluteEndButton, "Last Shot frame");
        _shotAbsoluteStartButton.Click += (_, _) => SetShotPreviewFrame(0, useSelectedScope: false, synchronizeScreenSelection: true);
        _shotPreviousSlotButton.Click += (_, _) => MoveShotSlot(-1);
        _shotPreviousKeyframeButton.Click += (_, _) => MoveAnimationKeyframe(-1);
        _shotPreviousFrameButton.Click += (_, _) => SetShotPreviewFrame(_shotPreviewFrame - 1, synchronizeScreenSelection: true);
        _shotPlayButton.Click += (_, _) => ToggleShotPlayback();
        _shotNextFrameButton.Click += (_, _) => SetShotPreviewFrame(_shotPreviewFrame + 1, synchronizeScreenSelection: true);
        _shotNextKeyframeButton.Click += (_, _) => MoveAnimationKeyframe(1);
        _shotNextSlotButton.Click += (_, _) => MoveShotSlot(1);
        _shotAbsoluteEndButton.Click += (_, _) => SetShotPreviewFrame(ShotLastFrame(), useSelectedScope: false, synchronizeScreenSelection: true);

        var controlsRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 8,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        controlsRow.Children.Add(primaryControls);
        var transportHost = new Border
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Child = _shotTimelineControls,
        };
        Grid.SetColumn(transportHost, 2);
        controlsRow.Children.Add(transportHost);
        bool? transportWraps = null;
        void ArrangeTransport(double availableWidth)
        {
            var primaryNaturalWidth = primaryControlItems.Sum((control) => control.DesiredSize.Width)
                + (primaryControls.ColumnSpacing * (primaryControlItems.Length - 1));
            var requiredWidth = primaryNaturalWidth
                + _shotTimelineControls.DesiredSize.Width
                + (controlsRow.ColumnSpacing * 2);
            var wraps = availableWidth > 0 && availableWidth + 1 < requiredWidth;
            if (transportWraps == wraps) return;
            transportWraps = wraps;
            _scaleComboBox.MinWidth = wraps ? 0 : 96;
            _playbackRouteComboBox.MinWidth = wraps ? 0 : 190;
            _referenceViewComboBox.MinWidth = wraps ? 0 : 88;
            primaryControls.ColumnDefinitions = wraps
                ? new ColumnDefinitions("*,2*,Auto,Auto,*")
                : new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto");
            Grid.SetColumnSpan(primaryControls, wraps ? 3 : 1);
            primaryControls.HorizontalAlignment = wraps
                ? Avalonia.Layout.HorizontalAlignment.Stretch
                : Avalonia.Layout.HorizontalAlignment.Left;
            transportLeadingSeparator.Opacity = wraps ? 0 : 1;
            transportHost.BorderBrush = EditorUiVisuals.ScrollbarSeparatorBrush(_isDark());
            transportHost.BorderThickness = wraps ? new Thickness(0, 1, 0, 0) : new Thickness(0);
            transportHost.Padding = wraps ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            Grid.SetRow(transportHost, wraps ? 1 : 0);
            Grid.SetColumn(transportHost, wraps ? 0 : 2);
            Grid.SetColumnSpan(transportHost, wraps ? 3 : 1);
            transportHost.HorizontalAlignment = wraps
                ? Avalonia.Layout.HorizontalAlignment.Stretch
                : Avalonia.Layout.HorizontalAlignment.Right;
            _shotTimelineControls.HorizontalAlignment = wraps
                ? Avalonia.Layout.HorizontalAlignment.Center
                : Avalonia.Layout.HorizontalAlignment.Right;
        }
        controlsRow.SizeChanged += (_, args) => ArrangeTransport(args.NewSize.Width);
        controlsRow.LayoutUpdated += (_, _) => ArrangeTransport(controlsRow.Bounds.Width);
        ArrangeTransport(controlsRow.Bounds.Width);

        previewControlsHost.Content = new GlassCard
        {
            Content = new InstantEditorCard(
                EditorCardHeader.Create(
                    "Preview controls",
                    "Display and reference",
                    EditorIcons.CreateSemantic("Preview controls", EditorIcons.Design, 18)),
                new Border
                {
                    Padding = new Thickness(12, 0, 12, 12),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            controlsRow,
                            _referenceSplitControls,
                        },
                    },
                },
                isExpanded: true,
                headerTrailing: _shotHeaderTimelineControls),
        };
        UpdateReferenceControlsVisibility();
    }

    private static Control LabeledToggle(string label, ToggleSwitch toggle)
    {
        return new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = label, FontSize = 11, Opacity = 0.72, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                toggle,
            },
        };
    }

    private static Control TimelineButtonGroup(params Button[] buttons)
    {
        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        foreach (var button in buttons) panel.Children.Add(button);
        return panel;
    }

    private Control TimelineSeparator(double height = 22) => new Border
    {
        Width = 1,
        Height = height,
        Margin = new Thickness(2, 0),
        Background = EditorUiVisuals.ScrollbarSeparatorBrush(_isDark()),
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };

    private static void AddReferenceSlider(Grid host, int column, string label, Slider slider)
    {
        var control = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Children =
            {
                new TextBlock { Text = label, FontSize = 10, Opacity = 0.72 },
                slider,
            },
        };
        Grid.SetColumn(control, column);
        host.Children.Add(control);
    }

    public string? SelectedDeviceId { get; private set; }

    public void Initialize(IReadOnlyList<ProjectTreeNode> treeRoots)
    {
        RefreshOptions(treeRoots);
    }

    public void RefreshOptions(IReadOnlyList<ProjectTreeNode> treeRoots)
    {
        var project = treeRoots.FirstOrDefault((node) => node.Kind == ProjectTreeNodeKind.Project);
        if (project is null) return;

        _projectId = project.Id;
        _isRefreshingOptions = true;
        try
        {
            var deviceOptions = _database.GetDeviceOptions(project.Id);
            _deviceComboBox.ItemsSource = deviceOptions;
            var selectedDevice = !string.IsNullOrWhiteSpace(SelectedDeviceId)
                ? deviceOptions.FirstOrDefault((option) => option.Value == SelectedDeviceId)
                : null;
            selectedDevice ??= deviceOptions.FirstOrDefault();
            _deviceComboBox.SelectedItem = selectedDevice;
            SelectedDeviceId = selectedDevice?.Value;

            var themeOptions = _database.GetThemeOptions(project.Id);
            _themeComboBox.ItemsSource = themeOptions;
            var selectedTheme = !string.IsNullOrWhiteSpace(_selectedThemeId)
                ? themeOptions.FirstOrDefault((option) => option.Value == _selectedThemeId)
                : null;
            selectedTheme ??= themeOptions.FirstOrDefault();
            _themeComboBox.SelectedItem = selectedTheme;
            _selectedThemeId = selectedTheme?.Value;

            var modeOptions = new[]
            {
                new FieldOption("light", "Light"),
                new FieldOption("dark", "Dark"),
            };
            _modeComboBox.ItemsSource = modeOptions;
            _modeComboBox.SelectedItem = modeOptions.FirstOrDefault((option) => option.Value == _selectedMode) ?? modeOptions[0];
            _selectedMode = _modeComboBox.SelectedItem?.Value ?? "light";

            var orientationOptions = new[]
            {
                new FieldOption("portrait", "Portrait"),
                new FieldOption("landscape", "Landscape"),
            };
            _orientationComboBox.ItemsSource = orientationOptions;
            _orientationComboBox.SelectedItem = orientationOptions.FirstOrDefault((option) => option.Value == _selectedOrientation) ?? orientationOptions[0];
            _selectedOrientation = _orientationComboBox.SelectedItem?.Value ?? "portrait";

            var scaleOptions = new[]
            {
                new FieldOption("fit", "Fit"),
                new FieldOption("actual", "1:1"),
                new FieldOption("2x", "2:1"),
                new FieldOption("3x", "3:1"),
                new FieldOption("4x", "4:1"),
            };
            _scaleComboBox.ItemsSource = scaleOptions;
            _scaleComboBox.SelectedItem = scaleOptions.FirstOrDefault((option) => option.Value == _selectedScale) ?? scaleOptions[0];
            _selectedScale = _scaleComboBox.SelectedItem?.Value ?? "fit";
            var playbackRouteOptions = new[]
            {
                new FieldOption("html-fps", "HTML · Priority FPS"),
                new FieldOption("html-all", "HTML · Every frame"),
                new FieldOption("raster", "Raster · Every frame"),
            };
            _playbackRouteComboBox.ItemsSource = playbackRouteOptions;
            _playbackRouteComboBox.SelectedItem = playbackRouteOptions.FirstOrDefault((option) => option.Value == _selectedPlaybackRoute) ?? playbackRouteOptions[2];
            _selectedPlaybackRoute = _playbackRouteComboBox.SelectedItem?.Value ?? "raster";
            _designInputsPanel.PresentEveryPlaybackFrame = _selectedPlaybackRoute == "html-all";
            _marksToggle.IsChecked = _showDesignMarks;
            _canonicalFrameToggle.IsChecked = _showCanonicalFrame;
            var referenceViewOptions = new[]
            {
                new FieldOption("preview", "Preview"),
                new FieldOption("split", "Split"),
            };
            _referenceViewComboBox.ItemsSource = referenceViewOptions;
            _referenceViewComboBox.SelectedItem = referenceViewOptions.FirstOrDefault((option) => option.Value == _referenceViewMode) ?? referenceViewOptions[0];
            _referenceViewMode = _referenceViewComboBox.SelectedItem?.Value ?? "preview";
            UpdateReferenceControlsVisibility();
        }
        finally
        {
            _isRefreshingOptions = false;
        }

        Refresh();
    }

    private void AttachControlEvents()
    {
        var shotFrameMagnet = new TimelineSliderMagnet(
            _shotFrameSlider,
            () =>
            {
                var range = NavigationFrameRange();
                return AnimationKeyframesInCurrentScreen()
                    .Select((frame) => frame - range.StartFrame)
                    .Where((frame) => frame >= 0 && frame < range.DurationFrames)
                    .ToList();
            });
        _deviceComboBox.SelectionChanged += (_, _) => OnDeviceChanged();
        _themeComboBox.SelectionChanged += (_, _) => OnThemeChanged();
        _modeComboBox.SelectionChanged += (_, _) => OnModeChanged();
        _orientationComboBox.SelectionChanged += (_, _) => OnOrientationChanged();
        _scaleComboBox.SelectionChanged += (_, _) => OnScaleChanged();
        _playbackRouteComboBox.SelectionChanged += (_, _) => OnPlaybackRouteChanged();
        _referenceButton.Click += async (_, _) => await BrowseReferenceAsync();
        _referenceViewComboBox.SelectionChanged += (_, _) => OnReferenceViewChanged();
        _referenceSwipeSlider.PropertyChanged += (_, change) => { if (change.Property == RangeBase.ValueProperty) RefreshReferenceOverlay(); };
        _referenceOpacitySlider.PropertyChanged += (_, change) => { if (change.Property == RangeBase.ValueProperty) RefreshReferenceOverlay(); };
        _referenceAngleSlider.PropertyChanged += (_, change) => { if (change.Property == RangeBase.ValueProperty) RefreshReferenceOverlay(); };
        _shotFrameSlider.PropertyChanged += (_, change) =>
        {
            if (change.Property == RangeBase.ValueProperty && !_isUpdatingShotTimeline)
            {
                var range = NavigationFrameRange();
                SetShotPreviewFrame(range.StartFrame + (int)Math.Round(
                    shotFrameMagnet.Resolve(_shotFrameSlider.Value),
                    MidpointRounding.AwayFromZero));
            }
        };
        _shotFrameSlider.PointerReleased += (_, _) => SynchronizeExplicitScreenSelection();
        _shotFrameSlider.KeyUp += (_, _) => SynchronizeExplicitScreenSelection();
        _shotNavigationScopeComboBox.SelectionChanged += (_, _) =>
        {
            if (_shotNavigationScopeComboBox.SelectedItem is not { } option || _isUpdatingShotTimeline) return;
            SetShotNavigationScope(option.Value);
            StopShotPlayback();
            Refresh();
        };
        _marksToggle.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleSwitch.IsCheckedProperty)
            {
                OnMarksChanged();
            }
        };
        _canonicalFrameToggle.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleSwitch.IsCheckedProperty)
            {
                OnCanonicalFrameChanged();
            }
        };
    }

    private void OnDeviceChanged()
    {
        if (_deviceComboBox.SelectedItem is not { } option) return;

        SelectedDeviceId = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    private void OnThemeChanged()
    {
        if (_themeComboBox.SelectedItem is not { } option) return;

        _selectedThemeId = option.Value;
        ThemeChanged?.Invoke();
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    private void OnModeChanged()
    {
        if (_modeComboBox.SelectedItem is not { } option) return;

        _selectedMode = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    private void OnOrientationChanged()
    {
        if (_orientationComboBox.SelectedItem is not { } option) return;

        _selectedOrientation = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    private void OnScaleChanged()
    {
        if (_scaleComboBox.SelectedItem is not { } option) return;

        _selectedScale = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    private void OnPlaybackRouteChanged()
    {
        if (_playbackRouteComboBox.SelectedItem is not { } option) return;
        _selectedPlaybackRoute = option.Value;
        _designInputsPanel.PresentEveryPlaybackFrame = _selectedPlaybackRoute == "html-all";
        if (!_isRefreshingOptions) Refresh();
    }

    private void OnMarksChanged()
    {
        _showDesignMarks = _marksToggle.IsChecked == true;
        if (!_isRefreshingOptions)
        {
            _ = _designPreviewPane.SetDesignMarksAsync(_showDesignMarks);
            Refresh();
        }
    }

    private void OnCanonicalFrameChanged()
    {
        _showCanonicalFrame = _canonicalFrameToggle.IsChecked == true;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    private async Task BrowseReferenceAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectId)) return;
        var mediaRoot = _database.GetProjectSettings(_projectId).MediaRoot;
        var selected = await EditorPathBrowser.BrowseMediaFile(_owner.StorageProvider, _referenceSource, mediaRoot);
        if (string.IsNullOrWhiteSpace(selected)) return;

        _referenceSource = selected;
        _referenceStartPreviewFrame = CurrentNavigationFrame();
        _referenceViewMode = "split";
        _referenceViewComboBox.SelectedItem = (_referenceViewComboBox.ItemsSource as IEnumerable<FieldOption>)?.FirstOrDefault((option) => option.Value == "split");
        UpdateReferenceControlsVisibility();
        ToolTip.SetTip(_referenceButton, _referenceSource);
        RefreshReferenceOverlay();
    }

    private void OnReferenceViewChanged()
    {
        if (_referenceViewComboBox.SelectedItem is not { } option) return;
        _referenceViewMode = option.Value;
        UpdateReferenceControlsVisibility();
        if (!_isRefreshingOptions) RefreshReferenceOverlay();
    }

    private void UpdateReferenceControlsVisibility()
    {
        _referenceSplitControls.IsVisible = _referenceViewMode == "split";
    }

    private void RefreshReferenceOverlay()
    {
        if (_isRefreshingOptions) return;
        _ = _designPreviewPane.UpdateReferenceOverlayAsync(CurrentReferenceState());
    }

    private PreviewReferenceState CurrentReferenceState() => new(
        _referenceSource,
        _referenceViewMode,
        _referenceSwipeSlider.Value,
        _referenceOpacitySlider.Value,
        _referenceAngleSlider.Value,
        Math.Max(0, CurrentNavigationFrame() - _referenceStartPreviewFrame),
        _designInputsPanel.PlaybackFrameRate,
        string.IsNullOrWhiteSpace(_projectId) ? "" : _database.GetProjectSettings(_projectId).MediaRoot);

    public void Refresh()
    {
        try
        {
            // Static preview changes (including reference images and design marks)
            // must never inherit a playback preparation overlay.
            if (!IsPreviewPlaybackActive && !_shotPlaybackIsPreparing)
            {
                HidePreviewLoading();
            }
            EnsureSelectedOptionsExist();
            UpdateShotTimelineControls();
            UpdateProductionPreviewSetup();
            var invalidProductionContext = InvalidProductionContext();
            var designPayload = invalidProductionContext is null ? DesignPreviewPayloadForSelection() : null;
            var contextState = invalidProductionContext
                ?? (designPayload is null ? NonRenderableStateForSelection() : PreviewContextState.Renderable);
            if (invalidProductionContext is not null)
            {
                _messages.Error("Production context", invalidProductionContext.Message);
            }
            var deviceId = PreviewDeviceId(designPayload);
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                _messages.Warning("Preview", "No device selected.");
                return;
            }

            var metrics = _showCanonicalFrame
                ? CanonicalPreviewMetrics()
                : ApplyPreviewOrientation(_database.GetDevicePreviewMetrics(deviceId));
            var themeName = _themeComboBox.SelectedItem?.Label ?? "No theme";
            designPayload = ProcessPreviewPayload(designPayload, "static");
            UpdateDesignContextChrome(designPayload);
            if (_selectedPlaybackRoute == "raster"
                && designPayload is not null
                && IsPreviewPlaybackActive
                && _rasterPlaybackFrames.TryGetValue(PlaybackFrameKey(designPayload), out var rasterPath))
            {
                _designPreviewPane.ShowRasterFrame(rasterPath);
                RecordAndUpdatePlaybackStatus(new DesignWebPreviewPane.DesignPreviewFrameStatus(
                    ElapsedMilliseconds: 0,
                    IsAnimationOnly: true,
                    UsedDomPatch: false,
                    RenderError: false));
                _messages.Clear();
                return;
            }
            _designPreviewPane.HideRasterFrame();
            _designPreviewPane.Update(
                metrics,
                _isDark(),
                themeName,
                _selectedMode,
                _selectedScale,
                _showDesignMarks,
                !_showCanonicalFrame,
                CurrentReferenceState(),
                designPayload,
                contextState,
                IsPreviewPlaybackActive,
                _messages);
            if (designPayload is not null && _designInputsPanel.IsPlaybackActive)
            {
                SchedulePlaybackAheadPreload(metrics, designPayload);
            }
            _messages.Clear();
        }
        catch (Exception exception)
        {
            _messages.Error("Preview", exception);
        }
    }

    public void TriggerDesignPreviewAction(string actionId)
    {
        if (_designInputsPanel.TriggerAction(actionId))
        {
            return;
        }

        Refresh();
        _designInputsPanel.TriggerAction(actionId);
    }

    public void SetDesignPreviewTestValue(string jsonKey, string value)
    {
        _designInputsPanel.SetExternalInputValue(jsonKey, value);
    }

    public void SetDesignPreviewCollectionTestValue(
        string collectionJsonKey,
        string itemId,
        ComponentInputDefinition input,
        string value)
    {
        _designInputsPanel.SetExternalCollectionInputValue(collectionJsonKey, itemId, input, value);
    }

    public JsonObject ApplyDesignPreviewTransientTestValues(JsonObject preview)
    {
        return _designInputsPanel.ApplyTransientTestValues(preview);
    }

    public bool ResetDesignPreviewTestValues()
    {
        return _designInputsPanel.ResetCurrentTestValues();
    }

    private async Task<bool> PreparePlaybackFramesAsync(ComponentPreviewActionDefinition? requestedAction)
    {
        ReleaseFrameCacheReservation();
        EnsureSelectedOptionsExist();
        if (string.IsNullOrWhiteSpace(_projectId))
        {
            return true;
        }

        var designPayload = DesignPreviewPayloadForSelection();
        if (designPayload is null)
        {
            return true;
        }

        var deviceId = PreviewDeviceId(designPayload);
        if (string.IsNullOrWhiteSpace(deviceId)) return true;
        var metrics = ApplyPreviewOrientation(_database.GetDevicePreviewMetrics(deviceId));
        var payload = ProcessPreviewPayload(designPayload, "playback-prepare") ?? designPayload;
        var projectFps = payload.FrameRate;
        var previewFps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        var frames = _pendingPlaybackFramesOverride?.ToList()
            ?? PlaybackFramePayloads(payload, projectFps, requestedAction).ToList();
        if (frames.Count == 0)
        {
            PreviewDebugLog.Write(
                "preview.playback.frames.skip",
                ("component", payload.ComponentType),
                ("name", payload.Name),
                ("projectFps", projectFps),
                ("previewFps", previewFps),
                ("reason", "no-frames"));
            return true;
        }
        _frameCacheReservation = WebDesignPreviewRenderer.ReserveFrameCacheCapacity(frames.Count);
        if (_selectedPlaybackRoute != "raster")
        {
            var prewarmStopwatch = Stopwatch.StartNew();
            ShowPreviewLoading($"Preparing HTML 0 / {frames.Count} frames…", () => { });
            try
            {
                var imageSources = new HashSet<string>(StringComparer.Ordinal);
                for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
                {
                    var bodyContent = await WebDesignPreviewRenderer.RenderBodyAsync(
                        metrics,
                        _selectedMode,
                        _showDesignMarks,
                        frames[frameIndex]);
                    foreach (var source in DesignWebPreviewPane.ImageSourcesForPreload(bodyContent))
                    {
                        imageSources.Add(source);
                    }
                    if ((frameIndex + 1) % 5 == 0 || frameIndex + 1 == frames.Count)
                    {
                        _designPreviewPane.SetRasterLoading(
                            true,
                            $"Preparing HTML {frameIndex + 1} / {frames.Count} frames…");
                    }
                }
                _designPreviewPane.SetRasterLoading(true, $"Decoding HTML assets 0 / {imageSources.Count}…");
                var loadedImages = await _designPreviewPane.PreloadFrameImagesAsync(imageSources, CancellationToken.None);
                _designPreviewPane.SetRasterLoading(true, $"Decoding HTML assets {loadedImages} / {imageSources.Count}…");
            }
            catch
            {
                ReleaseFrameCacheReservation();
                throw;
            }
            finally
            {
                HidePreviewLoading();
            }
            PreviewDebugLog.Write(
                "preview.playback.prepare.html",
                ("route", _selectedPlaybackRoute),
                ("frames", frames.Count),
                ("fps", previewFps),
                ("ms", prewarmStopwatch.Elapsed.TotalMilliseconds));
            return true;
        }
        var rasterSignature = RasterPlaybackSignature(metrics, payload, frames);
        if (_rasterPlaybackSignature == rasterSignature
            && _rasterPlaybackOrder.Count == frames.Count
            && frames.All((frame) => _rasterPlaybackFrames.ContainsKey(PlaybackFrameKey(frame))))
        {
            try
            {
                await _designPreviewPane.PrepareRasterPlaybackAsync(_rasterPlaybackOrder, CancellationToken.None);
                await _designPreviewPane.SyncRasterViewportAsync();
                PreviewDebugLog.Write(
                    "preview.playback.raster-cache-hit",
                    ("component", payload.ComponentType),
                    ("name", payload.Name),
                    ("frames", frames.Count));
                return true;
            }
            catch
            {
                ReleaseFrameCacheReservation();
                throw;
            }
        }

        _previewLoadingCancellation?.Cancel();
        _previewLoadingCancellation?.Dispose();
        _aheadPreloadCancellation?.Cancel();
        _aheadPreloadCancellation?.Dispose();
        _aheadPreloadCancellation = null;
        _aheadPreloadedFrameKeys.Clear();
        var cancellation = new CancellationTokenSource();
        _previewLoadingCancellation = cancellation;
        var totalStopwatch = Stopwatch.StartNew();
        PreviewDebugLog.Write(
            "preview.playback.frames.start",
            ("component", payload.ComponentType),
            ("name", payload.Name),
            ("projectFps", projectFps),
            ("previewFps", previewFps),
            ("multiplier", PreviewPlaybackTiming.FrameRateMultiplier),
            ("frames", frames.Count),
            ("themeMode", _selectedMode),
            ("scale", _selectedScale),
            ("marks", _showDesignMarks));
        if (frames.Count > LoadingPreviewFrameThreshold)
        {
            ShowPreviewLoading(
                $"Rasterizing {frames.Count} frames for playback...",
                () =>
                {
                    PreviewDebugLog.Write(
                        "preview.playback.frames.cancel.request",
                        ("component", payload.ComponentType),
                        ("name", payload.Name),
                        ("frames", frames.Count));
                    cancellation.Cancel();
                });
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_rasterCacheDirectory) && Directory.Exists(_rasterCacheDirectory))
            {
                Directory.Delete(_rasterCacheDirectory, recursive: true);
            }
            _rasterCacheDirectory = Path.Combine(Path.GetTempPath(), "mockups-preview-raster", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rasterCacheDirectory);
            _rasterPlaybackFrames.Clear();
            _rasterPlaybackOrder.Clear();
            UpdateRasterProgress(0, frames.Count);
            for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                var frame = frames[frameIndex];
                UpdateRasterProgress(frameIndex, frames.Count);
                _aheadPreloadedFrameKeys.Add(PlaybackFrameKey(frame));
                cancellation.Token.ThrowIfCancellationRequested();
                var rasterHtml = await _designPreviewPane.BuildRasterHtmlAsync(metrics, _selectedMode, frame);
                var rasterPath = Path.Combine(_rasterCacheDirectory, $"frame-{frameIndex:D6}.webp");
                await _chromiumRasterizer.RasterizeAsync(
                    rasterHtml,
                    Math.Max(1, (int)Math.Ceiling(metrics.CanvasWidth)),
                    Math.Max(1, (int)Math.Ceiling(metrics.CanvasHeight)),
                    rasterPath,
                    "webp",
                    quality: 95,
                    captureScale: 1,
                    cancellation.Token);
                _rasterPlaybackFrames[PlaybackFrameKey(frame)] = rasterPath;
                _rasterPlaybackOrder.Add(rasterPath);
                UpdateRasterProgress(frameIndex + 1, frames.Count);
            }
            _rasterPlaybackSignature = rasterSignature;
            await _designPreviewPane.PrepareRasterPlaybackAsync(_rasterPlaybackOrder, cancellation.Token);
            await _designPreviewPane.SyncRasterViewportAsync();
            PreviewDebugLog.Write(
                "preview.playback.frames.end",
                ("component", payload.ComponentType),
                ("name", payload.Name),
                ("frames", _rasterPlaybackFrames.Count),
                ("totalFrames", frames.Count),
                ("ms", totalStopwatch.Elapsed.TotalMilliseconds));
            return true;
        }
        catch (OperationCanceledException)
        {
            ReleaseFrameCacheReservation();
            PreviewDebugLog.Write(
                "preview.playback.frames.cancelled",
                ("component", payload.ComponentType),
                ("name", payload.Name),
                ("frames", frames.Count),
                ("ms", totalStopwatch.Elapsed.TotalMilliseconds));
            return false;
        }
        catch (Exception error)
        {
            ReleaseFrameCacheReservation();
            PreviewDebugLog.Write(
                "preview.playback.frames.error",
                ("component", payload.ComponentType),
                ("name", payload.Name),
                ("frames", _rasterPlaybackFrames.Count),
                ("ms", totalStopwatch.Elapsed.TotalMilliseconds),
                ("error", error.Message));
            _messages.Error("Playback raster", error);
            _rasterPlaybackFrames.Clear();
            _rasterPlaybackOrder.Clear();
            _rasterPlaybackSignature = "";
            return false;
        }
        finally
        {
            if (ReferenceEquals(_previewLoadingCancellation, cancellation))
            {
                _previewLoadingCancellation = null;
                HidePreviewLoading();
            }

            cancellation.Dispose();
        }
    }

    private async Task PreloadPlaybackFramesAsync(
        SpikeDatabase.DevicePreviewMetrics metrics,
        DesignPreviewPayload ownerPayload,
        IReadOnlyList<DesignPreviewPayload> frames,
        CancellationToken cancellationToken,
        string phase)
    {
        if (frames.Count == 0) return;

        var imageSources = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < frames.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frameStopwatch = Stopwatch.StartNew();
            var frame = frames[index];
            var bodyContent = await WebDesignPreviewRenderer.RenderPrewarmBodyAsync(
                metrics,
                _selectedMode,
                _showDesignMarks,
                frame);
            foreach (var source in DesignWebPreviewPane.ImageSourcesForPreload(bodyContent))
            {
                imageSources.Add(source);
            }
            PreviewDebugLog.Write(
                "preview.playback.frames.prewarm",
                ("component", ownerPayload.ComponentType),
                ("phase", phase),
                ("frame", index + 1),
                ("frames", frames.Count),
                ("ms", frameStopwatch.Elapsed.TotalMilliseconds),
                ("time", PlaybackFrameTime(frame)),
                ("sources", imageSources.Count));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var loadedImages = await _designPreviewPane.PreloadFrameImagesAsync(imageSources, cancellationToken);
        PreviewDebugLog.Write(
            "preview.playback.frames.images",
            ("component", ownerPayload.ComponentType),
            ("name", ownerPayload.Name),
            ("phase", phase),
            ("frames", frames.Count),
            ("sources", imageSources.Count),
            ("loadedImages", loadedImages));
    }

    private void SchedulePlaybackAheadPreload(
        SpikeDatabase.DevicePreviewMetrics metrics,
        DesignPreviewPayload payload)
    {
        if (_isAheadPreloading || string.IsNullOrWhiteSpace(_projectId))
        {
            return;
        }

        if (JsonNode.Parse(string.IsNullOrWhiteSpace(payload.DesignPreviewJson) ? "{}" : payload.DesignPreviewJson) is not JsonObject preview
            || PlaybackFrameAction(preview)?.PrewarmFrames != true)
        {
            return;
        }

        var projectFps = payload.FrameRate;
        var frames = PlaybackAheadFramePayloads(payload, projectFps)
            .Where((frame) => _aheadPreloadedFrameKeys.Add(PlaybackFrameKey(frame)))
            .Take(AheadPlaybackPreloadFrames)
            .ToList();
        if (frames.Count == 0)
        {
            return;
        }

        _aheadPreloadCancellation ??= new CancellationTokenSource();
        var token = _aheadPreloadCancellation.Token;
        _isAheadPreloading = true;
        _ = PreloadPlaybackFramesAsync(metrics, payload, frames, token, "ahead")
            .ContinueWith((_) => _isAheadPreloading = false, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ShowPreviewLoading(string message, Action cancel)
    {
        _designPreviewPane.SetRasterLoading(true, message);
        SetPreviewPerformanceStatus(PreviewPerformanceStatus.Loading);
    }

    private void UpdateRasterProgress(int completedFrames, int totalFrames)
    {
        var message = $"Rasterizing {completedFrames} / {totalFrames} frames…";
        _designPreviewPane.SetRasterLoading(true, message);
        _messages.Info("Playback", message);
    }

    private void HidePreviewLoading()
    {
        _designPreviewPane.SetRasterLoading(false, "");
        if (!IsPreviewPlaybackActive && !_shotPlaybackIsPreparing)
        {
            SetPreviewPerformanceStatus(PreviewPerformanceStatus.Idle);
        }
    }

    private void OnDesignPreviewFrameStatusChanged(DesignWebPreviewPane.DesignPreviewFrameStatus status)
    {
        RecordAndUpdatePlaybackStatus(status);
    }

    private void RecordAndUpdatePlaybackStatus(DesignWebPreviewPane.DesignPreviewFrameStatus status)
    {
        var actualFps = RecordPresentedPlaybackFrame(status);
        _designInputsPanel.NotifyPlaybackFramePresented();
        if (!IsPreviewPlaybackActive)
        {
            SetPreviewPerformanceStatus(PreviewPerformanceStatus.Idle);
            return;
        }
        if (actualFps is null)
        {
            SetPreviewPerformanceStatus(PreviewPerformanceStatus.Loading);
            return;
        }
        var targetFps = Math.Max(1, CurrentPlaybackFrameRate());
        var tolerance = targetFps * 0.02;
        SetPreviewPerformanceStatus(actualFps < targetFps - tolerance
            ? PreviewPerformanceStatus.Slow
            : actualFps > targetFps + tolerance
                ? PreviewPerformanceStatus.Fast
                : PreviewPerformanceStatus.Good);
    }

    private void OnPlaybackStarted(ComponentPreviewInputSession.PlaybackRunInfo run)
    {
        _playbackSummaryGeneration++;
        _playbackPerformanceRun = new PlaybackPerformanceRun(run.TargetFrames, run.TargetFps, Stopwatch.GetTimestamp());
        if (_selectedPlaybackRoute == "raster"
            && _rasterPlaybackOrder.Count > 0)
        {
            _designPreviewPane.PlayRasterFrames(_rasterPlaybackOrder, run.TargetFps);
        }
    }

    private string RasterPlaybackSignature(
        SpikeDatabase.DevicePreviewMetrics metrics,
        DesignPreviewPayload payload,
        IReadOnlyList<DesignPreviewPayload> frames)
    {
        return string.Join(
            "\u001f",
            payload.Kind,
            payload.ComponentType,
            payload.Name,
            payload.ConfigJson,
            payload.ThemeTokensJson,
            payload.ThemeStatusBarPresetId,
            payload.ThemeNavigationBarPresetId,
            payload.ComponentBaseConfigsJson,
            payload.AppConfigJson,
            payload.FrameRate,
            _selectedMode,
            _showDesignMarks,
            metrics.CanvasWidth,
            metrics.CanvasHeight,
            frames.Count,
            string.Join("|", frames.Select(PlaybackFrameKey)));
    }

    private void OnPlaybackStopped(ComponentPreviewInputSession.PlaybackRunInfo run)
    {
        ReleaseFrameCacheReservation();
        if (_playbackPerformanceRun is null) return;
        _playbackPerformanceRun.AcceptsPresentations = false;
        var generation = ++_playbackSummaryGeneration;
        DispatcherTimer.RunOnce(
            () =>
            {
                if (generation == _playbackSummaryGeneration) FinalizePlaybackSummary();
            },
            TimeSpan.FromMilliseconds(750));
    }

    private void ReleaseFrameCacheReservation()
    {
        _frameCacheReservation?.Dispose();
        _frameCacheReservation = null;
    }

    private double? RecordPresentedPlaybackFrame(DesignWebPreviewPane.DesignPreviewFrameStatus status)
    {
        var run = _playbackPerformanceRun;
        if (run is null || !run.AcceptsPresentations || status.RenderError) return null;
        var now = Stopwatch.GetTimestamp();
        if (run.LastPresentedTimestamp != 0)
        {
            var seconds = Stopwatch.GetElapsedTime(run.LastPresentedTimestamp, now).TotalSeconds;
            if (seconds > 0) run.PresentationFps.Add(1.0 / seconds);
        }
        run.LastPresentedTimestamp = now;
        run.PresentedFrames++;
        run.RecentPresentationTimestamps.Enqueue(now);
        while (run.RecentPresentationTimestamps.Count > 13) run.RecentPresentationTimestamps.Dequeue();
        if (run.RecentPresentationTimestamps.Count < 3) return null;
        var first = run.RecentPresentationTimestamps.Peek();
        var windowSeconds = Stopwatch.GetElapsedTime(first, now).TotalSeconds;
        return windowSeconds > 0 ? (run.RecentPresentationTimestamps.Count - 1) / windowSeconds : null;
    }

    private void FinalizePlaybackSummary()
    {
        var run = _playbackPerformanceRun;
        if (run is null) return;
        _playbackPerformanceRun = null;
        var presented = Math.Min(run.TargetFrames, run.PresentedFrames);
        var discarded = Math.Max(0, run.TargetFrames - presented);
        var elapsedSeconds = run.LastPresentedTimestamp == 0
            ? 0
            : Stopwatch.GetElapsedTime(run.StartedTimestamp, run.LastPresentedTimestamp).TotalSeconds;
        var averageFps = elapsedSeconds > 0 ? presented / elapsedSeconds : 0;
        var minimumFps = run.PresentationFps.Count > 0 ? run.PresentationFps.Min() : averageFps;
        var maximumFps = run.PresentationFps.Count > 0 ? run.PresentationFps.Max() : averageFps;
        var summary = string.Format(
            CultureInfo.CurrentCulture,
            "Last play · {0}/{1} frames · {2} discarded · FPS avg {3:0.0} · min {4:0.0} · max {5:0.0}",
            presented,
            run.TargetFrames,
            discarded,
            averageFps,
            minimumFps,
            maximumFps);
        _messages.Info("Playback", summary);
        PreviewDebugLog.Write(
            "preview.playback.summary",
            ("targetFrames", run.TargetFrames),
            ("presentedFrames", presented),
            ("discardedFrames", discarded),
            ("targetFps", run.TargetFps),
            ("averageFps", averageFps),
            ("minimumFps", minimumFps),
            ("maximumFps", maximumFps));
    }

    private sealed class PlaybackPerformanceRun(int targetFrames, int targetFps, long startedTimestamp)
    {
        public int TargetFrames { get; } = targetFrames;
        public int TargetFps { get; } = targetFps;
        public long StartedTimestamp { get; } = startedTimestamp;
        public long LastPresentedTimestamp { get; set; }
        public int PresentedFrames { get; set; }
        public bool AcceptsPresentations { get; set; } = true;
        public List<double> PresentationFps { get; } = [];
        public Queue<long> RecentPresentationTimestamps { get; } = [];
    }

    private void SetPreviewPerformanceStatus(PreviewPerformanceStatus status)
    {
        _previewPerformanceDot.Background = status switch
        {
            PreviewPerformanceStatus.Loading => PreviewStatusLoadingBrush,
            PreviewPerformanceStatus.Fast => PreviewStatusLoadingBrush,
            PreviewPerformanceStatus.Good => PreviewStatusGoodBrush,
            PreviewPerformanceStatus.Slow => PreviewStatusSlowBrush,
            _ => PreviewStatusIdleBrush,
        };
        _previewPerformanceDot.BorderBrush = status == PreviewPerformanceStatus.Idle
            ? PreviewStatusIdleBorder
            : Brushes.Transparent;
    }

    private enum PreviewPerformanceStatus
    {
        Idle,
        Loading,
        Fast,
        Good,
        Slow,
    }

    private static IEnumerable<DesignPreviewPayload> PlaybackFramePayloads(
        DesignPreviewPayload payload,
        int projectFps,
        ComponentPreviewActionDefinition? requestedAction = null)
    {
        var fps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        if (JsonNode.Parse(string.IsNullOrWhiteSpace(payload.DesignPreviewJson) ? "{}" : payload.DesignPreviewJson) is not JsonObject preview)
        {
            yield break;
        }

        var action = requestedAction is not null && ComponentPreviewActions.IsApplicable(preview, requestedAction)
            ? requestedAction
            : PlaybackFrameAction(preview);
        if (action is null)
        {
            yield break;
        }

        var timeJsonKey = action.TimeJsonKey;
        var durationInputId = action.DurationInputId;
        var animationDurationSeconds = action.DurationSeconds;
        if (string.IsNullOrWhiteSpace(timeJsonKey)
            || (string.IsNullOrWhiteSpace(durationInputId)
                && string.IsNullOrWhiteSpace(action.DurationCollectionJsonKey)
                && !action.DurationOwnerTimeline
                && animationDurationSeconds <= 0))
        {
            yield break;
        }

        var frameCount = PlaybackDurationFrames(action, preview, fps, payload.ThemeTokensJson);
        if (frameCount <= 0)
        {
            yield break;
        }

        for (var frame = 0; frame <= frameCount; frame++)
        {
            var framePreview = JsonNode.Parse(preview.ToJsonString()) as JsonObject ?? new JsonObject();
            ComponentPreviewActions.SetValue(
                framePreview,
                action,
                timeJsonKey,
                action.TimeUnit == ComponentPreviewActionTimeUnit.Frames
                    ? frame
                    : action.TimeUnit == ComponentPreviewActionTimeUnit.Milliseconds
                        ? frame / (double)fps * 1000
                        : frame / (double)fps);
            ComponentPreviewActions.SetValue(framePreview, action, action.PlayInputId, true);
            yield return payload with { DesignPreviewJson = framePreview.ToJsonString() };
        }
    }

    private static IEnumerable<DesignPreviewPayload> PlaybackAheadFramePayloads(DesignPreviewPayload payload, int projectFps)
    {
        var fps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        if (JsonNode.Parse(string.IsNullOrWhiteSpace(payload.DesignPreviewJson) ? "{}" : payload.DesignPreviewJson) is not JsonObject preview)
        {
            yield break;
        }

        var action = PlaybackFrameAction(preview);
        if (action is null || string.IsNullOrWhiteSpace(action.TimeJsonKey))
        {
            yield break;
        }

        var durationFrames = PlaybackDurationFrames(action, preview, fps, payload.ThemeTokensJson);
        if (durationFrames <= 0)
        {
            yield break;
        }

        var currentFrame = action.TimeUnit == ComponentPreviewActionTimeUnit.Frames
            ? Math.Max(0, (int)Math.Floor(JsonNodeNumber(ComponentPreviewActions.Value(preview, action, action.TimeJsonKey), 0)))
            : action.TimeUnit == ComponentPreviewActionTimeUnit.Milliseconds
                ? Math.Max(0, (int)Math.Floor(JsonNodeNumber(ComponentPreviewActions.Value(preview, action, action.TimeJsonKey), 0) / 1000 * fps))
                : Math.Max(0, (int)Math.Floor(JsonNodeNumber(ComponentPreviewActions.Value(preview, action, action.TimeJsonKey), 0) * fps));
        for (var index = 1; index <= AheadPlaybackPreloadFrames * 2; index++)
        {
            var frame = currentFrame + index;
            if (frame > durationFrames)
            {
                yield break;
            }

            var framePreview = JsonNode.Parse(preview.ToJsonString()) as JsonObject ?? new JsonObject();
            ComponentPreviewActions.SetValue(
                framePreview,
                action,
                action.TimeJsonKey,
                action.TimeUnit == ComponentPreviewActionTimeUnit.Frames
                    ? frame
                    : action.TimeUnit == ComponentPreviewActionTimeUnit.Milliseconds
                        ? frame / (double)fps * 1000
                        : frame / (double)fps);
            ComponentPreviewActions.SetValue(framePreview, action, action.PlayInputId, true);
            yield return payload with { DesignPreviewJson = framePreview.ToJsonString() };
        }
    }

    private static int PlaybackDurationFrames(ComponentPreviewActionDefinition action, JsonObject preview, int fps, string themeTokensJson)
    {
        if (action.DurationOwnerTimeline)
        {
            return RuntimeTimeline.DurationFrames(preview.ToJsonString(), preview.ToJsonString(), "{}", 1, themeTokensJson);
        }
        if (!string.IsNullOrWhiteSpace(action.DurationCollectionJsonKey))
        {
            return CollectionDurationFrames(preview, action);
        }

        if (action.TimeUnit == ComponentPreviewActionTimeUnit.Frames)
        {
            return Math.Max(0, (int)Math.Round(
                JsonNodeNumber(ComponentPreviewActions.Value(preview, action, action.DurationInputId), 0),
                MidpointRounding.AwayFromZero));
        }

        var duration = action.DurationSeconds > 0
            ? action.DurationSeconds
            : Math.Max(0, JsonNodeNumber(ComponentPreviewActions.Value(preview, action, action.DurationInputId), 0));
        return duration <= 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(duration * Math.Max(1, fps)));
    }

    private static int CollectionDurationFrames(JsonObject preview, ComponentPreviewActionDefinition action)
    {
        if (preview[action.DurationCollectionJsonKey] is not JsonArray items)
        {
            return Math.Max(1, (int)Math.Round(action.DurationBaseFrames, MidpointRounding.AwayFromZero));
        }

        var total = action.DurationBaseFrames;
        foreach (var item in items.OfType<JsonObject>())
        {
            foreach (var key in action.DurationItemNumberKeys)
            {
                total += JsonNumber(item, key, 0);
            }
            foreach (var key in action.DurationCollectionMultiplierNumberKeys)
            {
                total += JsonNumber(preview, key, 0);
            }
        }
        return Math.Max(1, (int)Math.Ceiling(total));
    }

    private static string PlaybackFrameKey(DesignPreviewPayload payload)
    {
        return string.Join(
            "\u001f",
            payload.ComponentType,
            payload.Name,
            payload.InstanceJson.GetHashCode(StringComparison.Ordinal),
            PlaybackFrameTime(payload),
            payload.DesignPreviewJson.GetHashCode(StringComparison.Ordinal));
    }

    private static string PlaybackFrameTime(DesignPreviewPayload payload)
    {
        try
        {
            if (JsonNode.Parse(string.IsNullOrWhiteSpace(payload.DesignPreviewJson) ? "{}" : payload.DesignPreviewJson) is not JsonObject preview)
            {
                return "";
            }

            var action = PlaybackFrameAction(preview);
            if (action is null || string.IsNullOrWhiteSpace(action.TimeJsonKey))
            {
                return "";
            }

            return ComponentPreviewActions.Value(preview, action, action.TimeJsonKey)?.ToJsonString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static ComponentPreviewActionDefinition? PlaybackFrameAction(JsonObject preview)
    {
        var actions = ComponentPreviewActions.ReadApplicable(preview);
        return actions.FirstOrDefault((action) => JsonBoolean(ComponentPreviewActions.Value(preview, action, action.PlayInputId)))
            ?? actions.FirstOrDefault();
    }

    private static bool JsonBoolean(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return false;
        }

        if (value.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        return value.TryGetValue<string>(out var text) && BooleanText.Parse(text);
    }

    private static string JsonString(JsonObject owner, string key)
    {
        return owner[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : "";
    }

    private static double JsonNumber(JsonObject owner, string key, double fallback)
    {
        if (owner[key] is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<double>(out var number))
        {
            return number;
        }

        return value.TryGetValue<string>(out var text)
            && double.TryParse(text.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static double JsonNodeNumber(JsonNode? node, double fallback)
    {
        if (node is not JsonValue value) return fallback;
        if (value.TryGetValue<double>(out var number)) return number;
        return value.TryGetValue<string>(out var text)
            && double.TryParse(text.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private SpikeDatabase.DevicePreviewMetrics ApplyPreviewOrientation(SpikeDatabase.DevicePreviewMetrics metrics)
    {
        if (_selectedOrientation != "landscape")
        {
            return metrics;
        }

        return metrics with
        {
            CanvasWidth = metrics.CanvasHeight,
            CanvasHeight = metrics.CanvasWidth,
            ScreenX = metrics.ScreenY,
            ScreenY = metrics.ScreenX,
            ScreenWidth = metrics.ScreenHeight,
            ScreenHeight = metrics.ScreenWidth,
            CornerRadius = metrics.CornerRadiusCoefficient > 0
                ? metrics.CanvasHeight * metrics.CornerRadiusCoefficient
                : metrics.CornerRadius,
        };
    }

    private static SpikeDatabase.DevicePreviewMetrics CanonicalPreviewMetrics()
    {
        return new SpikeDatabase.DevicePreviewMetrics(
            "Canonical 360 × 800",
            360,
            800,
            0,
            0,
            360,
            800,
            0,
            0,
            0,
            0,
            0,
            1);
    }

    private DesignPreviewPayload? DesignPreviewPayloadForSelection()
    {
        if (_workspace == EditorWorkspace.Production)
        {
            var selected = _selectedNode();
            if (!_isDesignPreviewContextLocked
                && selected?.Kind is not ProjectTreeNodeKind.Shot and not ProjectTreeNodeKind.ModuleInstance)
            {
                _activeDesignPreviewNode = null;
                return null;
            }
            var productionNode = ProductionPayloadNode();
            var productionPayload = DesignPreviewPayloadFactory.Create(
                _database,
                productionNode,
                _selectedThemeId,
                _selectedMode,
                _shotPreviewFrame);
            if (productionPayload is not null && productionNode is not null)
            {
                _lastProductionPreviewNode = PreviewNodeKey.From(productionNode);
                _activeDesignPreviewNode = _lastProductionPreviewNode;
            }
            else
            {
                _activeDesignPreviewNode = null;
            }
            return productionPayload;
        }
        if (_isDesignPreviewContextLocked && _lockedDesignPreviewNode is not null)
        {
            var lockedPayload = DesignPreviewPayloadFactory.Create(_database, _lockedDesignPreviewNode.ToNode(), _selectedThemeId, _selectedMode, _shotPreviewFrame);
            if (lockedPayload is not null)
            {
                _activeDesignPreviewNode = _lockedDesignPreviewNode;
                return lockedPayload;
            }

            _isDesignPreviewContextLocked = false;
            _lockedDesignPreviewNode = null;
        }

        var selectedNode = _selectedNode();
        var selectedPayload = DesignPreviewPayloadFactory.Create(_database, selectedNode, _selectedThemeId, _selectedMode, _shotPreviewFrame);
        if (selectedPayload is not null && selectedNode is not null)
        {
            _lastDesignPreviewNode = PreviewNodeKey.From(selectedNode);
            _activeDesignPreviewNode = _lastDesignPreviewNode;
            return selectedPayload;
        }

        _activeDesignPreviewNode = null;
        return null;
    }

    private const string PreviewRetryTargetId = "__preview_retry__";

    private PreviewContextState NonRenderableStateForSelection()
    {
        var selected = _selectedNode();
        var destination = FirstRenderableDescendant(selected);
        return new PreviewContextState(
            PreviewContextStateKind.NonRenderable,
            selected is null ? "There is no renderable selection" : $"{selected.Name} has no direct preview",
            selected is null
                ? "Select a component, module, or Screen to view its resolved result."
                : "This object organizes or contains other elements, but does not produce an image by itself.",
            destination is null ? "" : "View renderable items",
            destination?.Id ?? "");
    }

    private ProjectTreeNode? FirstRenderableDescendant(ProjectTreeNode? node)
    {
        if (node is null) return null;
        foreach (var child in node.Children)
        {
            if (DesignPreviewPayloadFactory.Create(
                    _database,
                    child,
                    _selectedThemeId,
                    _selectedMode,
                    _shotPreviewFrame) is not null)
            {
                return child;
            }

            var nested = FirstRenderableDescendant(child);
            if (nested is not null) return nested;
        }

        return null;
    }

    private void UpdateShotTimelineControls()
    {
        var shotId = ProductionShotId();
        if (_workspace != EditorWorkspace.Production || string.IsNullOrWhiteSpace(shotId))
        {
            StopShotPlayback();
            _shotTimelineControls.IsVisible = false;
            _shotHeaderTimelineControls.IsVisible = false;
            return;
        }
        var contextNode = ProductionContextNode();
        var duration = ModuleInstanceTimeline.ShotDurationFrames(_database, shotId);
        if (_shotTimelineShotId != shotId || _shotTimelineContextNodeId != contextNode?.Id)
        {
            var pendingExplicitFrame = _pendingExplicitScreenFrame;
            _pendingExplicitScreenFrame = null;
            _shotTimelineShotId = shotId;
            _shotTimelineContextNodeId = contextNode?.Id ?? "";
            SetShotNavigationScope(
                contextNode?.Kind == ProjectTreeNodeKind.ModuleInstance ? "screen" : "shot");
            _shotPreviewFrame = pendingExplicitFrame
                ?? (contextNode?.Kind == ProjectTreeNodeKind.ModuleInstance
                    ? ModuleInstanceStartFrame(shotId, contextNode.Id)
                    : 0);
        }
        _shotPreviewFrame = Math.Clamp(_shotPreviewFrame, 0, Math.Max(0, duration - 1));
        var range = NavigationFrameRange();
        var displayedFrame = Math.Clamp(_shotPreviewFrame - range.StartFrame, 0, Math.Max(0, range.DurationFrames - 1));
        _isUpdatingShotTimeline = true;
        _shotFrameSlider.Maximum = Math.Max(0, range.DurationFrames - 1);
        _shotFrameSlider.Value = displayedFrame;
        _shotFrameText.Text = $"{displayedFrame}/{Math.Max(0, range.DurationFrames - 1)}";
        EditorAccessibility.Describe(
            _shotFrameText,
            $"Frame {displayedFrame} of {Math.Max(0, range.DurationFrames - 1)}",
            showToolTip: false);
        _shotPreviousFrameButton.IsEnabled = _shotPreviewFrame > range.StartFrame;
        _shotNextFrameButton.IsEnabled = _shotPreviewFrame < range.EndFrame;
        _shotAbsoluteStartButton.IsEnabled = _shotPreviewFrame > 0;
        _shotAbsoluteEndButton.IsEnabled = _shotPreviewFrame < duration - 1;
        var keyframes = AnimationKeyframesInCurrentScreen();
        _shotPreviousKeyframeButton.IsEnabled = keyframes.Any((frame) => frame < _shotPreviewFrame);
        _shotNextKeyframeButton.IsEnabled = keyframes.Any((frame) => frame > _shotPreviewFrame);
        var isOnKeyframe = keyframes.Contains(_shotPreviewFrame);
        _shotPlayButton.BorderBrush = isOnKeyframe
            ? EditorAnimationVisuals.ActiveTrackBrush
            : Brushes.Transparent;
        _shotPlayButton.BorderThickness = new Thickness(2);
        EditorAccessibility.Describe(
            _shotPlayButton,
            isOnKeyframe
                ? "Play or pause the selected scope; current frame is an animation keyframe"
                : "Play or pause the selected scope");
        var activeSlotIndex = ActiveShotSlotIndex(shotId);
        var slotCount = _database.GetShotModuleInstanceSlots(shotId).Count;
        var showScreenStep = _shotNavigationScope == "shot";
        _shotPreviousSlotButton.IsVisible = showScreenStep;
        _shotNextSlotButton.IsVisible = showScreenStep;
        _shotPreviousSlotButton.IsEnabled = showScreenStep && activeSlotIndex > 0;
        _shotNextSlotButton.IsEnabled = showScreenStep && activeSlotIndex >= 0 && activeSlotIndex < slotCount - 1;
        _shotTimelineControls.IsVisible = true;
        _shotHeaderTimelineControls.IsVisible = true;
        _isUpdatingShotTimeline = false;
    }

    private void SetShotNavigationScope(string scope)
    {
        _shotNavigationScope = scope == "screen" ? "screen" : "shot";
        if (_shotNavigationScopeComboBox.ItemsSource is not IEnumerable<FieldOption> options) return;
        var selected = options.FirstOrDefault((option) => option.Value == _shotNavigationScope);
        if (selected is null || ReferenceEquals(_shotNavigationScopeComboBox.SelectedItem, selected)) return;
        var wasUpdating = _isUpdatingShotTimeline;
        _isUpdatingShotTimeline = true;
        _shotNavigationScopeComboBox.SelectedItem = selected;
        _isUpdatingShotTimeline = wasUpdating;
    }

    private (int StartFrame, int EndFrame, int DurationFrames) NavigationFrameRange()
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return (0, 0, 1);
        var shotDuration = Math.Max(1, ModuleInstanceTimeline.ShotDurationFrames(_database, shotId));
        if (_shotNavigationScope != "screen") return (0, shotDuration - 1, shotDuration);
        return ActiveScreenFrameRange(shotId);
    }

    private (int StartFrame, int EndFrame, int DurationFrames) ActiveScreenFrameRange(string shotId)
    {
        var shotDuration = Math.Max(1, ModuleInstanceTimeline.ShotDurationFrames(_database, shotId));
        var slots = _database.GetShotModuleInstanceSlots(shotId);
        var index = ActiveShotSlotIndex(shotId);
        if (index < 0 || index >= slots.Count) return (0, shotDuration - 1, shotDuration);
        var start = ModuleInstanceStartFrame(shotId, slots[index].Id);
        var duration = Math.Max(1, ModuleInstanceTimeline.DurationFrames(_database, slots[index].Id));
        return (start, start + duration - 1, duration);
    }

    private void SetShotPreviewFrame(
        int frame,
        bool useSelectedScope = true,
        bool synchronizeScreenSelection = false)
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return;
        StopShotPlayback();
        var range = useSelectedScope
            ? NavigationFrameRange()
            : (StartFrame: 0, EndFrame: ShotLastFrame(), DurationFrames: ShotLastFrame() + 1);
        var next = Math.Clamp(frame, range.StartFrame, range.EndFrame);
        if (next == _shotPreviewFrame)
        {
            if (synchronizeScreenSelection) SynchronizeExplicitScreenSelection();
            return;
        }
        _shotPreviewFrame = next;
        PlaybackState.NotifyFrameChanged();
        if (synchronizeScreenSelection && SynchronizeExplicitScreenSelection()) return;
        Refresh();
    }

    private int ShotLastFrame()
    {
        var shotId = ProductionShotId();
        return !string.IsNullOrWhiteSpace(shotId)
            ? Math.Max(0, ModuleInstanceTimeline.ShotDurationFrames(_database, shotId) - 1)
            : 0;
    }

    private int ActiveShotSlotIndex(string shotId)
    {
        var cursor = 0;
        var slots = _database.GetShotModuleInstanceSlots(shotId);
        for (var index = 0; index < slots.Count; index++)
        {
            cursor += ModuleInstanceTimeline.DurationFrames(_database, slots[index].Id);
            if (_shotPreviewFrame < cursor) return index;
        }
        return slots.Count - 1;
    }

    private void MoveShotSlot(int offset)
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return;
        var slots = _database.GetShotModuleInstanceSlots(shotId);
        var target = ActiveShotSlotIndex(shotId) + offset;
        if (target < 0 || target >= slots.Count) return;
        var start = ModuleInstanceStartFrame(shotId, slots[target].Id);
        StopShotPlayback();
        _shotPreviewFrame = start;
        PlaybackState.NotifyFrameChanged();
        Refresh();
    }

    private IReadOnlyList<int> AnimationKeyframesInCurrentScreen()
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return [];
        var range = ActiveScreenFrameRange(shotId);
        return ModuleInstanceTimeline.ShotKeyframeFrames(_database, shotId)
            .Where((frame) => frame >= range.StartFrame && frame <= range.EndFrame)
            .ToList();
    }

    private void MoveAnimationKeyframe(int direction)
    {
        var keyframes = AnimationKeyframesInCurrentScreen();
        var target = direction < 0
            ? keyframes.LastOrDefault((frame) => frame < _shotPreviewFrame, -1)
            : keyframes.FirstOrDefault((frame) => frame > _shotPreviewFrame, -1);
        if (target < 0) return;
        SetShotPreviewFrame(target, useSelectedScope: false, synchronizeScreenSelection: true);
    }

    private bool SynchronizeExplicitScreenSelection()
    {
        if (IsPreviewPlaybackActive) return false;
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return false;
        var slots = _database.GetShotModuleInstanceSlots(shotId);
        var target = ActiveShotSlotIndex(shotId);
        if (target < 0 || target >= slots.Count) return false;
        if (string.Equals(_selectedNode()?.Id, slots[target].Id, StringComparison.Ordinal)) return false;
        return SelectExplicitScreen(slots[target].Id);
    }

    private bool SelectExplicitScreen(string screenId)
    {
        _pendingExplicitScreenFrame = _shotPreviewFrame;
        if (_selectNodeById(screenId, null)) return true;
        _pendingExplicitScreenFrame = null;
        return false;
    }

    private int ModuleInstanceStartFrame(string shotId, string instanceId)
    {
        var start = 0;
        foreach (var slot in _database.GetShotModuleInstanceSlots(shotId))
        {
            if (slot.Id == instanceId) return start;
            start += ModuleInstanceTimeline.DurationFrames(_database, slot.Id);
        }
        return 0;
    }

    private async void ToggleShotPlayback()
    {
        if (_shotPlaybackTimer.IsEnabled || _shotPlaybackIsPreparing)
        {
            StopShotPlayback();
            return;
        }
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return;
        var shot = new PreviewNodeKey(ProjectTreeNodeKind.Shot, shotId).ToNode();
        var navigationRange = NavigationFrameRange();
        if (_shotPreviewFrame >= navigationRange.EndFrame) _shotPreviewFrame = navigationRange.StartFrame;
        _shotPlaybackIsPreparing = true;
        PlaybackState.SetPlaying(true);
        PlaybackState.SetBusy(true);
        try
        {
            _pendingPlaybackFramesOverride = ShotPlaybackFramePayloads(shot, _shotPreviewFrame, navigationRange.EndFrame).ToList();
            if (!await PreparePlaybackFramesAsync(null))
            {
                PlaybackState.SetPlaying(false);
                return;
            }
        }
        finally
        {
            _pendingPlaybackFramesOverride = null;
            _shotPlaybackIsPreparing = false;
            PlaybackState.SetBusy(false);
        }
        _shotPlaybackStartFrame = _shotPreviewFrame;
        _shotPlaybackStartedTimestamp = Stopwatch.GetTimestamp();
        _shotPlaybackTimer.Start();
        _shotPlayButton.Content = EditorIcons.Create(EditorIcons.Pause, 16);
        OnPlaybackStarted(new ComponentPreviewInputSession.PlaybackRunInfo(
            navigationRange.EndFrame - _shotPlaybackStartFrame + 1,
            Math.Max(1, _database.GetShotSettings(shotId).Fps)));
        AdvanceShotPlayback();
    }

    private IEnumerable<DesignPreviewPayload> ShotPlaybackFramePayloads(ProjectTreeNode shot, int startFrame, int endFrame)
    {
        var lastFrame = Math.Max(startFrame, endFrame);
        for (var frame = startFrame; frame <= lastFrame; frame++)
        {
            var payload = DesignPreviewPayloadFactory.Create(
                _database,
                shot,
                _selectedThemeId,
                _selectedMode,
                frame);
            if (payload is not null)
            {
                yield return ProcessPreviewPayload(payload, "shot-play-sequence", frame) ?? payload;
            }
        }
    }

    private void AdvanceShotPlayback()
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId))
        {
            StopShotPlayback();
            return;
        }
        var elapsed = Stopwatch.GetElapsedTime(_shotPlaybackStartedTimestamp).TotalSeconds;
        var next = _shotPlaybackStartFrame + (int)Math.Floor(elapsed * Math.Max(1, _database.GetShotSettings(shotId).Fps));
        var last = NavigationFrameRange().EndFrame;
        if (next >= last)
        {
            _shotPreviewFrame = last;
            StopShotPlayback();
            Refresh();
            return;
        }
        if (next == _shotPreviewFrame) return;
        _shotPreviewFrame = next;
        PlaybackState.NotifyFrameChanged();
        Refresh();
    }

    private void StopShotPlayback()
    {
        var wasPlaying = _shotPlaybackTimer.IsEnabled;
        if (wasPlaying) _shotPlaybackTimer.Stop();
        _shotPlaybackStartedTimestamp = 0;
        PlaybackState.SetPlaying(false);
        _shotPlayButton.Content = EditorIcons.Create(EditorIcons.Play, 16);
        if (wasPlaying)
        {
            ReleaseFrameCacheReservation();
            OnPlaybackStopped(new ComponentPreviewInputSession.PlaybackRunInfo(
                Math.Max(1, ShotLastFrame() - _shotPlaybackStartFrame + 1),
                Math.Max(1, ProductionShotId() is { Length: > 0 } shotId
                    ? _database.GetShotSettings(shotId).Fps
                    : 25)));
        }
    }

    private bool IsPreviewPlaybackActive =>
        _designInputsPanel.IsPlaybackActive || _shotPlaybackTimer.IsEnabled;

    private int CurrentPlaybackFrameRate() =>
        _shotPlaybackTimer.IsEnabled
        && ProductionShotId() is { Length: > 0 } shotId
            ? _database.GetShotSettings(shotId).Fps
            : _designInputsPanel.PlaybackFrameRate;

    private int CurrentNavigationFrame() =>
        _workspace == EditorWorkspace.Production && !string.IsNullOrWhiteSpace(ProductionShotId())
            ? _shotPreviewFrame
            : _designInputsPanel.CurrentPreviewFrame;

    private string PreviewDeviceId(DesignPreviewPayload? payload)
    {
        return !string.IsNullOrWhiteSpace(payload?.DeviceId)
            ? payload.DeviceId
            : SelectedDeviceId ?? "";
    }

    private void ToggleDesignPreviewContextLock()
    {
        if (_isDesignPreviewContextLocked)
        {
            _isDesignPreviewContextLocked = false;
            _lockedDesignPreviewNode = null;
            UpdateDesignContextChrome(null);
            Refresh();
            return;
        }

        var currentNode = _activeDesignPreviewNode ?? _lastDesignPreviewNode;
        if (currentNode is null)
        {
            UpdateDesignContextChrome(null);
            return;
        }

        _lockedDesignPreviewNode = currentNode;
        _isDesignPreviewContextLocked = true;
        UpdateDesignContextChrome(null);
        Refresh();
    }

    private void UpdateDesignContextChrome(DesignPreviewPayload? payload)
    {
        _activeProductionModuleInstanceId = RuntimeContextValue(payload, "moduleInstanceId");
        _designContextText.Text = payload?.Name ?? "";
        var productionNodes = ProductionNodePath(_selectedNode());
        var previewItems = _workspace == EditorWorkspace.Production
            ? productionNodes.Select((node, index) => new EditorBreadcrumbItem(
                node.Name,
                index == productionNodes.Count - 1 ? null : () => _selectNodeById(node.Id, null)))
            : [new EditorBreadcrumbItem(string.IsNullOrWhiteSpace(payload?.Name) ? "Preview" : payload.Name)];
        EditorBreadcrumbBar.Render(
            _previewTitle,
            previewItems.Any() ? previewItems : [new EditorBreadcrumbItem("Production Preview")]);
        _designContextText.IsVisible = !string.IsNullOrWhiteSpace(_designContextText.Text);
        _designContextText.Foreground = EditorNavigationVisuals.VariantLockBrush(true);
        _designContextText.Opacity = 1;
        var productionContext = !string.IsNullOrWhiteSpace(ProductionShotId());
        if (productionContext)
        {
            _designContextAddHistoryButton.IsVisible = true;
            _designContextLockButton.IsVisible = true;
            RefreshDesignContextHistoryChrome();
        }
        else
        {
            _designContextAddHistoryButton.IsVisible = true;
            _designContextLockButton.IsVisible = true;
            RefreshDesignContextHistoryChrome();
        }
        ToolTip.SetTip(
            _designContextText,
            _activeDesignPreviewNode is not null
                ? productionContext ? "Open the active module instance" : "Open this component variant in the editor"
                : null);

        _designContextLockButton.IsEnabled = _activeDesignPreviewNode is not null
            || _lastDesignPreviewNode is not null
            || _lockedDesignPreviewNode is not null;
        if (_renderedLockState != _isDesignPreviewContextLocked)
        {
            _designContextLockButton.Content = EditorIcons.CreateSemantic(
                _isDesignPreviewContextLocked ? "Release design context" : "Keep current design context",
                _isDesignPreviewContextLocked ? EditorIcons.Lock : EditorIcons.Unlock,
                15);
            _renderedLockState = _isDesignPreviewContextLocked;
        }
        if (_designContextLockButton.Content is Control lockIcon)
        {
            EditorIcons.ApplyBrush(
                lockIcon,
                _isDesignPreviewContextLocked
                    ? EditorNavigationVisuals.VariantLockBrush(true)
                    : null);
        }

        ToolTip.SetTip(
            _designContextLockButton,
            _isDesignPreviewContextLocked
                ? "Release design context"
                : "Keep current design context");
    }

    private void NavigateToActiveDesignContext()
    {
        if (!string.IsNullOrWhiteSpace(_activeProductionModuleInstanceId))
        {
            _selectNodeById(_activeProductionModuleInstanceId, null);
            return;
        }
        var node = _activeDesignPreviewNode ?? _lockedDesignPreviewNode ?? _lastDesignPreviewNode;
        if (node is null)
        {
            return;
        }

        _selectNodeById(node.Id, null);
    }

    private string ProductionShotId()
    {
        if (_workspace != EditorWorkspace.Production) return "";
        return ProductionContextNode() switch
        {
            { Kind: ProjectTreeNodeKind.Shot } shot => shot.Id,
            { Kind: ProjectTreeNodeKind.ModuleInstance } instance => _database.GetModuleInstanceSettings(instance.Id).ShotId,
            _ => "",
        };
    }

    private ProjectTreeNode? ProductionContextNode()
    {
        if (_workspace == EditorWorkspace.Production
            && _isDesignPreviewContextLocked
            && _lockedDesignPreviewNode?.Kind is ProjectTreeNodeKind.Shot or ProjectTreeNodeKind.ModuleInstance)
        {
            return _lockedDesignPreviewNode.ToNode();
        }
        var selected = _selectedNode();
        if (selected?.Kind is ProjectTreeNodeKind.Shot or ProjectTreeNodeKind.ModuleInstance)
        {
            return selected;
        }
        return _lastProductionPreviewNode?.ToNode();
    }

    private ProjectTreeNode? ProductionPayloadNode()
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return null;
        if (_shotNavigationScope == "shot")
        {
            return new PreviewNodeKey(ProjectTreeNodeKind.Shot, shotId).ToNode();
        }
        var slots = _database.GetShotModuleInstanceSlots(shotId);
        var index = ActiveShotSlotIndex(shotId);
        return index >= 0 && index < slots.Count
            ? new PreviewNodeKey(ProjectTreeNodeKind.ModuleInstance, slots[index].Id).ToNode()
            : new PreviewNodeKey(ProjectTreeNodeKind.Shot, shotId).ToNode();
    }

    private static string RuntimeContextValue(DesignPreviewPayload? payload, string key)
    {
        if (payload is null) return "";
        var instance = DesignPreviewTestValues.Parse(payload.InstanceJson);
        return (instance["context"] as JsonObject)?[key]?.GetValue<string>() ?? "";
    }

    private DesignPreviewPayload? ProcessPreviewPayload(
        DesignPreviewPayload? payload,
        string purpose,
        int? productionShotFrame = null)
    {
        if (payload is null) return null;
        if (_workspace == EditorWorkspace.Design)
        {
            _designInputsPanel.UpdateForPayload(payload, _projectId);
            return _designInputsPanel.ApplyInputs(payload, _selectedMode, _projectId);
        }

        _designInputsPanel.UpdateForPayload(null, _projectId);
        LogProductionFrameBoundary("before-runtime", purpose, payload, productionShotFrame);
        var localFrameBefore = ResolvedTimelineFrame(payload);
        var resolved = _productionRuntimeResolver.Resolve(payload, _selectedMode);
        LogProductionFrameBoundary("after-runtime", purpose, resolved, productionShotFrame);
        var localFrameAfter = ResolvedTimelineFrame(resolved);
        if (localFrameAfter != localFrameBefore)
        {
            throw new InvalidOperationException(
                $"Production runtime resolution changed local frame from {localFrameBefore} to {localFrameAfter}.");
        }
        return resolved;
    }

    private void LogProductionFrameBoundary(
        string phase,
        string purpose,
        DesignPreviewPayload payload,
        int? productionShotFrame)
    {
        var screenId = RuntimeContextValue(payload, "moduleInstanceId");
        var localFrame = RuntimeContextNumber(payload, "localFrame");
        var timelineFrame = ResolvedTimelineFrame(payload);
        var startFrame = 0;
        var durationFrames = 0;
        var shotId = ProductionShotId();
        if (!string.IsNullOrWhiteSpace(shotId) && !string.IsNullOrWhiteSpace(screenId))
        {
            foreach (var slot in _database.GetShotModuleInstanceSlots(shotId))
            {
                var duration = ModuleInstanceTimeline.DurationFrames(_database, slot.Id);
                if (slot.Id == screenId)
                {
                    durationFrames = duration;
                    break;
                }
                startFrame += duration;
            }
        }
        PreviewDebugLog.Write(
            "preview.production.frame-boundary",
            ("phase", phase),
            ("purpose", purpose),
            ("shotFrame", productionShotFrame ?? _shotPreviewFrame),
            ("screenId", screenId),
            ("screenStartFrame", startFrame),
            ("screenDurationFrames", durationFrames),
            ("contextLocalFrame", localFrame),
            ("payloadLocalFrame", timelineFrame));
    }

    private static int RuntimeContextNumber(DesignPreviewPayload payload, string key)
    {
        var instance = DesignPreviewTestValues.Parse(payload.InstanceJson);
        return (instance["context"] as JsonObject)?[key]?.GetValue<int>() ?? 0;
    }

    private static int ResolvedTimelineFrame(DesignPreviewPayload payload)
    {
        var preview = DesignPreviewTestValues.Parse(payload.DesignPreviewJson);
        var key = preview["timelineFrameJsonKey"]?.GetValue<string>() ?? "";
        return !string.IsNullOrWhiteSpace(key) && preview[key] is JsonValue value
            && value.TryGetValue<int>(out var frame)
                ? frame
                : 0;
    }

    private void UpdateProductionPreviewSetup()
    {
        var production = _workspace == EditorWorkspace.Production;
        UpdateProductionContextStrip(production);
        if (_deviceComboBox.Parent is Control deviceField) deviceField.IsVisible = !production;
        if (_themeComboBox.Parent is Control themeField) themeField.IsVisible = !production;
        if (_modeComboBox.Parent is Control modeField) modeField.IsVisible = !production;
        if (_previewSetupGrid is { } setupGrid)
        {
            setupGrid.ColumnDefinitions = new ColumnDefinitions(production
                ? "0,0,0,Auto"
                : "*,*,Auto,Auto");
            if (!production && _orientationField is { Parent: Panel currentParent } orientationField)
            {
                if (!ReferenceEquals(currentParent, setupGrid))
                {
                    currentParent.Children.Remove(orientationField);
                    Grid.SetColumn(orientationField, 3);
                    orientationField.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                    setupGrid.Children.Add(orientationField);
                }
            }
        }
        var appearanceMode = production ? ActiveModuleAppearanceMode() : "inherit";
        var forcedMode = appearanceMode is "light" or "dark";
        if (_modeComboBox.ItemsSource is IEnumerable<FieldOption> modeOptions)
        {
            _isRefreshingOptions = true;
            _modeComboBox.SelectedItem = modeOptions.FirstOrDefault((option) =>
                option.Value == (forcedMode ? appearanceMode : _selectedMode));
            _isRefreshingOptions = false;
        }
        _modeComboBox.IsEnabled = !forcedMode;
        ToolTip.SetTip(_modeComboBox, forcedMode ? "Mode is fixed by the active module" : null);
    }

    private void UpdateProductionContextStrip(bool production)
    {
        _productionContextHost.IsVisible = production;
        _productionContextHost.Children.Clear();
        if (!production) return;

        var selected = _selectedNode();
        var pathNodes = ProductionNodePath(selected);
        var path = pathNodes.Select((node, index) => new ProductionPreviewPathItem(
            node.Name,
            index == pathNodes.Count - 1 ? null : () => _selectNodeById(node.Id, null))).ToList();
        var shotId = ProductionShotId();
        string actorName;
        string device;
        string theme;
        string mode;
        if (string.IsNullOrWhiteSpace(shotId))
        {
            actorName = "No Shot selected";
            device = "No Shot selected";
            theme = "No Shot selected";
            mode = "Inherited";
        }
        else
        {
            var inherited = _productionShotContext.Resolve(shotId);
            actorName = inherited.Actor;
            device = inherited.Device;
            theme = inherited.Theme;
            mode = ActiveModuleAppearanceMode();
            if (mode == "inherit")
            {
                mode = inherited.ThemeMode;
            }
            mode = EditorUiText.IdentifierLabel(mode);
        }
        Control? orientation = null;
        if (_orientationField is { } orientationField)
        {
            if (orientationField.Parent is Panel parent) parent.Children.Remove(orientationField);
            orientation = orientationField;
        }
        ProductionPreviewContextStrip.Render(
            _productionContextHost,
            new ProductionPreviewContextMetadata(path, actorName, device, theme, mode),
            orientation);
    }

    private static IReadOnlyList<ProjectTreeNode> ProductionNodePath(ProjectTreeNode? selected)
    {
        if (selected is null) return [];
        var nodes = new List<ProjectTreeNode>();
        var current = selected;
        while (current is not null)
        {
            if (current.Kind is ProjectTreeNodeKind.Episode or ProjectTreeNodeKind.Shot or ProjectTreeNodeKind.ModuleInstance)
            {
                nodes.Add(current);
            }
            current = current.Parent;
        }
        nodes.Reverse();
        return nodes;
    }

    private PreviewContextState? InvalidProductionContext()
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return null;
        var context = _productionShotContext.Resolve(shotId);
        return context.IsValid
            ? null
            : new PreviewContextState(
                PreviewContextStateKind.Error,
                "Shot context is incomplete",
                context.Error);
    }


    private string ActiveModuleAppearanceMode()
    {
        var instanceId = _selectedNode() is { Kind: ProjectTreeNodeKind.ModuleInstance } selectedInstance
            ? selectedInstance.Id
            : _activeProductionModuleInstanceId;
        if (string.IsNullOrWhiteSpace(instanceId) && ProductionShotId() is { Length: > 0 } shotId)
        {
            var frame = 0;
            foreach (var slot in _database.GetShotModuleInstanceSlots(shotId))
            {
                var duration = ModuleInstanceTimeline.DurationFrames(_database, slot.Id);
                if (_shotPreviewFrame < frame + duration)
                {
                    instanceId = slot.Id;
                    break;
                }
                frame += duration;
            }
        }
        if (string.IsNullOrWhiteSpace(instanceId)) return "inherit";
        var instance = _database.GetModuleInstanceSettings(instanceId);
        var config = DesignPreviewTestValues.Parse(_database.GetModuleSettings(instance.ModuleId).ConfigJson);
        return config["appearanceMode"]?.GetValue<string>() ?? "inherit";
    }

    private void EnsureSelectedOptionsExist()
    {
        if (string.IsNullOrWhiteSpace(_projectId) || _isRefreshingOptions)
        {
            return;
        }

        var deviceOptions = _database.GetDeviceOptions(_projectId);
        var selectedDevice = !string.IsNullOrWhiteSpace(SelectedDeviceId)
            ? deviceOptions.FirstOrDefault((option) => option.Value == SelectedDeviceId)
            : null;
        selectedDevice ??= deviceOptions.FirstOrDefault();

        var themeOptions = _database.GetThemeOptions(_projectId);
        var selectedTheme = !string.IsNullOrWhiteSpace(_selectedThemeId)
            ? themeOptions.FirstOrDefault((option) => option.Value == _selectedThemeId)
            : null;
        selectedTheme ??= themeOptions.FirstOrDefault();

        _isRefreshingOptions = true;
        try
        {
            _deviceComboBox.ItemsSource = deviceOptions;
            _deviceComboBox.SelectedItem = selectedDevice;
            SelectedDeviceId = selectedDevice?.Value;

            _themeComboBox.ItemsSource = themeOptions;
            _themeComboBox.SelectedItem = selectedTheme;
            _selectedThemeId = selectedTheme?.Value;
        }
        finally
        {
            _isRefreshingOptions = false;
        }
    }

    private sealed record PreviewNodeKey(ProjectTreeNodeKind Kind, string Id)
    {
        public static PreviewNodeKey From(ProjectTreeNode node)
        {
            return new PreviewNodeKey(node.Kind, node.Id);
        }

        public ProjectTreeNode ToNode()
        {
            return new ProjectTreeNode(Kind, Id, "", "", "");
        }
    }

    private sealed record DesignPreviewHistoryEntry(
        PreviewNodeKey Key,
        string Name,
        EditorViewState? ViewState);
}
