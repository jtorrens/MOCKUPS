using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class PreviewOptionsPreparation : IDisposable
{
    internal PreviewOptionsPreparation(
        CancellationTokenSource ownerOperation,
        CancellationTokenSource linkedOperation,
        PreviewVisualContextSnapshot visual,
        ProductionPreviewSessionSnapshot production,
        PreviewVisualContextSnapshot? previousVisual,
        ProductionPreviewSessionSnapshot? previousProduction)
    {
        OwnerOperation = ownerOperation;
        LinkedOperation = linkedOperation;
        Visual = visual;
        Production = production;
        PreviousVisual = previousVisual;
        PreviousProduction = previousProduction;
    }

    internal CancellationTokenSource OwnerOperation { get; }
    internal CancellationTokenSource LinkedOperation { get; }
    internal PreviewVisualContextSnapshot Visual { get; }
    internal ProductionPreviewSessionSnapshot Production { get; }
    internal PreviewVisualContextSnapshot? PreviousVisual { get; }
    internal ProductionPreviewSessionSnapshot? PreviousProduction { get; }
    public void Dispose() => LinkedOperation.Dispose();
}

internal sealed class EditorPreviewController : IDisposable
{
    private const int LoadingPreviewFrameThreshold = 0;
    private const int InitialPlaybackPreloadFrames = 32;
    private const int AheadPlaybackPreloadFrames = 16;
    private static readonly IBrush PreviewStatusIdleBrush = Brushes.Transparent;
    private static readonly IBrush PreviewStatusIdleBorder = new SolidColorBrush(Color.FromArgb(150, 210, 220, 232));
    private static readonly IBrush PreviewStatusLoadingBrush = new SolidColorBrush(Color.Parse("#2F80ED"));
    private static readonly IBrush PreviewStatusGoodBrush = new SolidColorBrush(Color.Parse("#2ECC71"));
    private static readonly IBrush PreviewStatusSlowBrush = new SolidColorBrush(Color.Parse("#E74C3C"));
    private readonly DesignPreviewPayloadDataSource _previewPayloadData;
    private readonly PreviewVisualContextDataSource _visualContextData;
    private readonly EditorOperationCoordinator _operations;
    private readonly ProductionPreviewSessionDataSource _productionPreviewData;
    private readonly IProductionRecordFieldStore _productionRecordFields;
    private readonly Window _owner;
    private readonly Control _previewPanel;
    private readonly EditorInstantComboBox _deviceComboBox;
    private readonly EditorInstantComboBox _themeComboBox;
    private readonly EditorInstantComboBox _modeComboBox;
    private readonly EditorInstantComboBox _orientationComboBox;
    private readonly EditorInstantComboBox _scaleComboBox = new()
    {
        MinWidth = 96,
        MaxWidth = 112,
        MinHeight = 36,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly EditorInstantComboBox _playbackRouteComboBox = new()
    {
        MinWidth = 160,
        MaxWidth = 190,
        MinHeight = 36,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly EditorCompactToggleSwitch _marksToggle = new()
    {
        IsChecked = false,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly EditorCompactToggleSwitch _canonicalFrameToggle = new()
    {
        IsChecked = false,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly EditorCompactToggleSwitch _transparencyGridToggle = new()
    {
        IsChecked = false,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly EditorCompactToggleSwitch _alphaOnlyToggle = new()
    {
        IsChecked = false,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly Button _referenceButton = new()
    {
        Content = "Reference",
        MinHeight = 32,
    };
    private readonly Button _shotReferenceVideoButton = new()
    {
        Content = EditorIcons.Create(EditorIcons.Video, 17),
        Width = 34,
        Height = 30,
        Padding = new Thickness(0),
        IsVisible = false,
    };
    private readonly Canvas _shotReferenceMarkerOverlay = new()
    {
        Height = 14,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        IsHitTestVisible = false,
    };
    private readonly EditorInstantComboBox _referenceViewComboBox = new()
    {
        MinWidth = 88,
        MaxWidth = 120,
        MinHeight = 32,
    };
    private readonly Slider _referenceSwipeSlider = EditorSliderBehavior.Configure(new Slider { Minimum = 0, Maximum = 1, Value = 0.5, MinWidth = 72, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch });
    private readonly Slider _referenceOpacitySlider = EditorSliderBehavior.Configure(new Slider { Minimum = 0, Maximum = 1, Value = 1, MinWidth = 72, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch });
    private readonly Slider _referenceAngleSlider = EditorSliderBehavior.Configure(new Slider { Minimum = -45, Maximum = 45, Value = 0, MinWidth = 72, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch });
    private readonly StackPanel _referenceSplitControls = new() { Spacing = 8, IsVisible = false };
    private readonly StackPanel _shotTimelineControls = new()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal,
        Spacing = 7,
        IsVisible = false,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly Grid _shotTimelineSliderRow = new()
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        ColumnSpacing = 10,
        IsVisible = false,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly Slider _shotFrameSlider = EditorSliderBehavior.Configure(new Slider
    {
        Minimum = 0,
        Maximum = 0,
        Value = 0,
        TickFrequency = 1,
        MinWidth = 0,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
    });
    private readonly TextBlock _shotFrameText = new()
    {
        MinWidth = 70,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly Button _shotPreviousSlotButton = new() { Content = EditorIcons.Create(EditorIcons.TimelinePreviousInstance, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotPreviousKeyframeButton = new() { Content = EditorTimelineTransport.CreateKeyframeStepIcon(next: false), Width = 38, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotAbsoluteStartButton = new() { Content = EditorIcons.Create(EditorIcons.TimelineShotStart, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotPreviousFrameButton = new() { Content = EditorIcons.Create(EditorIcons.TimelinePreviousFrame, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotPlayButton = new() { Content = ShotPlaybackIcon(isPlaying: false), Width = 42, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotNextFrameButton = new() { Content = EditorIcons.Create(EditorIcons.TimelineNextFrame, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotNextKeyframeButton = new() { Content = EditorTimelineTransport.CreateKeyframeStepIcon(next: true), Width = 38, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotNextSlotButton = new() { Content = EditorIcons.Create(EditorIcons.TimelineNextInstance, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly Button _shotAbsoluteEndButton = new() { Content = EditorIcons.Create(EditorIcons.TimelineShotEnd, 16), Width = 34, Height = 30, Padding = new Thickness(0) };
    private readonly DispatcherTimer _shotPlaybackTimer = new() { Interval = TimeSpan.FromMilliseconds(20) };
    private readonly IEditorShellMessageSink _messages;
    private readonly Func<bool> _isDark;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly Func<string, bool> _selectNodeById;
    private readonly Func<EditorWorkspace, string, Task<bool>>
        _navigateNodeInWorkspace;
    private readonly TextBlock _designContextText;
    private readonly Button _designContextHistoryButton;
    private readonly Button _designContextAddHistoryButton;
    private readonly Button _designContextLockButton;
    private readonly Panel _previewTitle;
    private readonly Popup _designContextHistoryPopup;
    private readonly StackPanel _designContextHistoryItems = new() { Spacing = 1 };
    private readonly DesignWebPreviewPane _designPreviewPane;
    private readonly IProjectPathResolver _projectPaths;
    private readonly ComponentPreviewInputSession _designInputsPanel;
    private readonly ContentControl _previewBusyHost;
    private readonly StackPanel _productionContextHost = new()
    {
        Spacing = 7,
        IsVisible = false,
    };
    private Border? _previewSetupBorder;
    private Grid? _previewSetupGrid;
    private Grid? _previewPrimaryControls;
    private Control? _deviceField;
    private Control? _themeField;
    private Control? _modeField;
    private Panel? _orientationField;
    private PreviewSetupLayoutMode? _previewSetupLayoutMode;
    private readonly EditorLoadingScrim _previewLoadingScrim = new();
    private readonly ProductionPreviewRuntimeResolver _productionRuntimeResolver;
    private readonly ProductionPreviewPayloadPreparer
        _productionPayloadPreparer;
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
        var screen =
            PreparedProductionSession()
                .Screen(moduleInstanceId);
        return Math.Clamp(
            _shotPreviewFrame - screen.StartFrame,
            0,
            screen.DurationFrames - 1);
    }

    public void SetProductionScreenFrame(string moduleInstanceId, int localFrame)
    {
        var screen =
            PreparedProductionSession()
                .Screen(moduleInstanceId);
        SetShotPreviewFrame(
            screen.StartFrame
            + Math.Clamp(
                localFrame,
                0,
                screen.DurationFrames - 1));
    }

    public int ProductionScreenTimelineFrame(string moduleInstanceId)
    {
        var screen = PreparedProductionSession().Screen(moduleInstanceId);
        return _shotPreviewFrame
            - screen.StartFrame
            - screen.TransitionFrameCount
            - screen.ActionDelayFrames;
    }

    public PreviewScreenTimelineRange ProductionScreenTimelineRange(
        string moduleInstanceId)
    {
        var screen = PreparedProductionSession().Screen(moduleInstanceId);
        return new PreviewScreenTimelineRange(
            screen.TransitionFrameCount + screen.ActionDelayFrames,
            screen.ActionDurationFrames,
            ScreenPostRollFrames(moduleInstanceId));
    }

    public void SetProductionScreenTimelineFrame(
        string moduleInstanceId,
        int localFrame)
    {
        var screen = PreparedProductionSession().Screen(moduleInstanceId);
        var minimum = -screen.TransitionFrameCount - screen.ActionDelayFrames;
        var maximum = screen.ActionDurationFrames
            + ScreenPostRollFrames(moduleInstanceId) - 1;
        SetShotPreviewFrame(
            screen.StartFrame
            + screen.TransitionFrameCount
            + screen.ActionDelayFrames
            + Math.Clamp(localFrame, minimum, maximum));
    }

    public int ProductionShotFrame() => _shotPreviewFrame;

    public void SetProductionShotFrame(int frame) => SetShotPreviewFrame(frame);

    public ProductionPreviewShotSnapshot ProductionShotTimelineSnapshot(
        string shotId) =>
        PreparedProductionSession().Shot(shotId);

    public Task UpdateProductionShotScreenTimelineAsync(
        string screenId,
        string fieldId,
        int value)
    {
        if (fieldId is not "moduleInstance.startFrame"
            and not "moduleInstance.durationFrames")
        {
            throw new InvalidOperationException(
                $"Shot Timeline cannot edit Screen field '{fieldId}'.");
        }
        return _operations.ExecuteAsync(
            () => _productionRecordFields.UpdateModuleInstanceField(
                screenId,
                fieldId,
                value.ToString(CultureInfo.InvariantCulture)));
    }

    public EditorWorkspace PreviewAuthoringWorkspace =>
        PreviewWorkspace();

    public ProjectTreeNode PreviewAuthoringNode(
        ProjectTreeNode selectedNode,
        IReadOnlyList<ProjectTreeNode> treeRoots)
    {
        if (_lockedPreviewContext is not { } locked)
        {
            return selectedNode;
        }

        var resolved = EditorNodeSelectionState.FindNodeById(
            treeRoots,
            locked.Node.Id);
        if (resolved is null || resolved.Kind != locked.Node.Kind)
        {
            throw new InvalidOperationException(
                $"Locked Preview context '{locked.Node.Kind}:{locked.Node.Id}' "
                + "is not present in the current project tree.");
        }

        return resolved;
    }

    public IReadOnlyList<PreviewScreenTimelineReferenceMarker>
        ProductionScreenReferenceMarkers(string moduleInstanceId)
    {
        var session = PreparedProductionSession();
        var screen = session.Screen(moduleInstanceId);
        var shot = session.Shot(screen.ShotId);
        if (shot.ReferenceVideo.InFrame is not { } inFrame) return [];
        var actionOrigin = screen.StartFrame
            + screen.TransitionFrameCount
            + screen.ActionDelayFrames;
        return shot.ReferenceVideo.Markers
            .Select((marker) => new PreviewScreenTimelineReferenceMarker(
                marker.Id,
                marker.VideoFrame
                    - inFrame
                    - actionOrigin,
                marker.Text))
            .Where((marker) =>
                marker.Frame >= -screen.TransitionFrameCount - screen.ActionDelayFrames
                && marker.Frame < screen.ActionDurationFrames
                    + ScreenPostRollFrames(moduleInstanceId))
            .ToArray();
    }

    public string ActiveNavigationNodeId
    {
        get
        {
            var shotId = ProductionShotId();
            return string.IsNullOrWhiteSpace(shotId)
                ? ""
                : ProductionScreenPlaybackState.ActiveScreenId(
                    PreparedProductionSession()
                        .Shot(shotId)
                        .FrameRanges,
                    _shotPreviewFrame);
        }
    }

    public void ToggleProductionPlayback() => ToggleShotPlayback();
    private PreviewNodeKey? _lastDesignPreviewNode;
    private PreviewNodeKey? _lastProductionPreviewNode;
    private PreviewNodeKey? _activeDesignPreviewNode;
    private PreviewContextLock? _lockedPreviewContext;
    private DesignPreviewHistoryEntry?
        _activeProductionHistoryEntry;
    private readonly List<DesignPreviewHistoryEntry> _designHistory = [];
    private readonly List<DesignPreviewHistoryEntry> _productionHistory = [];
    private EditorWorkspace _workspace = EditorWorkspace.Design;
    private string _selectedMode = "light";
    private string _selectedOrientation = "portrait";
    private string _selectedScale = "fit";
    private string _selectedPlaybackRoute = "html-all";
    private bool _showDesignMarks;
    private bool _showCanonicalFrame;
    private bool _showTransparencyGrid;
    private bool _showAlphaOnly;
    private string _referenceSource = "";
    private string _referenceViewMode = "preview";
    private int _referenceStartPreviewFrame;
    private bool _isRefreshingOptions;
    private bool? _renderedLockState;
    private string _activePreviewContextName = "";
    private readonly PreviewPreparationCancellation _designPlaybackPreparation = new();
    private readonly PreviewPreparationCancellation
        _visualContextPreparation = new();
    private readonly PreviewPreparationCancellation
        _productionPayloadPreparation = new();
    private CancellationTokenSource? _aheadPreloadCancellation;
    private readonly HashSet<string> _aheadPreloadedFrameKeys = new(StringComparer.Ordinal);
    private bool _isAheadPreloading;
    private readonly Dictionary<string, string> _rasterPlaybackFrames = new(StringComparer.Ordinal);
    private readonly List<string> _rasterPlaybackOrder = [];
    private string _rasterPlaybackSignature = "";
    private readonly ChromiumPreviewRasterizer _chromiumRasterizer = new();
    private string _rasterCacheDirectory = "";
    private PlaybackPerformanceRun? _playbackPerformanceRun;
    private readonly Dictionary<PlaybackFrameCacheOwner, IDisposable>
        _frameCacheReservations = [];
    private PreparedDesignPlayback? _preparedDesignPlayback;
    private int _playbackSummaryGeneration;
    private int _shotPreviewFrame;
    private string _shotTimelineShotId = "";
    private string _shotTimelineContextNodeId = "";
    private bool _isUpdatingShotTimeline;
    private long _selectionRefreshGeneration;
    private readonly PreviewPreparationCancellation _shotPlaybackPreparation = new();
    private PreparedProductionPlayback? _preparedShotPlayback;
    private long _shotPlaybackStartedTimestamp;
    private int _shotPlaybackStartFrame;
    private bool _shotPlaybackIsPreparing;
    private IReadOnlyList<DesignPreviewPayload>? _pendingPlaybackFramesOverride;
    private string _activeProductionModuleInstanceId = "";
    private bool _disposed;
    private PreviewVisualContextSnapshot?
        _visualContextSnapshot;
    private ProductionPreviewSessionSnapshot?
        _productionSessionSnapshot;
    private readonly ShotReferenceVideoController
        _referenceVideoController;
    private Func<int, bool>? _stepScreenTimelineFrame;
    private Func<int, bool>? _moveToScreenTimelineNavigationFrame;

    public EditorPreviewController(
        IPreviewInputRepository preview,
        IComponentPreviewInputRepository componentPreview,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes,
        IDictionaryFieldContextRepository dictionary,
        IActorPreviewRepository actors,
        IProductionRecordFieldStore productionRecordFields,
        IProjectPathResolver projectPaths,
        EditorOperationCoordinator operations,
        EditorInstantComboBox deviceComboBox,
        EditorInstantComboBox themeComboBox,
        EditorInstantComboBox modeComboBox,
        EditorInstantComboBox orientationComboBox,
        IEditorShellMessageSink messages,
        ContentControl previewSetupHost,
        ContentControl previewCombinedControlsHost,
        ContentControl previewBusyHost,
        ContentControl designPreviewHost,
        TextBlock designContextText,
        Button designContextHistoryButton,
        Button designContextAddHistoryButton,
        Button designContextLockButton,
        Panel previewTitle,
        Func<bool> isDark,
        Func<ProjectTreeNode?> selectedNode,
        Func<string, bool> selectNodeById,
        Func<EditorWorkspace, string, Task<bool>> navigateNodeInWorkspace,
        Action<PreviewAuthoringNavigationTarget> navigateAuthoringTarget,
        Control previewPanel,
        Window owner)
    {
        _projectPaths = projectPaths;
        _productionRecordFields = productionRecordFields;
        _operations = operations;
        _designPreviewPane = new DesignWebPreviewPane(projectPaths);
        _previewPayloadData = new DesignPreviewPayloadDataSource(
            preview,
            timeline,
            moduleInstanceThemes,
            actors,
            projectPaths);
        _visualContextData =
            new PreviewVisualContextDataSource(preview, actors);
        _productionPreviewData =
            new ProductionPreviewSessionDataSource(
                preview,
                timeline,
                moduleInstanceThemes,
                actors);
        _owner = owner;
        _previewPanel = previewPanel;
        _referenceVideoController = new ShotReferenceVideoController(
            owner,
            projectPaths,
            messages,
            CommitReferenceVideoAsync,
            SetProductionShotFrame,
            ToggleProductionPlayback);
        _deviceComboBox = deviceComboBox;
        _themeComboBox = themeComboBox;
        _modeComboBox = modeComboBox;
        _orientationComboBox = orientationComboBox;
        _messages = messages;
        _isDark = isDark;
        _selectedNode = selectedNode;
        _selectNodeById = selectNodeById;
        _navigateNodeInWorkspace = navigateNodeInWorkspace;
        _designContextText = designContextText;
        _designContextHistoryButton = designContextHistoryButton;
        _designContextAddHistoryButton = designContextAddHistoryButton;
        _designContextLockButton = designContextLockButton;
        _previewTitle = previewTitle;
        _designContextHistoryPopup = CreateDesignContextHistoryPopup();
        _previewBusyHost = previewBusyHost;
        _productionRuntimeResolver = new ProductionPreviewRuntimeResolver(
            actors,
            projectPaths);
        _productionPayloadPreparer =
            new ProductionPreviewPayloadPreparer(
                _previewPayloadData,
                _productionRuntimeResolver);
        _previewBusyHost.Content = _previewLoadingScrim;
        _previewBusyHost.IsVisible = false;
        _designInputsPanel = new ComponentPreviewInputSession(
            componentPreview,
            dictionary,
            actors,
            projectPaths,
            Refresh,
            PreparePlaybackFramesAsync);
        _designPreviewPane.FrameStatusChanged += OnDesignPreviewFrameStatusChanged;
        _designPreviewPane.ContextActionRequested += targetId =>
        {
            if (targetId == PreviewRetryTargetId) Refresh();
            else _selectNodeById(targetId);
        };
        _designPreviewPane.AuthoringTargetRequested += navigateAuthoringTarget;
        _designInputsPanel.PlaybackStarted += OnPlaybackStarted;
        _designInputsPanel.PlaybackStopped += OnPlaybackStopped;
        _designInputsPanel.PlaybackBusyChanged += PlaybackState.SetBusy;
        _shotPlaybackTimer.Tick += (_, _) => AdvanceShotPlayback();
        PlaybackState.Changed += SyncReferenceVideo;
        PlaybackState.Changed += RefreshShotPlaybackButton;
        RefreshShotPlaybackButton();

        _designContextHistoryButton.Content = EditorIcons.CreateSemantic("Recent design contexts", EditorIcons.Collapse, 15);
        _designContextAddHistoryButton.Content = EditorIcons.Create(EditorIcons.Add, 15);

        WrapPreviewSetup(previewSetupHost);
        previewCombinedControlsHost.Content = CreatePreviewControls();
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shotPlaybackTimer.Stop();
        _visualContextPreparation.Dispose();
        _productionPayloadPreparation.Dispose();
        _designPlaybackPreparation.Dispose();
        _shotPlaybackPreparation.Dispose();
        _aheadPreloadCancellation?.Cancel();
        _aheadPreloadCancellation?.Dispose();
        _aheadPreloadCancellation = null;
        _aheadPreloadedFrameKeys.Clear();
        ReleaseFrameCacheReservations();
        _chromiumRasterizer.Dispose();
        WebDesignPreviewRenderer.Shutdown();
        PlaybackState.Changed -= SyncReferenceVideo;
        PlaybackState.Changed -= RefreshShotPlaybackButton;
        _owner.RemoveHandler(InputElement.KeyDownEvent, OnOwnerKeyDown);
        _previewPanel.RemoveHandler(
            InputElement.KeyDownEvent,
            OnPreviewPanelKeyDown);
        _referenceVideoController.Dispose();
    }

    public void CancelPreparationsForApplicationClose()
    {
        CancelPlaybackPreparation();
        _visualContextPreparation.Cancel();
        _productionPayloadPreparation.Cancel();
        _aheadPreloadCancellation?.Cancel();
    }

    public void ConfigureScreenTimelineKeyboardNavigation(
        Func<int, bool> stepFrame,
        Func<int, bool> moveToNavigationFrame)
    {
        _stepScreenTimelineFrame = stepFrame;
        _moveToScreenTimelineNavigationFrame = moveToNavigationFrame;
    }

    private void AddCurrentDesignContextToHistory()
    {
        if (PreviewWorkspace() == EditorWorkspace.Production)
        {
            AddCurrentProductionContextToHistory();
            return;
        }
        var key = _activeDesignPreviewNode
            ?? LockedNode(EditorWorkspace.Design)
            ?? _lastDesignPreviewNode;
        if (key is null)
        {
            return;
        }

        var payload = DesignPreviewPayloadFactory.Create(_previewPayloadData, key.ToNode(), _selectedThemeId, _selectedMode, _shotPreviewFrame);
        if (payload is null)
        {
            return;
        }

        AddDesignHistory(key, payload.Name);
        RefreshDesignContextHistoryChrome();
    }

    private void AddCurrentProductionContextToHistory()
    {
        var node = ProductionContextNode();
        if (node is null) return;
        var key = PreviewNodeKey.From(node);
        if (_activeProductionHistoryEntry
                is not { } active
            || active.Key != key)
        {
            return;
        }
        AddHistory(
            _productionHistory,
            key,
            active.Name);
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
        if (PreviewWorkspace() == EditorWorkspace.Production)
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
                : $"Screen · {PreparedProductionSession().Screen(entry.Key.Id).ShotId}";
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
                _selectNodeById(entry.Key.Id);
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
                _selectNodeById(entry.Key.Id);
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

    private void AddDesignHistory(PreviewNodeKey key, string name)
    {
        AddHistory(_designHistory, key, name);
    }

    private static void AddHistory(
        List<DesignPreviewHistoryEntry> history,
        PreviewNodeKey key,
        string name)
    {
        history.RemoveAll((entry) => entry.Key.Id.Equals(key.Id, StringComparison.Ordinal));
        history.Insert(0, new DesignPreviewHistoryEntry(key, name));
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
                string.IsNullOrWhiteSpace(entry.Name) ? entry.Id : entry.Name));
        }
        RefreshDesignContextHistoryChrome();
    }

    public IReadOnlyList<EditorDesignPreviewHistoryEntryState> ExportProductionHistoryState() =>
        _productionHistory.Select((entry) => new EditorDesignPreviewHistoryEntryState
        {
            Kind = entry.Key.Kind,
            Id = entry.Key.Id,
            Name = entry.Name,
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
                string.IsNullOrWhiteSpace(entry.Name) ? entry.Id : entry.Name));
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
        var previewWorkspace = PreviewWorkspace();
        var history = previewWorkspace == EditorWorkspace.Production ? _productionHistory : _designHistory;
        _designContextHistoryButton.Content = EditorIcons.CreateSemantic(
            previewWorkspace == EditorWorkspace.Production ? "Recent production contexts" : "Recent design contexts",
            EditorIcons.Collapse,
            15);
        _designContextHistoryButton.IsEnabled = history.Count > 0;
        _designContextHistoryButton.Opacity = history.Count > 0 ? 1 : 0.38;
        var canAddCurrentContext = _activeDesignPreviewNode is not null
            || LockedNode(EditorWorkspace.Design) is not null
            || _lastDesignPreviewNode is not null
            || (previewWorkspace == EditorWorkspace.Production && ProductionContextNode() is not null);
        _designContextAddHistoryButton.IsEnabled = canAddCurrentContext;
        _designContextAddHistoryButton.Opacity = canAddCurrentContext ? 1 : 0.38;
        ToolTip.SetTip(
            _designContextHistoryButton,
            history.Count > 0
                ? previewWorkspace == EditorWorkspace.Production ? "Recent production contexts" : "Recent design contexts"
                : previewWorkspace == EditorWorkspace.Production ? "No recent production contexts" : "No recent design contexts");
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
        ToolTip.SetTip(_previewPerformanceDot, "Preview FPS and rendering status");
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Children =
            {
                new StackPanel
                {
                    Spacing = 0,
                    Children =
                    {
                        _productionContextHost,
                        setupContent,
                    },
                },
            },
        };
        Grid.SetColumn(_previewPerformanceDot, 1);
        content.Children.Add(_previewPerformanceDot);
        _previewSetupBorder = new Border
        {
            Padding = new Thickness(12),
            Child = content,
        };
        previewSetupHost.Content = _previewSetupBorder;
        _previewSetupGrid = _deviceComboBox.Parent?.Parent as Grid;
        _deviceField = _deviceComboBox.Parent as Control;
        _themeField = _themeComboBox.Parent as Control;
        _modeField = _modeComboBox.Parent as Control;
        _orientationField = _orientationComboBox.Parent as Panel;
        if (_previewSetupGrid is { } setupGrid)
        {
            setupGrid.SizeChanged += (_, args) => ApplyPreviewSetupLayout(args.NewSize.Width);
            ApplyPreviewSetupLayout(setupGrid.Bounds.Width);
        }
    }

    private Control CreatePreviewControls()
    {
        ToolTip.SetTip(_marksToggle, "Show design markers");
        ToolTip.SetTip(_canonicalFrameToggle, "Show canonical 360 × 800 frame without the device layer");
        ToolTip.SetTip(_transparencyGridToggle, "Alternate the transparency matte between black and a gray checkerboard");
        ToolTip.SetTip(_alphaOnlyToggle, "Show the final alpha channel as white over black");

        var primaryControls = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
            ColumnSpacing = 10,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        var primaryControlItems = new Control[]
        {
            _scaleComboBox,
            _playbackRouteComboBox,
            LabeledToggle("Markers", _marksToggle),
            LabeledToggle("360", _canonicalFrameToggle),
            LabeledToggle("Grid", _transparencyGridToggle),
            LabeledToggle("Alpha", _alphaOnlyToggle),
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
        EditorAccessibility.Describe(
            _shotFrameSlider,
            "Navigate preview frames",
            "Navigate the shared Shot playhead used by Preview and Animation");
        var shotSliderHost = new Grid
        {
            Children =
            {
                _shotFrameSlider,
                _shotReferenceMarkerOverlay,
            },
        };
        shotSliderHost.SizeChanged += (_, _) => RefreshShotReferenceMarkers();
        _shotTimelineSliderRow.Children.Add(shotSliderHost);
        Grid.SetColumn(_shotFrameText, 1);
        _shotTimelineSliderRow.Children.Add(_shotFrameText);
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
        _shotTimelineControls.Children.Add(TimelineSeparator(30));
        _shotTimelineControls.Children.Add(_shotReferenceVideoButton);
        EditorAccessibility.Describe(_shotPreviousKeyframeButton, "Previous animation keyframe in the current Screen");
        EditorAccessibility.Describe(_shotPreviousSlotButton, "Previous Screen");
        EditorAccessibility.Describe(_shotAbsoluteStartButton, "First Shot frame");
        EditorAccessibility.Describe(_shotPreviousFrameButton, "Previous frame");
        EditorAccessibility.Describe(_shotPlayButton, "Play or pause the shared Shot timeline");
        EditorAccessibility.Describe(_shotNextFrameButton, "Next frame");
        EditorAccessibility.Describe(_shotNextKeyframeButton, "Next animation keyframe in the current Screen");
        EditorAccessibility.Describe(_shotNextSlotButton, "Next Screen");
        EditorAccessibility.Describe(_shotAbsoluteEndButton, "Last Shot frame");
        EditorAccessibility.Describe(
            _shotReferenceVideoButton,
            "Show or hide the Shot reference video");
        _shotAbsoluteStartButton.Click += (_, _) => SetShotPreviewFrame(0);
        _shotPreviousSlotButton.Click += (_, _) => MoveShotSlot(-1);
        _shotPreviousKeyframeButton.Click += (_, _) => MoveAnimationKeyframe(-1);
        _shotPreviousFrameButton.Click += (_, _) => SetShotPreviewFrame(_shotPreviewFrame - 1);
        _shotPlayButton.Click += (_, _) => ToggleShotPlayback();
        _shotNextFrameButton.Click += (_, _) => SetShotPreviewFrame(_shotPreviewFrame + 1);
        _shotNextKeyframeButton.Click += (_, _) => MoveAnimationKeyframe(1);
        _shotNextSlotButton.Click += (_, _) => MoveShotSlot(1);
        _shotAbsoluteEndButton.Click += (_, _) => SetShotPreviewFrame(ShotLastFrame());
        _shotReferenceVideoButton.Click += (_, _) =>
            _referenceVideoController.Toggle();

        var controlsRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 8,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        _previewPrimaryControls = primaryControls;
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
        bool? primaryHasOrientation = null;
        void ArrangeTransport(double availableWidth)
        {
            var visiblePrimaryControls = primaryControls.Children
                .OfType<Control>()
                .Where((control) => control.IsVisible)
                .ToList();
            var primaryNaturalWidth = visiblePrimaryControls.Sum((control) => control.DesiredSize.Width)
                + (primaryControls.ColumnSpacing * Math.Max(0, visiblePrimaryControls.Count - 1));
            var requiredWidth = primaryNaturalWidth
                + _shotTimelineControls.DesiredSize.Width
                + (controlsRow.ColumnSpacing * 2);
            var wraps = availableWidth > 0 && availableWidth + 1 < requiredWidth;
            var productionOrientation =
                ReferenceEquals(_orientationComboBox.Parent, primaryControls);
            if (transportWraps == wraps
                && primaryHasOrientation == productionOrientation)
            {
                return;
            }
            transportWraps = wraps;
            primaryHasOrientation = productionOrientation;
            _scaleComboBox.MinWidth = wraps ? 0 : 96;
            _playbackRouteComboBox.MinWidth = wraps ? 0 : 160;
            _referenceViewComboBox.MinWidth = wraps ? 0 : 88;
            _orientationComboBox.MinWidth = productionOrientation
                ? wraps ? 0 : 96
                : 0;
            primaryControls.ColumnDefinitions = productionOrientation
                ? wraps
                    ? new ColumnDefinitions("*,1.6*,Auto,Auto,Auto,Auto,*,*")
                    : new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto")
                : wraps
                    ? new ColumnDefinitions("*,2*,Auto,Auto,Auto,Auto,*")
                    : new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto");
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

        var timelineAndReference = new StackPanel
        {
            Spacing = 0,
            Children =
            {
                _shotTimelineSliderRow,
                _referenceSplitControls,
            },
        };
        var content = new Border
        {
            Padding = new Thickness(12, 0, 12, 12),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    controlsRow,
                    timelineAndReference,
                },
            },
        };
        UpdateReferenceControlsVisibility();
        return content;
    }

    private static Control LabeledToggle(string label, EditorCompactToggleSwitch toggle)
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

    internal static Control ShotPlaybackIcon(bool isPlaying)
    {
        var icon = EditorIcons.Create(
            isPlaying ? EditorIcons.Pause : EditorIcons.Play,
            16);
        EditorIcons.ApplyBrush(icon, Brushes.White);
        return icon;
    }

    private void RefreshShotPlaybackButton()
    {
        _shotPlayButton.Content = ShotPlaybackIcon(PlaybackState.IsPlaying);
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

    public async Task<bool> RefreshOptionsAsync(
        IReadOnlyList<ProjectTreeNode> treeRoots)
    {
        var prepared = await PrepareOptionsAsync(
            treeRoots,
            CancellationToken.None);
        return prepared is not null
            && TryCommitOptions(prepared);
    }

    internal async Task<PreviewOptionsPreparation?>
        PrepareOptionsAsync(
            IReadOnlyList<ProjectTreeNode> treeRoots,
        CancellationToken cancellationToken)
    {
        var project = treeRoots.FirstOrDefault((node) => node.Kind == ProjectTreeNodeKind.Project);
        if (project is null) return null;

        var preparation = _visualContextPreparation.Begin();
        var linkedOperation =
            CancellationTokenSource.CreateLinkedTokenSource(
                preparation.Token,
                cancellationToken);
        PreviewOptionsPreparation? result = null;
        try
        {
            var prepared = await _operations.ExecuteAsync(
                () => (
                    Visual:
                        _visualContextData.LoadSnapshot(
                            project.Id),
                    Production:
                        _productionPreviewData.LoadSnapshot(
                            treeRoots)),
                linkedOperation.Token);
            if (_disposed
                || !_visualContextPreparation.IsCurrent(
                    preparation)
                || linkedOperation.IsCancellationRequested)
            {
                return null;
            }

            result = new PreviewOptionsPreparation(
                preparation,
                linkedOperation,
                prepared.Visual,
                prepared.Production,
                _visualContextSnapshot,
                _productionSessionSnapshot);
            return result;
        }
        catch (OperationCanceledException)
            when (linkedOperation.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            if (result is null)
            {
                _visualContextPreparation.Complete(
                    preparation);
                linkedOperation.Dispose();
            }
        }
    }

    internal bool IsCurrentOptionsPreparation(
        PreviewOptionsPreparation preparation)
    {
        return !_disposed
            && !preparation.LinkedOperation
                .IsCancellationRequested
            && _visualContextPreparation.IsCurrent(
                preparation.OwnerOperation);
    }

    internal bool TryCommitOptions(
        PreviewOptionsPreparation preparation,
        bool applyVisualState = true)
    {
        if (!IsCurrentOptionsPreparation(
                preparation))
        {
            DiscardOptions(preparation);
            return false;
        }

        _visualContextSnapshot =
            preparation.Visual;
        _productionSessionSnapshot =
            preparation.Production;
        _visualContextPreparation.Complete(
            preparation.OwnerOperation);
        preparation.Dispose();
        if (applyVisualState)
        {
            ApplyCommittedOptions(
                preparation);
        }
        return true;
    }

    internal void ApplyCommittedOptions(
        PreviewOptionsPreparation preparation)
    {
        InvalidatePreparedShotPlayback();
        ApplyVisualContextOptions(
            preparation.Visual);
        Refresh();
    }

    internal void RestoreCommittedOptions(
        PreviewOptionsPreparation preparation)
    {
        _visualContextSnapshot =
            preparation.PreviousVisual;
        _productionSessionSnapshot =
            preparation.PreviousProduction;
    }

    internal void DiscardOptions(
        PreviewOptionsPreparation preparation)
    {
        _visualContextPreparation.Complete(
            preparation.OwnerOperation);
        preparation.Dispose();
    }

    private void ApplyVisualContextOptions(
        PreviewVisualContextSnapshot snapshot)
    {
        _projectId = snapshot.ProjectId;
        _isRefreshingOptions = true;
        try
        {
            var deviceOptions = snapshot.DeviceOptions;
            _deviceComboBox.ItemsSource = deviceOptions;
            var selectedDevice = PreferredResourceOption(deviceOptions, SelectedDeviceId);
            _deviceComboBox.SelectedItem = selectedDevice;
            SelectedDeviceId = selectedDevice?.Value;

            var themeOptions = snapshot.ThemeOptions;
            _themeComboBox.ItemsSource = themeOptions;
            var selectedTheme = PreferredResourceOption(themeOptions, _selectedThemeId);
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
            _transparencyGridToggle.IsChecked = _showTransparencyGrid;
            _alphaOnlyToggle.IsChecked = _showAlphaOnly;
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
    }

    private void AttachControlEvents()
    {
        _owner.AddHandler(
            InputElement.KeyDownEvent,
            OnOwnerKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _previewPanel.AddHandler(
            InputElement.KeyDownEvent,
            OnPreviewPanelKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
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
        _marksToggle.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleButton.IsCheckedProperty)
            {
                OnMarksChanged();
            }
        };
        _canonicalFrameToggle.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleButton.IsCheckedProperty)
            {
                OnCanonicalFrameChanged();
            }
        };
        _transparencyGridToggle.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleButton.IsCheckedProperty)
            {
                OnTransparencyInspectionChanged();
            }
        };
        _alphaOnlyToggle.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleButton.IsCheckedProperty)
            {
                OnTransparencyInspectionChanged();
            }
        };
    }

    private void OnDeviceChanged()
    {
        if (_deviceComboBox.SelectedItem is not { } option) return;

        SelectedDeviceId = option.Value;
        if (!_isRefreshingOptions)
        {
            InvalidatePreparedShotPlayback();
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
            InvalidatePreparedShotPlayback();
            Refresh();
        }
    }

    private void OnModeChanged()
    {
        if (_modeComboBox.SelectedItem is not { } option) return;

        _selectedMode = option.Value;
        if (!_isRefreshingOptions)
        {
            InvalidatePreparedShotPlayback();
            Refresh();
        }
    }

    private void OnOrientationChanged()
    {
        if (_orientationComboBox.SelectedItem is not { } option) return;

        _selectedOrientation = option.Value;
        if (!_isRefreshingOptions)
        {
            InvalidatePreparedShotPlayback();
            Refresh();
        }
    }

    private void OnScaleChanged()
    {
        if (_scaleComboBox.SelectedItem is not { } option) return;

        _selectedScale = option.Value;
        if (!_isRefreshingOptions)
        {
            InvalidatePreparedShotPlayback();
            Refresh();
        }
    }

    private void OnPlaybackRouteChanged()
    {
        if (_playbackRouteComboBox.SelectedItem is not { } option) return;
        _selectedPlaybackRoute = option.Value;
        _designInputsPanel.PresentEveryPlaybackFrame = _selectedPlaybackRoute == "html-all";
        if (!_isRefreshingOptions)
        {
            InvalidatePreparedShotPlayback();
            Refresh();
        }
    }

    private void OnMarksChanged()
    {
        _showDesignMarks = _marksToggle.IsChecked == true;
        if (!_isRefreshingOptions)
        {
            InvalidatePreparedShotPlayback();
            _ = _designPreviewPane.SetDesignMarksAsync(_showDesignMarks);
            Refresh();
        }
    }

    private void OnCanonicalFrameChanged()
    {
        _showCanonicalFrame = _canonicalFrameToggle.IsChecked == true;
        if (!_isRefreshingOptions)
        {
            InvalidatePreparedShotPlayback();
            Refresh();
        }
    }

    private void OnTransparencyInspectionChanged()
    {
        _showTransparencyGrid = _transparencyGridToggle.IsChecked == true;
        _showAlphaOnly = _alphaOnlyToggle.IsChecked == true;
        if (!_isRefreshingOptions)
        {
            _ = _designPreviewPane.SetTransparencyInspectionAsync(
                _showTransparencyGrid,
                _showAlphaOnly);
            Refresh();
        }
    }

    public void NotifyAuthoredPreviewInputsChanged()
    {
        InvalidatePreparedShotPlayback();
        Refresh();
    }

    private async Task BrowseReferenceAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectId)) return;
        var mediaRoot = PreparedMediaRoot();
        if (string.IsNullOrWhiteSpace(mediaRoot)) return;
        var selected = await EditorPathBrowser.BrowseMediaFile(
            _owner.StorageProvider,
            _projectPaths,
            _referenceSource,
            mediaRoot);
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
        PreparedMediaRoot());

    public void BeginSelectionTransition()
    {
        CancelPlaybackPreparation();
        _productionPayloadPreparation.Cancel();
        if (PreviewWorkspace() != EditorWorkspace.Production
            || ProductionContextNode() is not { } selected)
        {
            return;
        }

        _designPreviewPane.BeginContextUpdate(selected.Name);
        PreviewDebugLog.Write(
            "preview.selection-transition.begin",
            ("workspace", _workspace),
            ("kind", selected.Kind),
            ("id", selected.Id));
    }

    public void ScheduleSelectionRefresh(Func<bool>? isCurrent = null)
    {
        var generation = Interlocked.Increment(ref _selectionRefreshGeneration);
        Dispatcher.UIThread.Post(
            () =>
            {
                if (generation != Volatile.Read(ref _selectionRefreshGeneration))
                {
                    return;
                }
                if (isCurrent is not null && !isCurrent())
                {
                    return;
                }

                RefreshCore();
            },
            DispatcherPriority.Background);
    }

    public void Refresh()
    {
        Interlocked.Increment(ref _selectionRefreshGeneration);
        CancelPlaybackPreparation();
        RefreshCore();
    }

    private void RefreshCore()
    {
        if (PreviewWorkspace() == EditorWorkspace.Production)
        {
            _ = RefreshProductionCoreAsync();
            return;
        }

        _productionPayloadPreparation.Cancel();
        try
        {
            PrepareStaticPreviewRefresh();
            var invalidProductionContext = InvalidProductionContext();
            var designPayload = invalidProductionContext is null ? DesignPreviewPayloadForSelection() : null;
            if (designPayload is not null)
            {
                designPayload =
                    ProcessDesignPreviewPayload(
                        designPayload);
            }
            var contextState =
                invalidProductionContext
                ?? (designPayload is null
                    ? NonRenderableStateForSelection(
                        _selectedNode(),
                        _selectedThemeId,
                        _selectedMode,
                        _shotPreviewFrame,
                        CancellationToken.None)
                    : PreviewContextState.Renderable);
            RenderStaticPreview(
                designPayload,
                contextState,
                invalidProductionContext);
        }
        catch (Exception exception)
        {
            _messages.Error("Preview", exception);
        }
    }

    private async Task RefreshProductionCoreAsync()
    {
        var preparation =
            _productionPayloadPreparation.Begin();
        var cancellationToken =
            preparation.Token;
        var revision =
            Volatile.Read(
                ref _selectionRefreshGeneration);
        try
        {
            if (TryRenderPreparedProductionFrame())
            {
                return;
            }

            PrepareStaticPreviewRefresh();
            var invalidProductionContext =
                InvalidProductionContext();
            var node =
                invalidProductionContext is null
                    ? ProductionPayloadNode()
                    : null;
            var themeId =
                _selectedThemeId;
            var themeMode =
                _selectedMode;
            var shotFrame =
                _shotPreviewFrame;
            var selected =
                _selectedNode();
            _designInputsPanel.UpdateForPayload(
                null,
                _projectId);
            var prepared =
                await _operations.ExecuteAsync(
                    () =>
                    {
                        var payload =
                            node is null
                                ? null
                                : _productionPayloadPreparer
                                    .Prepare(
                                        node,
                                        themeId,
                                        themeMode,
                                        shotFrame,
                                        cancellationToken);
                        var contextState =
                            invalidProductionContext
                            ?? (payload is null
                                ? IsTransparentShotFrame(node, shotFrame)
                                    ? PreviewContextState.Transparent
                                    : NonRenderableStateForSelection(
                                        selected,
                                        themeId,
                                        themeMode,
                                        shotFrame,
                                        cancellationToken)
                                : PreviewContextState
                                    .Renderable);
                        return (
                            Payload: payload,
                            ContextState:
                                contextState);
                    },
                    cancellationToken);
            if (_disposed
                || !_productionPayloadPreparation
                    .IsCurrent(preparation)
                || revision
                    != Volatile.Read(
                        ref _selectionRefreshGeneration))
            {
                return;
            }

            CommitProductionPreviewContext(
                node,
                prepared.Payload);
            RenderStaticPreview(
                prepared.Payload,
                prepared.ContextState,
                invalidProductionContext);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (_productionPayloadPreparation
                    .IsCurrent(preparation)
                && revision
                    == Volatile.Read(
                        ref _selectionRefreshGeneration))
            {
                _messages.Error(
                    "Preview",
                    exception);
            }
        }
        finally
        {
            _productionPayloadPreparation
                .Complete(preparation);
        }
    }

    private bool TryRenderPreparedProductionFrame()
    {
        var node = ProductionPayloadNode();
        if (_preparedShotPlayback is not { } prepared
            || node is null
            || !prepared.TryGetFrame(
                node,
                _shotPreviewFrame,
                out var payload)
            || payload is null)
        {
            return false;
        }

        PrepareStaticPreviewRefresh();
        _designInputsPanel.UpdateForPayload(
            null,
            _projectId);
        CommitProductionPreviewContext(
            node,
            payload);
        RenderStaticPreview(
            payload,
            PreviewContextState.Renderable,
            null);
        return true;
    }

    private void CommitProductionPreviewContext(
        ProjectTreeNode? node,
        DesignPreviewPayload? payload)
    {
        if (payload is not null
            && node is not null)
        {
            _lastProductionPreviewNode =
                PreviewNodeKey.From(node);
            _activeDesignPreviewNode =
                _lastProductionPreviewNode;
            _activeProductionHistoryEntry =
                new DesignPreviewHistoryEntry(
                    _lastProductionPreviewNode,
                    payload.Name);
            return;
        }

        _activeDesignPreviewNode = null;
        _activeProductionHistoryEntry = null;
    }

    private void PrepareStaticPreviewRefresh()
    {
        // Static preview changes (including reference images and design marks)
        // must never inherit a playback preparation overlay.
        if (!IsPreviewPlaybackActive
            && !_shotPlaybackIsPreparing)
        {
            HidePreviewLoading();
        }
        EnsureSelectedOptionsExist();
        UpdateShotTimelineControls();
        UpdateProductionPreviewSetup();
    }

    private void RenderStaticPreview(
        DesignPreviewPayload? designPayload,
        PreviewContextState contextState,
        PreviewContextState? invalidProductionContext)
    {
        if (invalidProductionContext is not null)
        {
            _messages.Error(
                "Production context",
                invalidProductionContext.Message);
        }
        var deviceId =
            PreviewDeviceId(designPayload);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _messages.Warning(
                "Preview",
                "No device selected.");
            return;
        }

        var metrics =
            _showCanonicalFrame
                ? CanonicalPreviewMetrics()
                : ApplyPreviewOrientation(
                    PreparedDeviceMetrics(
                        deviceId,
                        designPayload));
        var themeName =
            _themeComboBox.SelectedItem?.Label
            ?? "No theme";
        UpdateDesignContextChrome(
            designPayload);
        if (_selectedPlaybackRoute == "raster"
            && designPayload is not null
            && IsPreviewPlaybackActive
            && !_showTransparencyGrid
            && !_showAlphaOnly
            && _rasterPlaybackFrames.TryGetValue(
                PlaybackFrameKey(designPayload),
                out var rasterPath))
        {
            _designPreviewPane.ShowRasterFrame(
                rasterPath);
            RecordAndUpdatePlaybackStatus(
                new DesignWebPreviewPane
                    .DesignPreviewFrameStatus(
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
            _showTransparencyGrid,
            _showAlphaOnly,
            CurrentReferenceState(),
            designPayload,
            contextState,
            IsPreviewPlaybackActive,
            _messages);
        if (designPayload is not null
            && _designInputsPanel
                .IsPlaybackActive)
        {
            SchedulePlaybackAheadPreload(
                metrics,
                designPayload);
        }
        _messages.Clear();
    }

    public void TriggerDesignPreviewAction(string actionId, string? targetValue = null)
    {
        if (_designInputsPanel.TriggerAction(actionId, targetValue))
        {
            return;
        }

        Refresh();
        _designInputsPanel.TriggerAction(actionId, targetValue);
    }

    public bool CanRestoreDesignPreviewAction(string actionId)
    {
        return _designInputsPanel.CanRestoreAction(actionId);
    }

    public bool IsDesignPreviewActionPlaying(string actionId)
    {
        return _designInputsPanel.IsActionPlaying(actionId);
    }

    public bool CanStepDesignPreviewAction(string actionId, int delta)
    {
        return _designInputsPanel.CanStepActionFrame(actionId, delta);
    }

    public int CurrentDesignPreviewActionFrame(string actionId)
    {
        return _designInputsPanel.CurrentActionFrame(actionId);
    }

    public int MaximumDesignPreviewActionFrame(string actionId)
    {
        return _designInputsPanel.MaximumActionFrame(actionId);
    }

    public void StepDesignPreviewAction(
        string actionId,
        int delta,
        string? targetValue = null)
    {
        if (_designInputsPanel.StepActionFrame(actionId, delta, targetValue))
        {
            return;
        }

        Refresh();
        _designInputsPanel.StepActionFrame(actionId, delta, targetValue);
    }

    public void SetDesignPreviewActionFrame(
        string actionId,
        int frame,
        string? targetValue = null)
    {
        if (_designInputsPanel.SetActionFrame(actionId, frame, targetValue))
        {
            return;
        }

        Refresh();
        _designInputsPanel.SetActionFrame(actionId, frame, targetValue);
    }

    public void RestoreDesignPreviewAction(string actionId)
    {
        if (_designInputsPanel.RestoreAction(actionId)) return;
        Refresh();
        _designInputsPanel.RestoreAction(actionId);
    }

    public void SetDesignPreviewTestValue(string jsonKey, string value)
    {
        _designInputsPanel.SetExternalInputValue(jsonKey, value);
    }

    public void DiscardCommittedProductionRuntimeValue(
        string jsonKey)
    {
        _designInputsPanel.DiscardExternalInputValue(jsonKey);
    }

    public void DiscardCommittedProductionRuntimeCollection(
        string rootStorageJsonKey)
    {
        _designInputsPanel.DiscardExternalCollectionValues(
            rootStorageJsonKey);
    }

    public void SetDesignPreviewCollectionItemValues(
        StructuredCollectionAddress address,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        _designInputsPanel.SetExternalCollectionItemValues(
            address,
            itemId,
            values);
    }

    public void SetDesignPreviewCollectionTestItems(
        ProjectTreeNode node,
        string collectionJsonKey,
        IReadOnlyList<JsonObject> items)
    {
        var payload = DesignPreviewPayloadFactory.Create(
            _previewPayloadData,
            node,
            _selectedThemeId,
            _selectedMode,
            _shotPreviewFrame);
        if (payload is not null)
        {
            _designInputsPanel.SetExternalCollectionItems(payload, collectionJsonKey, items);
        }
    }

    public JsonObject ApplyDesignPreviewTransientTestValues(ProjectTreeNode node, JsonObject preview)
    {
        var payload = DesignPreviewPayloadFactory.Create(
            _previewPayloadData,
            node,
            _selectedThemeId,
            _selectedMode,
            _shotPreviewFrame);
        return payload is null
            ? preview.DeepClone() as JsonObject ?? new JsonObject()
            : _designInputsPanel.ApplyTransientTestValues(preview, payload);
    }

    public ComponentPreviewTransientState
        CaptureDesignPreviewTransientState(
            ProjectTreeNode node) =>
        _designInputsPanel.CaptureTransientState(
            node,
            node.Kind == ProjectTreeNodeKind.ModuleInstance);

    public bool ResetDesignPreviewTestValues(ProjectTreeNode node)
    {
        var payload = DesignPreviewPayloadFactory.Create(
            _previewPayloadData,
            node,
            _selectedThemeId,
            _selectedMode,
            _shotPreviewFrame);
        return payload is not null && _designInputsPanel.ResetTestValues(payload);
    }

    private async Task<bool> PreparePlaybackFramesAsync(ComponentPreviewActionDefinition? requestedAction)
    {
        var operation = _designPlaybackPreparation.Begin();
        try
        {
            var prepared = await PreparePlaybackFramesAsync(
                requestedAction,
                operation.Token,
                PlaybackFrameCacheOwner.Design);
            return prepared && _designPlaybackPreparation.IsCurrent(operation);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            if (_designPlaybackPreparation.Complete(operation))
            {
                HidePreviewLoading();
            }
        }
    }

    private async Task<bool> PreparePlaybackFramesAsync(
        ComponentPreviewActionDefinition? requestedAction,
        CancellationToken cancellationToken,
        PlaybackFrameCacheOwner cacheOwner)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSelectedOptionsExist();
        if (string.IsNullOrWhiteSpace(_projectId))
        {
            return true;
        }

        DesignPreviewPayload? designPayload;
        if (cacheOwner
            == PlaybackFrameCacheOwner.Shot)
        {
            designPayload =
                _pendingPlaybackFramesOverride?
                    .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Production playback requires prepared frame payloads.");
        }
        else
        {
            designPayload =
                DesignPreviewPayloadForSelection();
            if (designPayload is null)
            {
                return true;
            }
            designPayload =
                ProcessDesignPreviewPayload(
                    designPayload);
        }

        var deviceId = PreviewDeviceId(designPayload);
        if (string.IsNullOrWhiteSpace(deviceId)) return true;
        var metrics = ApplyPreviewOrientation(
            PreparedDeviceMetrics(
                deviceId,
                designPayload));
        var payload = designPayload;
        var projectFps = payload.FrameRate;
        var previewFps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        var designRequestSignature = cacheOwner == PlaybackFrameCacheOwner.Design
            ? DesignPlaybackRequestSignature(metrics, payload, requestedAction)
            : "";
        var designReuse = cacheOwner == PlaybackFrameCacheOwner.Design
            ? PreparedPlaybackReusePolicy.Decide(
                _preparedDesignPlayback?.RequestSignature,
                designRequestSignature,
                HasFrameCacheReservation(PlaybackFrameCacheOwner.Design))
            : PreparedPlaybackReuse.None;
        if (designReuse == PreparedPlaybackReuse.Complete)
        {
            PreviewDebugLog.Write(
                "preview.playback.design-cache-hit",
                ("component", payload.ComponentType),
                ("name", payload.Name),
                ("action", requestedAction?.Id ?? ""),
                ("frames", _preparedDesignPlayback!.Frames.Count));
            return true;
        }
        if (cacheOwner == PlaybackFrameCacheOwner.Design
            && designReuse == PreparedPlaybackReuse.None)
        {
            InvalidatePreparedDesignPlayback();
        }
        var frames = _pendingPlaybackFramesOverride?.ToList()
            ?? (designReuse == PreparedPlaybackReuse.Frames
                ? _preparedDesignPlayback!.Frames
                : PlaybackFramePayloads(payload, projectFps, requestedAction).ToList());
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
        ReserveFrameCacheCapacity(cacheOwner, frames.Count);
        if (_selectedPlaybackRoute != "raster")
        {
            var prewarmStopwatch = Stopwatch.StartNew();
            ShowPreviewLoading($"Preparing HTML 0 / {frames.Count} frames…");
            try
            {
                var imageSources = new HashSet<string>(StringComparer.Ordinal);
                for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var bodyContent = await WebDesignPreviewRenderer.RenderBodyAsync(
                        metrics,
                        _showDesignMarks,
                        frames[frameIndex]);
                    foreach (var source in DesignWebPreviewPane.ImageSourcesForPreload(bodyContent))
                    {
                        imageSources.Add(source);
                    }
                    if ((frameIndex + 1) % 5 == 0 || frameIndex + 1 == frames.Count)
                    {
                        UpdatePreviewLoading(
                            $"Preparing HTML {frameIndex + 1} / {frames.Count} frames…");
                    }
                }
                UpdatePreviewLoading($"Decoding HTML assets 0 / {imageSources.Count}…");
                var loadedImages = await _designPreviewPane.PreloadFrameImagesAsync(imageSources, cancellationToken);
                UpdatePreviewLoading($"Decoding HTML assets {loadedImages} / {imageSources.Count}…");
            }
            catch (OperationCanceledException)
            {
                InvalidatePlaybackPreparation(cacheOwner);
                return false;
            }
            catch
            {
                InvalidatePlaybackPreparation(cacheOwner);
                throw;
            }
            RememberPreparedDesignPlayback(
                cacheOwner,
                designRequestSignature,
                frames);
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
                await _designPreviewPane.PrepareRasterPlaybackAsync(_rasterPlaybackOrder, cancellationToken);
                await _designPreviewPane.SyncRasterViewportAsync();
                RememberPreparedDesignPlayback(
                    cacheOwner,
                    designRequestSignature,
                    frames);
                PreviewDebugLog.Write(
                    "preview.playback.raster-cache-hit",
                    ("component", payload.ComponentType),
                    ("name", payload.Name),
                    ("frames", frames.Count));
                return true;
            }
            catch
            {
                InvalidatePlaybackPreparation(cacheOwner);
                throw;
            }
        }

        _aheadPreloadCancellation?.Cancel();
        _aheadPreloadCancellation?.Dispose();
        _aheadPreloadCancellation = null;
        _aheadPreloadedFrameKeys.Clear();
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
            ShowPreviewLoading($"Rasterizing {frames.Count} frames for playback...");
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
                cancellationToken.ThrowIfCancellationRequested();
                var rasterHtml = await DesignWebPreviewPane.BuildRasterHtmlAsync(metrics, frame);
                var rasterPath = Path.Combine(_rasterCacheDirectory, $"frame-{frameIndex:D6}.webp");
                await _chromiumRasterizer.RasterizeAsync(
                    rasterHtml,
                    Math.Max(1, (int)Math.Ceiling(metrics.CanvasWidth)),
                    Math.Max(1, (int)Math.Ceiling(metrics.CanvasHeight)),
                    rasterPath,
                    "webp",
                    quality: 95,
                    captureScale: 1,
                    cancellationToken);
                _rasterPlaybackFrames[PlaybackFrameKey(frame)] = rasterPath;
                _rasterPlaybackOrder.Add(rasterPath);
                UpdateRasterProgress(frameIndex + 1, frames.Count);
            }
            _rasterPlaybackSignature = rasterSignature;
            await _designPreviewPane.PrepareRasterPlaybackAsync(_rasterPlaybackOrder, cancellationToken);
            await _designPreviewPane.SyncRasterViewportAsync();
            RememberPreparedDesignPlayback(
                cacheOwner,
                designRequestSignature,
                frames);
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
            InvalidatePlaybackPreparation(cacheOwner);
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
            InvalidatePlaybackPreparation(cacheOwner);
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
    }

    private async Task PreloadPlaybackFramesAsync(
        DevicePreviewMetrics metrics,
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
        DevicePreviewMetrics metrics,
        DesignPreviewPayload payload)
    {
        if (_isAheadPreloading || string.IsNullOrWhiteSpace(_projectId))
        {
            return;
        }

        var preview = JsonPath.ParseRequiredObject(payload.DesignPreviewJson, "Design Preview payload");
        if (PlaybackFrameAction(preview)?.PrewarmFrames != true)
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

    private void ShowPreviewLoading(string message)
    {
        _previewBusyHost.IsVisible = true;
        _previewLoadingScrim.Show(message, CancelPreviewLoading);
        _designPreviewPane.SetRasterLoading(true, message);
        SetPreviewPerformanceStatus(PreviewPerformanceStatus.Loading);
    }

    private void UpdatePreviewLoading(string message)
    {
        _previewLoadingScrim.SetMessage(message);
        _designPreviewPane.SetRasterLoading(true, message);
    }

    private void UpdateRasterProgress(int completedFrames, int totalFrames)
    {
        var message = $"Rasterizing {completedFrames} / {totalFrames} frames…";
        UpdatePreviewLoading(message);
        _messages.Info("Playback", message);
    }

    private void HidePreviewLoading()
    {
        _previewLoadingScrim.Hide();
        _previewBusyHost.IsVisible = false;
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
        PlaybackState.SetPlaying(true);
        _playbackSummaryGeneration++;
        _playbackPerformanceRun = new PlaybackPerformanceRun(run.TargetFrames, run.TargetFps, Stopwatch.GetTimestamp());
        if (_selectedPlaybackRoute == "raster"
            && _rasterPlaybackOrder.Count > 0)
        {
            _designPreviewPane.PlayRasterFrames(_rasterPlaybackOrder);
        }
    }

    private string RasterPlaybackSignature(
        DevicePreviewMetrics metrics,
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
            payload.ThemeStatusBarVariantReference,
            payload.ThemeNavigationBarVariantReference,
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

    private string DesignPlaybackRequestSignature(
        DevicePreviewMetrics metrics,
        DesignPreviewPayload payload,
        ComponentPreviewActionDefinition? action)
    {
        var signatureJson = JsonSerializer.Serialize(new
        {
            Version = 1,
            PlaybackRoute = _selectedPlaybackRoute,
            ThemeId = _selectedThemeId,
            ThemeMode = _selectedMode,
            DeviceId = SelectedDeviceId,
            Orientation = _selectedOrientation,
            Scale = _selectedScale,
            ShowDesignMarks = _showDesignMarks,
            ShowCanonicalFrame = _showCanonicalFrame,
            ShellIsDark = _isDark(),
            metrics.CanvasWidth,
            metrics.CanvasHeight,
            Payload = PlaybackPayloadFingerprint(payload),
            Action = action,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureJson)));
    }

    private void OnPlaybackStopped(ComponentPreviewInputSession.PlaybackRunInfo run)
    {
        PlaybackState.SetPlaying(false);
        if (_preparedShotPlayback is null && _preparedDesignPlayback is null)
        {
            ReleaseFrameCacheReservations();
        }
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

    private void ReleaseFrameCacheReservations()
    {
        foreach (var reservation in _frameCacheReservations.Values)
        {
            reservation.Dispose();
        }
        _frameCacheReservations.Clear();
    }

    private void ReleaseFrameCacheReservation(
        PlaybackFrameCacheOwner owner)
    {
        if (!_frameCacheReservations.Remove(
                owner,
                out var reservation))
        {
            return;
        }
        reservation.Dispose();
    }

    private void ReserveFrameCacheCapacity(
        PlaybackFrameCacheOwner owner,
        int frameCount)
    {
        ReleaseFrameCacheReservation(
            owner);
        _frameCacheReservations[owner] =
            WebDesignPreviewRenderer
                .ReserveFrameCacheCapacity(
                    frameCount);
    }

    private bool HasFrameCacheReservation(PlaybackFrameCacheOwner owner) =>
        _frameCacheReservations.ContainsKey(
            owner);

    private void RememberPreparedDesignPlayback(
        PlaybackFrameCacheOwner owner,
        string requestSignature,
        IReadOnlyList<DesignPreviewPayload> frames)
    {
        if (owner != PlaybackFrameCacheOwner.Design) return;
        _preparedDesignPlayback = new PreparedDesignPlayback(requestSignature, frames);
    }

    private void InvalidatePlaybackPreparation(PlaybackFrameCacheOwner owner)
    {
        if (owner == PlaybackFrameCacheOwner.Design)
        {
            InvalidatePreparedDesignPlayback();
            return;
        }
        if (HasFrameCacheReservation(owner))
        {
            ReleaseFrameCacheReservation(
                owner);
        }
    }

    private void CancelShotPlaybackPreparation()
    {
        _shotPlaybackPreparation.Cancel();
    }

    private void CancelPlaybackPreparation()
    {
        CancelShotPlaybackPreparation();
        _designPlaybackPreparation.Cancel();
    }

    private void CancelPreviewLoading()
    {
        CancelPlaybackPreparation();
    }

    private void OnOwnerKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape || !StopPreviewFromEscape())
        {
            return;
        }

        args.Handled = true;
    }

    private void OnPreviewPanelKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.KeyModifiers != KeyModifiers.None
            || PreviewInputOwnsNavigationKeys(args.Source))
        {
            return;
        }

        var handled = args.Key switch
        {
            Key.Left => _stepScreenTimelineFrame?.Invoke(-1) == true,
            Key.Right => _stepScreenTimelineFrame?.Invoke(1) == true,
            Key.PageUp =>
                _moveToScreenTimelineNavigationFrame?.Invoke(-1) == true,
            Key.PageDown =>
                _moveToScreenTimelineNavigationFrame?.Invoke(1) == true,
            _ => false,
        };
        if (handled) args.Handled = true;
    }

    private static bool PreviewInputOwnsNavigationKeys(object? source)
    {
        if (source is not Visual visual) return false;
        return visual
            .GetVisualAncestors()
            .Prepend(visual)
            .Any((candidate) => candidate is TextBox
                or NumericUpDown
                or RangeBase
                or ComboBox
                or EditorInstantComboBox);
    }

    private bool StopPreviewFromEscape()
    {
        var stopShot = _shotPlaybackIsPreparing || _shotPlaybackTimer.IsEnabled;
        var stopDesign = _designInputsPanel.IsPreparingPlayback || _designInputsPanel.IsPlaybackActive;
        if (!stopShot && !stopDesign)
        {
            return false;
        }

        CancelPlaybackPreparation();
        if (stopShot)
        {
            StopShotPlayback();
        }
        if (stopDesign)
        {
            _designInputsPanel.StopActivePlayback();
        }
        return true;
    }

    private void InvalidatePreparedShotPlayback()
    {
        _preparedShotPlayback = null;
        if (HasFrameCacheReservation(PlaybackFrameCacheOwner.Shot))
        {
            ReleaseFrameCacheReservation(
                PlaybackFrameCacheOwner.Shot);
        }
    }

    private void InvalidatePreparedDesignPlayback()
    {
        _preparedDesignPlayback = null;
        if (HasFrameCacheReservation(PlaybackFrameCacheOwner.Design))
        {
            ReleaseFrameCacheReservation(
                PlaybackFrameCacheOwner.Design);
        }
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

    private sealed record PreparedDesignPlayback(
        string RequestSignature,
        IReadOnlyList<DesignPreviewPayload> Frames);

    private enum PlaybackFrameCacheOwner
    {
        Design,
        Shot,
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

    internal static IEnumerable<DesignPreviewPayload> PlaybackFramePayloads(
        DesignPreviewPayload payload,
        int projectFps,
        ComponentPreviewActionDefinition? requestedAction = null)
    {
        var fps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        var preview = JsonPath.ParseRequiredObject(payload.DesignPreviewJson, "Design Preview payload");

        var action = requestedAction is not null && ComponentPreviewActions.IsApplicable(preview, requestedAction)
            ? requestedAction
            : PlaybackFrameAction(preview);
        if (action is null)
        {
            yield break;
        }

        var timeJsonKey = action.TimeJsonKey;
        var frameCount = PlaybackDurationFrames(action, preview, fps, payload.ThemeTokensJson);
        if (frameCount <= 0)
        {
            yield break;
        }

        for (var frame = 0; frame <= frameCount; frame++)
        {
            var framePreview = (JsonObject)preview.DeepClone();
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
            yield return DesignPreviewPlaybackFrameProjection.Apply(
                payload with { DesignPreviewJson = framePreview.ToJsonString() },
                action,
                frame);
        }
    }

    private static IEnumerable<DesignPreviewPayload> PlaybackAheadFramePayloads(DesignPreviewPayload payload, int projectFps)
    {
        var fps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        var preview = JsonPath.ParseRequiredObject(payload.DesignPreviewJson, "Design Preview payload");

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

        var actionTime = ComponentPreviewActionRuntimeValue.RequireTime(preview, action);
        var currentFrame = action.TimeUnit == ComponentPreviewActionTimeUnit.Frames
            ? Math.Max(0, (int)Math.Floor(actionTime))
            : action.TimeUnit == ComponentPreviewActionTimeUnit.Milliseconds
                ? Math.Max(0, (int)Math.Floor(actionTime / 1000 * fps))
                : Math.Max(0, (int)Math.Floor(actionTime * fps));
        for (var index = 1; index <= AheadPlaybackPreloadFrames * 2; index++)
        {
            var frame = currentFrame + index;
            if (frame > durationFrames)
            {
                yield break;
            }

            var framePreview = (JsonObject)preview.DeepClone();
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
            yield return DesignPreviewPlaybackFrameProjection.Apply(
                payload with { DesignPreviewJson = framePreview.ToJsonString() },
                action,
                action.TimeUnit == ComponentPreviewActionTimeUnit.Frames
                    ? frame
                    : action.TimeUnit == ComponentPreviewActionTimeUnit.Milliseconds
                        ? frame / (double)fps * 1000
                        : frame / (double)fps);
        }
    }

    private static int PlaybackDurationFrames(ComponentPreviewActionDefinition action, JsonObject preview, int fps, string themeTokensJson)
    {
        if (action.DurationOwnerTimeline)
        {
            return RuntimeTimeline.DurationFrames(
                preview.ToJsonString(),
                preview.ToJsonString(),
                "{}",
                1,
                themeTokensJson,
                fps);
        }
        if (!string.IsNullOrWhiteSpace(action.DurationStateCollectionJsonKey))
        {
            var durationMs = ComponentPreviewActions.MotionStateTransitionDurationMilliseconds(
                preview,
                action,
                themeTokensJson);
            return durationMs <= 0
                ? 0
                : Math.Max(1, (int)Math.Ceiling(durationMs / 1000.0 * Math.Max(1, fps)));
        }
        if (!string.IsNullOrWhiteSpace(action.DurationThemeToken))
        {
            var themeTokens = JsonPath.ParseRequiredObject(themeTokensJson, "Theme tokens");
            var value = ThemeNumericTokenValue.RequirePositive(
                themeTokens,
                action.DurationThemeToken,
                $"Design Preview action '{action.Id}' duration");
            var seconds = action.TimeUnit switch
            {
                ComponentPreviewActionTimeUnit.Milliseconds => value / 1000.0,
                ComponentPreviewActionTimeUnit.Frames => value / Math.Max(1, fps),
                _ => value,
            };
            return seconds <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(seconds * Math.Max(1, fps)));
        }
        if (!string.IsNullOrWhiteSpace(action.DurationCollectionJsonKey))
        {
            return ComponentPreviewActionRuntimeValue.CollectionDurationFrames(preview, action);
        }
        if (!string.IsNullOrWhiteSpace(action.DurationBehaviorTimingInputId))
        {
            var owner = ComponentPreviewActions.RequiredOwner(preview, action);
            var fields = ComponentPreviewActionRuntimeValue.RequireInputDefinitions(preview, action);
            var definition = fields.FirstOrDefault((field) =>
                JsonString(field, "id") == action.DurationBehaviorTimingInputId)
                ?? throw new InvalidOperationException(
                    $"Missing BehaviorTiming action input '{action.DurationBehaviorTimingInputId}'.");
            var themeTokens = JsonPath.ParseRequiredObject(themeTokensJson, "Theme tokens");
            return BehaviorTimingResolver.ResolveFrames(owner, definition, fields, themeTokens);
        }

        if (action.TimeUnit == ComponentPreviewActionTimeUnit.Frames)
        {
            return Math.Max(1, (int)Math.Round(
                ComponentPreviewActionRuntimeValue.RequireDurationInput(preview, action),
                MidpointRounding.AwayFromZero));
        }

        var duration = action.DurationSeconds > 0
            ? action.DurationSeconds
            : ComponentPreviewActionRuntimeValue.RequireDurationInput(preview, action);
        return Math.Max(1, (int)Math.Ceiling(duration * Math.Max(1, fps)));
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
        var preview = JsonPath.ParseRequiredObject(payload.DesignPreviewJson, "Design Preview payload");
        var action = PlaybackFrameAction(preview);
        if (action is null || string.IsNullOrWhiteSpace(action.TimeJsonKey))
        {
            return "";
        }

        return ComponentPreviewActions.Value(preview, action, action.TimeJsonKey)?.ToJsonString() ?? "";
    }

    private static ComponentPreviewActionDefinition? PlaybackFrameAction(JsonObject preview)
    {
        var actions = ComponentPreviewActions.ReadApplicable(preview);
        return actions.FirstOrDefault((action) =>
                ComponentPreviewActionRuntimeValue.RequireBoolean(preview, action, action.PlayInputId))
            ?? actions.FirstOrDefault();
    }

    private static string JsonString(JsonObject owner, string key)
    {
        return owner[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : "";
    }


    private DevicePreviewMetrics ApplyPreviewOrientation(DevicePreviewMetrics metrics)
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

    private static DevicePreviewMetrics CanonicalPreviewMetrics()
    {
        return new DevicePreviewMetrics(
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
            DeviceModuleTransparencyOverride.Disabled);
    }

    private DesignPreviewPayload? DesignPreviewPayloadForSelection()
    {
        if (PreviewWorkspace() == EditorWorkspace.Production)
        {
            return null;
        }
        if (LockedNode(EditorWorkspace.Design) is { } lockedNode)
        {
            var lockedPayload = DesignPreviewPayloadFactory.Create(_previewPayloadData, lockedNode.ToNode(), _selectedThemeId, _selectedMode, _shotPreviewFrame);
            if (lockedPayload is not null)
            {
                _activeDesignPreviewNode = lockedNode;
                return lockedPayload;
            }

            throw new InvalidOperationException(
                $"Locked Design Preview context '{lockedNode.Id}' is no longer renderable.");
        }

        var selectedNode = _selectedNode();
        var selectedPayload = DesignPreviewPayloadFactory.Create(_previewPayloadData, selectedNode, _selectedThemeId, _selectedMode, _shotPreviewFrame);
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

    private PreviewContextState NonRenderableStateForSelection(
        ProjectTreeNode? selected,
        string? themeId,
        string themeMode,
        int shotFrame,
        CancellationToken cancellationToken)
    {
        var destination =
            FirstRenderableDescendant(
                selected,
                themeId,
                themeMode,
                shotFrame,
                cancellationToken);
        if (selected?.Kind == ProjectTreeNodeKind.Episode)
        {
            return new PreviewContextState(
                PreviewContextStateKind.NonRenderable,
                "Select a Shot or Screen to preview",
                "An Episode organizes the production sequence, but does not produce an image by itself.",
                destination is null ? "" : "Open first Shot",
                destination?.Id ?? "");
        }
        return new PreviewContextState(
            PreviewContextStateKind.NonRenderable,
            selected is null ? "There is no renderable selection" : $"{selected.Name} has no direct preview",
            selected is null
                ? "Select a component, module, or Screen to view its resolved result."
                : "This object organizes or contains other elements, but does not produce an image by itself.",
            destination is null ? "" : "View renderable items",
            destination?.Id ?? "");
    }

    private ProjectTreeNode? FirstRenderableDescendant(
        ProjectTreeNode? node,
        string? themeId,
        string themeMode,
        int shotFrame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (node is null) return null;
        foreach (var child in node.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DesignPreviewPayloadFactory.Create(
                    _previewPayloadData,
                    child,
                    themeId,
                    themeMode,
                    shotFrame) is not null)
            {
                return child;
            }

            var nested =
                FirstRenderableDescendant(
                    child,
                    themeId,
                    themeMode,
                    shotFrame,
                    cancellationToken);
            if (nested is not null) return nested;
        }

        return null;
    }

    private void UpdateShotTimelineControls()
    {
        var shotId = ProductionShotId();
        if (PreviewWorkspace() != EditorWorkspace.Production || string.IsNullOrWhiteSpace(shotId))
        {
            StopShotPlayback();
            _shotTimelineControls.IsVisible = false;
            _shotTimelineSliderRow.IsVisible = false;
            _shotReferenceVideoButton.IsVisible = false;
            _referenceVideoController.SetContext(
                "",
                25,
                0,
                false,
                ShotReferenceVideoDocument.Empty);
            return;
        }
        var contextNode = ProductionContextNode();
        var shot =
            PreparedProductionSession().Shot(shotId);
        var duration = shot.DurationFrames;
        if (_shotTimelineShotId != shotId || _shotTimelineContextNodeId != contextNode?.Id)
        {
            _shotTimelineShotId = shotId;
            _shotTimelineContextNodeId = contextNode?.Id ?? "";
        }
        _shotPreviewFrame = Math.Clamp(_shotPreviewFrame, 0, Math.Max(0, duration - 1));
        var range = NavigationFrameRange();
        _shotPreviewFrame = Math.Clamp(_shotPreviewFrame, range.StartFrame, range.EndFrame);
        var displayedFrame = Math.Clamp(_shotPreviewFrame - range.StartFrame, 0, Math.Max(0, range.DurationFrames - 1));
        _isUpdatingShotTimeline = true;
        _shotFrameSlider.Maximum = Math.Max(0, range.DurationFrames - 1);
        _shotFrameSlider.Value = displayedFrame;
        var timelineScope = contextNode?.Kind == ProjectTreeNodeKind.ModuleInstance
            ? "Screen local timeline"
            : "Shot timeline";
        _shotFrameText.Text = $"{displayedFrame}/{Math.Max(0, range.DurationFrames - 1)}";
        EditorAccessibility.Describe(
            _shotFrameText,
            $"{timelineScope}, frame {displayedFrame} of {Math.Max(0, range.DurationFrames - 1)}",
            showToolTip: false);
        _shotPreviousFrameButton.IsEnabled = _shotPreviewFrame > range.StartFrame;
        _shotNextFrameButton.IsEnabled = _shotPreviewFrame < range.EndFrame;
        _shotAbsoluteStartButton.IsEnabled = _shotPreviewFrame > range.StartFrame;
        _shotAbsoluteEndButton.IsEnabled = _shotPreviewFrame < range.EndFrame;
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
                ? "Play or pause the shared Shot timeline; current frame is an animation keyframe"
                : "Play or pause the shared Shot timeline");
        var activeSlotIndex = ActiveShotSlotIndex(shotId);
        var slotCount = shot.Screens.Count;
        var showScreenStep = contextNode?.Kind == ProjectTreeNodeKind.Shot;
        _shotPreviousSlotButton.IsVisible = showScreenStep;
        _shotNextSlotButton.IsVisible = showScreenStep;
        _shotPreviousSlotButton.IsEnabled = showScreenStep && activeSlotIndex > 0;
        _shotNextSlotButton.IsEnabled = showScreenStep && activeSlotIndex >= 0 && activeSlotIndex < slotCount - 1;
        _shotTimelineControls.IsVisible = true;
        _shotTimelineSliderRow.IsVisible = true;
        _shotReferenceVideoButton.IsVisible = true;
        _shotReferenceVideoButton.IsEnabled =
            !string.IsNullOrWhiteSpace(shot.ReferenceVideo.SourcePath);
        ToolTip.SetTip(
            _shotReferenceVideoButton,
            _shotReferenceVideoButton.IsEnabled
                ? shot.ReferenceVideo.SourcePath
                : "Assign a reference video in Shot > General");
        SyncReferenceVideo();
        RefreshShotReferenceMarkers();
        _isUpdatingShotTimeline = false;
    }

    private void SyncReferenceVideo()
    {
        var shotId = ProductionShotId();
        if (PreviewWorkspace() != EditorWorkspace.Production
            || string.IsNullOrWhiteSpace(shotId)
            || _productionSessionSnapshot is null
            || !_productionSessionSnapshot.ShotsById.TryGetValue(
                shotId,
                out var shot))
        {
            return;
        }
        _referenceVideoController.SetContext(
            shotId,
            shot.FrameRate,
            _shotPreviewFrame,
            PlaybackState.IsPlaying && !PlaybackState.IsBusy,
            shot.ReferenceVideo);
    }

    private void RefreshShotReferenceMarkers()
    {
        _shotReferenceMarkerOverlay.Children.Clear();
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)
            || _productionSessionSnapshot is null
            || !_productionSessionSnapshot.ShotsById.TryGetValue(
                shotId,
                out var shot))
        {
            return;
        }
        var range = NavigationFrameRange();
        var width = _shotReferenceMarkerOverlay.Bounds.Width;
        if (width <= 0 || shot.ReferenceVideo.InFrame is not { } inFrame) return;
        foreach (var marker in shot.ReferenceVideo.Markers)
        {
            var shotFrame = marker.VideoFrame - inFrame;
            if (shotFrame < range.StartFrame || shotFrame > range.EndFrame) continue;
            var tick = new Border
            {
                Width = 2,
                Height = 12,
                Background = EditorAnimationVisuals.ActiveTrackBrush,
            };
            ToolTip.SetTip(
                tick,
                string.IsNullOrWhiteSpace(marker.Text)
                    ? $"Reference marker · frame {shotFrame}"
                    : marker.Text);
            Canvas.SetLeft(
                tick,
                (shotFrame - range.StartFrame)
                / (double)Math.Max(1, range.DurationFrames - 1)
                * Math.Max(0, width - tick.Width));
            _shotReferenceMarkerOverlay.Children.Add(tick);
        }
    }

    private async Task CommitReferenceVideoAsync(
        string shotId,
        ShotReferenceVideoDocument document)
    {
        await _operations.ExecuteAsync(() =>
            _productionRecordFields.UpdateShotField(
                shotId,
                "shot.referenceVideo",
                document.ToJson()));
        if (_productionSessionSnapshot is null
            || !_productionSessionSnapshot.ShotsById.TryGetValue(
                shotId,
                out var shot))
        {
            return;
        }
        var shots = _productionSessionSnapshot.ShotsById
            .ToDictionary(
                (entry) => entry.Key,
                (entry) => entry.Value,
                StringComparer.Ordinal);
        shots[shotId] = shot with { ReferenceVideo = document };
        _productionSessionSnapshot = _productionSessionSnapshot with
        {
            ShotsById = shots,
        };
        PlaybackState.NotifyFrameChanged();
        RefreshShotReferenceMarkers();
    }

    private (int StartFrame, int EndFrame, int DurationFrames) NavigationFrameRange()
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return (0, 0, 1);
        if (ProductionContextNode() is { Kind: ProjectTreeNodeKind.ModuleInstance } screen)
            return ScreenFrameRange(screen.Id);
        var shotDuration =
            PreparedProductionSession()
                .Shot(shotId)
                .DurationFrames;
        return (0, shotDuration - 1, shotDuration);
    }

    private (int StartFrame, int EndFrame, int DurationFrames) ActiveScreenFrameRange(string shotId)
    {
        var shot =
            PreparedProductionSession().Shot(shotId);
        var shotDuration = shot.DurationFrames;
        var index = ActiveShotSlotIndex(shotId);
        if (index < 0 || index >= shot.Screens.Count)
        {
            return (0, shotDuration - 1, shotDuration);
        }
        var screen = shot.Screens[index];
        return (
            screen.StartFrame,
            screen.StartFrame
                + screen.DurationFrames - 1,
            screen.DurationFrames);
    }

    private void SetShotPreviewFrame(int frame)
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return;
        StopShotPlayback();
        var range = NavigationFrameRange();
        var next = Math.Clamp(frame, range.StartFrame, range.EndFrame);
        if (next == _shotPreviewFrame) return;
        _shotPreviewFrame = next;
        PlaybackState.NotifyFrameChanged();
        Refresh();
    }

    private int ShotLastFrame()
    {
        var shotId = ProductionShotId();
        return !string.IsNullOrWhiteSpace(shotId)
            ? PreparedProductionSession()
                .Shot(shotId)
                .DurationFrames - 1
            : 0;
    }

    private int ActiveShotSlotIndex(string shotId)
    {
        return ProductionScreenPlaybackState.ActiveScreenIndex(
            PreparedProductionSession()
                .Shot(shotId)
                .FrameRanges,
            _shotPreviewFrame);
    }

    private void MoveShotSlot(int offset)
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return;
        var screens =
            PreparedProductionSession()
                .Shot(shotId)
                .Screens;
        var target = ActiveShotSlotIndex(shotId) + offset;
        if (target < 0 || target >= screens.Count) return;
        StopShotPlayback();
        _shotPreviewFrame =
            screens[target].StartFrame;
        PlaybackState.NotifyFrameChanged();
        Refresh();
    }

    private IReadOnlyList<int> AnimationKeyframesInCurrentScreen()
    {
        var shotId = ProductionShotId();
        if (string.IsNullOrWhiteSpace(shotId)) return [];
        var contextNode = ProductionContextNode();
        var range = contextNode?.Kind == ProjectTreeNodeKind.ModuleInstance
            ? ScreenFrameRange(contextNode.Id)
            : ActiveScreenFrameRange(shotId);
        return PreparedProductionSession()
            .Shot(shotId)
            .KeyframeFrames
            .Where((frame) => frame >= range.StartFrame && frame <= range.EndFrame)
            .ToList();
    }

    private (int StartFrame, int EndFrame, int DurationFrames) ScreenFrameRange(string moduleInstanceId)
    {
        var screen =
            PreparedProductionSession()
                .Screen(moduleInstanceId);
        var postRollFrames = ScreenPostRollFrames(moduleInstanceId);
        return (
            screen.StartFrame,
            screen.StartFrame
                + screen.DurationFrames
                + postRollFrames - 1,
            screen.DurationFrames
                + postRollFrames);
    }

    private int ScreenPostRollFrames(string moduleInstanceId)
    {
        var screen = PreparedProductionSession().Screen(moduleInstanceId);
        var screens = PreparedProductionSession().Shot(screen.ShotId).Screens;
        var index = screens
            .Select((candidate, candidateIndex) => (candidate, candidateIndex))
            .Single((candidate) =>
                candidate.candidate.ScreenId.Equals(
                    moduleInstanceId,
                    StringComparison.Ordinal))
            .candidateIndex;
        return index + 1 < screens.Count
            ? screens[index + 1].TransitionFrameCount
            : 0;
    }

    private void MoveAnimationKeyframe(int direction)
    {
        var keyframes = AnimationKeyframesInCurrentScreen();
        var target = direction < 0
            ? keyframes.LastOrDefault((frame) => frame < _shotPreviewFrame, -1)
            : keyframes.FirstOrDefault((frame) => frame > _shotPreviewFrame, -1);
        if (target < 0) return;
        SetShotPreviewFrame(target);
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
        var payloadNode = ProductionPayloadNode();
        if (payloadNode is null) return;
        InvalidatePreparedDesignPlayback();
        var navigationRange = NavigationFrameRange();
        if (_shotPreviewFrame >= navigationRange.EndFrame) _shotPreviewFrame = navigationRange.StartFrame;
        if (ShotPlaybackContainsTransparentGap(
                payloadNode,
                _shotPreviewFrame,
                navigationRange.EndFrame))
        {
            InvalidatePreparedShotPlayback();
            StartShotPlayback(shotId, navigationRange);
            return;
        }
        if (CanReusePreparedShotPlayback(
                payloadNode,
                _shotPreviewFrame,
                navigationRange.EndFrame))
        {
            PreviewDebugLog.Write(
                "preview.playback.prepared-cache-hit",
                ("kind", payloadNode.Kind),
                ("id", payloadNode.Id),
                ("frames", _preparedShotPlayback!.Frames.Count),
                ("verification", "explicit-invalidation"));
            StartShotPlayback(
                shotId,
                navigationRange);
            return;
        }

        var cancellation = _shotPlaybackPreparation.Begin();
        _shotPlaybackIsPreparing = true;
        PlaybackState.SetPlaying(true);
        PlaybackState.SetBusy(true);
        var preparationSucceeded = false;
        try
        {
            ShowPreviewLoading("Preparing playback…");
            await YieldPreviewPreparationAsync(cancellation.Token);
            var requestSignature = await ShotPlaybackRequestSignatureAsync(
                payloadNode,
                _shotPreviewFrame,
                navigationRange.EndFrame,
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            var prepared = _preparedShotPlayback;
            var reuse = PreparedPlaybackReusePolicy.Decide(
                prepared?.RequestSignature,
                requestSignature,
                HasFrameCacheReservation(
                    PlaybackFrameCacheOwner.Shot));
            if (reuse == PreparedPlaybackReuse.Complete)
            {
                preparationSucceeded = true;
                PreviewDebugLog.Write(
                    "preview.playback.prepared-cache-hit",
                    ("kind", payloadNode.Kind),
                    ("id", payloadNode.Id),
                    ("frames", prepared!.Frames.Count),
                    ("verification", "signature"));
            }
            else
            {
                IReadOnlyList<DesignPreviewPayload> frames;
                if (reuse == PreparedPlaybackReuse.Frames && prepared is not null)
                {
                    frames = prepared.Frames;
                    PreviewDebugLog.Write(
                        "preview.playback.payload-cache-hit",
                        ("kind", payloadNode.Kind),
                        ("id", payloadNode.Id),
                        ("frames", frames.Count));
                }
                else
                {
                    InvalidatePreparedShotPlayback();
                    frames = await BuildShotPlaybackFramesAsync(
                        payloadNode,
                        _shotPreviewFrame,
                        navigationRange.EndFrame,
                        cancellation.Token);
                }

                _pendingPlaybackFramesOverride = frames;
                preparationSucceeded = await PreparePlaybackFramesAsync(
                    null,
                    cancellation.Token,
                    PlaybackFrameCacheOwner.Shot);
                if (preparationSucceeded)
                {
                    _preparedShotPlayback = new PreparedProductionPlayback(
                        requestSignature,
                        payloadNode.Kind,
                        payloadNode.Id,
                        _shotPreviewFrame,
                        frames);
                }
            }
        }
        catch (OperationCanceledException)
        {
            PreviewDebugLog.Write(
                "preview.playback.prepare.cancelled",
                ("kind", payloadNode.Kind),
                ("id", payloadNode.Id));
        }
        catch (Exception error)
        {
            InvalidatePreparedShotPlayback();
            _messages.Error("Playback preparation", error);
        }
        finally
        {
            if (_shotPlaybackPreparation.Complete(cancellation))
            {
                _pendingPlaybackFramesOverride = null;
                _shotPlaybackIsPreparing = false;
                PlaybackState.SetBusy(false);
                HidePreviewLoading();
                if (!preparationSucceeded)
                {
                    PlaybackState.SetPlaying(false);
                }
            }
        }
        if (!preparationSucceeded) return;

        StartShotPlayback(
            shotId,
            navigationRange);
    }

    private bool CanReusePreparedShotPlayback(
        ProjectTreeNode node,
        int startFrame,
        int endFrame)
    {
        return _preparedShotPlayback is { } prepared
            && prepared.Covers(
                node,
                startFrame,
                endFrame)
            && HasFrameCacheReservation(
                PlaybackFrameCacheOwner.Shot);
    }

    private bool ShotPlaybackContainsTransparentGap(
        ProjectTreeNode node,
        int startFrame,
        int endFrame)
    {
        if (node.Kind != ProjectTreeNodeKind.Shot)
        {
            return false;
        }
        var ranges = PreparedProductionSession()
            .Shot(node.Id)
            .FrameRanges;
        for (var frame = startFrame; frame <= endFrame; frame++)
        {
            if (ProductionScreenPlaybackState.ActiveScreenIndex(
                    ranges,
                    frame) < 0)
            {
                return true;
            }
        }
        return false;
    }

    private void StartShotPlayback(
        string shotId,
        (int StartFrame, int EndFrame, int DurationFrames) navigationRange)
    {
        _shotPlaybackStartFrame = _shotPreviewFrame;
        _shotPlaybackStartedTimestamp = Stopwatch.GetTimestamp();
        PlaybackState.SetPlaying(true);
        _shotPlaybackTimer.Start();
        OnPlaybackStarted(new ComponentPreviewInputSession.PlaybackRunInfo(
            navigationRange.EndFrame - _shotPlaybackStartFrame + 1,
            Math.Max(
                1,
                PreparedProductionSession()
                    .Shot(shotId)
                    .FrameRate)));
        AdvanceShotPlayback();
    }

    private async Task<IReadOnlyList<DesignPreviewPayload>> BuildShotPlaybackFramesAsync(
        ProjectTreeNode payloadNode,
        int startFrame,
        int endFrame,
        CancellationToken cancellationToken)
    {
        var themeId = _selectedThemeId;
        var themeMode = _selectedMode;
        var stopwatch =
            Stopwatch.StartNew();
        var frames =
            await _operations.ExecuteAsync(
            () => _productionPayloadPreparer
                .PrepareFrames(
                    payloadNode,
                    themeId,
                    themeMode,
                    startFrame,
                    endFrame,
                    cancellationToken),
            cancellationToken);
        PreviewDebugLog.Write(
            "preview.playback.payloads.prepared",
            ("kind", payloadNode.Kind),
            ("id", payloadNode.Id),
            ("frames", frames.Count),
            ("ms", stopwatch.Elapsed.TotalMilliseconds));
        return frames;
    }

    private async Task<string> ShotPlaybackRequestSignatureAsync(
        ProjectTreeNode payloadNode,
        int startFrame,
        int endFrame,
        CancellationToken cancellationToken)
    {
        var signatureFrames =
            ShotPlaybackSignatureFrames(
                payloadNode,
                startFrame,
                endFrame);
        var themeId = _selectedThemeId;
        var themeMode = _selectedMode;
        var payloadFingerprints =
            await _operations.ExecuteAsync(
                () => signatureFrames
                    .Select((frame) =>
                        $"{frame}\u001e"
                        + PlaybackPayloadFingerprint(
                            _productionPayloadPreparer
                                .PrepareRequired(
                                    payloadNode,
                                    themeId,
                                    themeMode,
                                    frame)))
                    .ToList(),
                cancellationToken);

        var signatureJson = JsonSerializer.Serialize(new
        {
            Version = 1,
            NodeKind = payloadNode.Kind.ToString(),
            NodeId = payloadNode.Id,
            StartFrame = startFrame,
            EndFrame = endFrame,
            PlaybackRoute = _selectedPlaybackRoute,
            ThemeId = _selectedThemeId,
            ThemeMode = _selectedMode,
            DeviceId = SelectedDeviceId,
            Orientation = _selectedOrientation,
            Scale = _selectedScale,
            ShowDesignMarks = _showDesignMarks,
            ShowCanonicalFrame = _showCanonicalFrame,
            ShellIsDark = _isDark(),
            Payloads = payloadFingerprints,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureJson)));
    }

    private IReadOnlyList<int> ShotPlaybackSignatureFrames(
        ProjectTreeNode payloadNode,
        int startFrame,
        int endFrame)
    {
        if (payloadNode.Kind == ProjectTreeNodeKind.ModuleInstance)
        {
            return [startFrame];
        }

        var shotId = ProductionShotId();
        var frames = new List<int>();
        foreach (var screen in PreparedProductionSession()
                     .Shot(shotId)
                     .Screens)
        {
            var screenEnd =
                screen.StartFrame
                + screen.DurationFrames - 1;
            if (screenEnd < startFrame
                || screen.StartFrame > endFrame)
            {
                continue;
            }
            frames.Add(
                Math.Max(
                    startFrame,
                    screen.StartFrame));
        }
        return frames.Count > 0 ? frames : [startFrame];
    }

    private static string PlaybackPayloadFingerprint(DesignPreviewPayload payload)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            payload.Kind,
            payload.Name,
            payload.ConfigJson,
            payload.ThemeTokensJson,
            PaletteColors = payload.PaletteColors.OrderBy((entry) => entry.Key, StringComparer.Ordinal),
            PaletteNeutralColors = payload.PaletteNeutralColors.OrderBy((entry) => entry.Key, StringComparer.Ordinal),
            payload.ProjectMediaRoot,
            payload.ProjectMediaFiles,
            payload.IconAssetRoot,
            payload.IconMappingJson,
            payload.FontFaces,
            payload.ComponentType,
            payload.DesignPreviewJson,
            payload.RuntimeContractJson,
            payload.RuntimeRecordReferencesJson,
            payload.ThemeMode,
            payload.ComponentBaseConfigsJson,
            payload.AppConfigJson,
            payload.InstanceJson,
            payload.DeviceId,
            payload.FrameRate,
            payload.ThemeStatusBarVariantReference,
            payload.ThemeNavigationBarVariantReference,
            payload.LocalFrame,
            payload.ScreenTiming,
            payload.ScreenTransition,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
    }

    private static async Task YieldPreviewPreparationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        cancellationToken.ThrowIfCancellationRequested();
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
        var next =
            _shotPlaybackStartFrame
            + (int)Math.Floor(
                elapsed
                * Math.Max(
                    1,
                    PreparedProductionSession()
                        .Shot(shotId)
                        .FrameRate));
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
        CancelShotPlaybackPreparation();
        var wasPlaying = _shotPlaybackTimer.IsEnabled;
        if (wasPlaying) _shotPlaybackTimer.Stop();
        _shotPlaybackStartedTimestamp = 0;
        PlaybackState.SetPlaying(false);
        if (wasPlaying)
        {
            OnPlaybackStopped(new ComponentPreviewInputSession.PlaybackRunInfo(
                Math.Max(1, ShotLastFrame() - _shotPlaybackStartFrame + 1),
                Math.Max(1, ProductionShotId() is { Length: > 0 } shotId
                    ? PreparedProductionSession()
                        .Shot(shotId)
                        .FrameRate
                    : 25)));
        }
    }

    private bool IsPreviewPlaybackActive =>
        _designInputsPanel.IsPlaybackActive || _shotPlaybackTimer.IsEnabled;

    private int CurrentPlaybackFrameRate() =>
        _shotPlaybackTimer.IsEnabled
        && ProductionShotId() is { Length: > 0 } shotId
            ? PreparedProductionSession()
                .Shot(shotId)
                .FrameRate
            : _designInputsPanel.PlaybackFrameRate;

    private int CurrentNavigationFrame() =>
        PreviewWorkspace() == EditorWorkspace.Production && !string.IsNullOrWhiteSpace(ProductionShotId())
            ? _shotPreviewFrame
            : _designInputsPanel.CurrentPreviewFrame;

    private string PreviewDeviceId(DesignPreviewPayload? payload)
    {
        var shotId = ProductionShotId();
        if (!string.IsNullOrWhiteSpace(shotId))
        {
            return PreparedProductionSession().Shot(shotId).DeviceId;
        }
        return !string.IsNullOrWhiteSpace(payload?.DeviceId)
            ? payload.DeviceId
            : SelectedDeviceId ?? "";
    }

    private bool IsTransparentShotFrame(
        ProjectTreeNode? node,
        int shotFrame)
    {
        if (node?.Kind != ProjectTreeNodeKind.Shot)
        {
            return false;
        }
        var shot = PreparedProductionSession().Shot(node.Id);
        return shot.Screens.Count > 0
            && ProductionScreenPlaybackState.ActiveScreenIndex(
                shot.FrameRanges,
                shotFrame) < 0;
    }

    private void ToggleDesignPreviewContextLock()
    {
        if (_lockedPreviewContext is not null)
        {
            _lockedPreviewContext = null;
            Refresh();
            return;
        }

        var target = PreviewWorkspace() switch
        {
            EditorWorkspace.Design =>
                _activeDesignPreviewNode ?? _lastDesignPreviewNode,
            EditorWorkspace.Production =>
                ActiveProductionScreenPreviewNode(),
            _ => null,
        };
        if (target is null)
        {
            UpdateDesignContextChrome(null);
            return;
        }

        var workspace = PreviewWorkspace();
        var path = workspace == EditorWorkspace.Production
            ? ProductionNodePath(ProductionContextNode())
                .Select((node) => new PreviewContextPathItem(
                    node.Id,
                    node.Name))
                .ToArray()
            :
            [
                new PreviewContextPathItem(
                    target.Id,
                    string.IsNullOrWhiteSpace(
                        _activePreviewContextName)
                            ? target.Id
                            : _activePreviewContextName),
            ];
        _lockedPreviewContext = new PreviewContextLock(
            workspace,
            target,
            _designContextText.Text ?? "",
            path);
        Refresh();
    }

    private void UpdateDesignContextChrome(DesignPreviewPayload? payload)
    {
        if (!string.IsNullOrWhiteSpace(payload?.Name))
        {
            _activePreviewContextName = payload.Name;
        }
        var lockedContext = _lockedPreviewContext;
        _activeProductionModuleInstanceId = RuntimeContextValue(payload, "moduleInstanceId");
        var resolvedContextText = PreviewWorkspace() == EditorWorkspace.Production
            && ProductionContextNode()?.Kind == ProjectTreeNodeKind.Shot
            && !string.IsNullOrWhiteSpace(payload?.Name)
                ? $"Active Screen: {payload.Name}"
                : payload?.Name ?? "";
        _designContextText.Text = string.IsNullOrWhiteSpace(
                resolvedContextText)
            ? lockedContext?.ContextText ?? ""
            : resolvedContextText;
        var productionNodes = ProductionNodePath(
            ProductionContextNode());
        var previewItems = lockedContext is not null
            ? lockedContext.Path.Select((item, index) =>
                new EditorBreadcrumbItem(
                    item.Name,
                    index == lockedContext.Path.Count - 1
                        ? null
                        : () => _selectNodeById(item.Id)))
            : PreviewWorkspace() == EditorWorkspace.Production
                ? productionNodes
                    .Select((node, index) => new EditorBreadcrumbItem(
                        node.Name,
                        index == productionNodes.Count - 1
                            ? null
                            : () => _selectNodeById(node.Id)))
                :
                [
                    new EditorBreadcrumbItem(
                        string.IsNullOrWhiteSpace(payload?.Name)
                            ? "Preview"
                            : payload.Name),
                ];
        EditorBreadcrumbBar.Render(
            _previewTitle,
            previewItems.Any() ? previewItems : [new EditorBreadcrumbItem("Production Preview")]);
        _designContextText.IsVisible = !string.IsNullOrWhiteSpace(_designContextText.Text);
        _designContextText.Foreground = EditorNavigationVisuals.VariantLockBrush(true);
        _designContextText.Opacity = 1;
        var productionContext = !string.IsNullOrWhiteSpace(ProductionShotId());
        _designContextAddHistoryButton.IsVisible = true;
        _designContextLockButton.IsVisible = true;
        RefreshDesignContextHistoryChrome();
        ToolTip.SetTip(
            _designContextText,
            _activeDesignPreviewNode is not null
                ? productionContext ? "Open the active module instance" : "Open this component variant in the editor"
                : null);

        _designContextLockButton.IsEnabled = _activeDesignPreviewNode is not null
            || _lastDesignPreviewNode is not null
            || _lockedPreviewContext is not null
            || ActiveProductionScreenPreviewNode() is not null;
        if (_renderedLockState != (_lockedPreviewContext is not null))
        {
            _designContextLockButton.Content = EditorIcons.CreateSemantic(
                _lockedPreviewContext is not null ? "Release preview context" : "Keep current preview context",
                _lockedPreviewContext is not null ? EditorIcons.Lock : EditorIcons.Unlock,
                15);
            _renderedLockState = _lockedPreviewContext is not null;
        }
        if (_designContextLockButton.Content is Control lockIcon)
        {
            EditorIcons.ApplyBrush(
                lockIcon,
                _lockedPreviewContext is not null
                    ? EditorNavigationVisuals.VariantLockBrush(true)
                    : null);
        }

        ToolTip.SetTip(
            _designContextLockButton,
            _lockedPreviewContext is not null
                ? "Release preview context"
                : "Keep current preview context");
    }

    private async void NavigateToActiveDesignContext()
    {
        var workspace = PreviewWorkspace();
        var targetId = !string.IsNullOrWhiteSpace(
                _activeProductionModuleInstanceId)
            ? _activeProductionModuleInstanceId
            : (_activeDesignPreviewNode
                ?? LockedNode(workspace)
                ?? _lastDesignPreviewNode)?.Id;
        if (string.IsNullOrWhiteSpace(targetId)) return;

        try
        {
            if (!await _navigateNodeInWorkspace(
                    workspace,
                    targetId))
            {
                _messages.Warning(
                    "Preview context",
                    $"The exact Preview editor '{targetId}' is unavailable.");
            }
        }
        catch (Exception error)
        {
            _messages.Error("Preview context", error);
        }
    }

    private string ProductionShotId()
    {
        return ProductionContextNode() switch
        {
            { Kind: ProjectTreeNodeKind.Shot } shot => shot.Id,
            { Kind: ProjectTreeNodeKind.ModuleInstance } instance =>
                _productionSessionSnapshot?
                    .Screen(instance.Id)
                    .ShotId
                ?? "",
            _ => "",
        };
    }

    private ProjectTreeNode? ProductionContextNode()
    {
        if (LockedNode(EditorWorkspace.Production) is { } locked)
        {
            return locked.ToNode();
        }
        if (_workspace != EditorWorkspace.Production)
        {
            return null;
        }
        var selected = _selectedNode();
        return selected?.Kind is ProjectTreeNodeKind.Shot or ProjectTreeNodeKind.ModuleInstance
            ? selected
            : null;
    }

    private ProjectTreeNode? ProductionPayloadNode() => ProductionContextNode();

    private EditorWorkspace PreviewWorkspace() =>
        _lockedPreviewContext?.Workspace ?? _workspace;

    private PreviewNodeKey? LockedNode(EditorWorkspace workspace) =>
        _lockedPreviewContext?.Workspace == workspace
            ? _lockedPreviewContext.Node
            : null;

    private ProjectTreeNode? LockedContextNode() =>
        _lockedPreviewContext is { } locked
            ? locked.Node.ToNode()
            : null;

    private PreviewNodeKey? ActiveProductionScreenPreviewNode()
    {
        var context = ProductionContextNode();
        if (context?.Kind == ProjectTreeNodeKind.ModuleInstance)
        {
            return PreviewNodeKey.From(context);
        }
        if (context?.Kind != ProjectTreeNodeKind.Shot)
        {
            return null;
        }

        var shot = PreparedProductionSession().Shot(context.Id);
        var index = ActiveShotSlotIndex(context.Id);
        return index >= 0 && index < shot.Screens.Count
            ? new PreviewNodeKey(
                ProjectTreeNodeKind.ModuleInstance,
                shot.Screens[index].ScreenId)
            : null;
    }

    private static string RuntimeContextValue(DesignPreviewPayload? payload, string key)
    {
        if (payload is null) return "";
        var instance = DesignPreviewTestValues.Parse(payload.InstanceJson);
        return (instance["context"] as JsonObject)?[key]?.GetValue<string>() ?? "";
    }

    private DesignPreviewPayload ProcessDesignPreviewPayload(
        DesignPreviewPayload payload)
    {
        _designInputsPanel.UpdateForPayload(
            payload,
            _projectId);
        var resolved = _designInputsPanel.ApplyInputs(
            payload,
            _selectedMode,
            _projectId);
        PlaybackState.NotifyFrameChanged();
        return resolved;
    }

    private void UpdateProductionPreviewSetup()
    {
        var production = PreviewWorkspace() == EditorWorkspace.Production;
        if (_previewSetupBorder is { } previewSetupBorder)
        {
            previewSetupBorder.Padding = production
                ? new Thickness(12, 12, 12, 0)
                : new Thickness(12);
        }
        UpdateOrientationPlacement(production);
        UpdateProductionContextStrip(production);
        if (_deviceField is { } deviceField) deviceField.IsVisible = !production;
        if (_themeField is { } themeField) themeField.IsVisible = !production;
        if (_modeField is { } modeField) modeField.IsVisible = !production;
        if (_previewSetupGrid is { } setupGrid)
        {
            _previewSetupLayoutMode = null;
            ApplyPreviewSetupLayout(setupGrid.Bounds.Width);
        }
        if (_modeComboBox.ItemsSource is IEnumerable<FieldOption> modeOptions)
        {
            _isRefreshingOptions = true;
            _modeComboBox.SelectedItem = modeOptions.FirstOrDefault((option) =>
                option.Value == _selectedMode);
            _isRefreshingOptions = false;
        }
        _modeComboBox.IsEnabled = true;
        ToolTip.SetTip(_modeComboBox, null);
    }

    private void UpdateOrientationPlacement(bool production)
    {
        if (_previewSetupGrid is not { } setupGrid
            || _previewPrimaryControls is not { } primaryControls
            || _orientationField is not { } orientationField)
        {
            return;
        }

        if (_orientationComboBox.Parent is Panel comboParent)
        {
            comboParent.Children.Remove(_orientationComboBox);
        }
        if (orientationField.Parent is Panel fieldParent)
        {
            fieldParent.Children.Remove(orientationField);
        }

        if (production)
        {
            _orientationComboBox.MinWidth = 96;
            _orientationComboBox.MaxWidth = 112;
            _orientationComboBox.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            Grid.SetColumn(_orientationComboBox, 7);
            Grid.SetRow(_orientationComboBox, 0);
            primaryControls.Children.Add(_orientationComboBox);
            return;
        }

        _orientationComboBox.MinWidth = 0;
        _orientationComboBox.MaxWidth = double.PositiveInfinity;
        _orientationComboBox.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        orientationField.Children.Add(_orientationComboBox);
        orientationField.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        setupGrid.Children.Add(orientationField);
    }

    private void ApplyPreviewSetupLayout(double availableWidth)
    {
        if (_previewSetupGrid is not { } setupGrid
            || PreviewWorkspace() == EditorWorkspace.Production)
        {
            return;
        }
        if (_deviceField is not { } deviceField
            || _themeField is not { } themeField
            || _modeField is not { } modeField
            || _orientationField is not { } orientationField
            || !ReferenceEquals(orientationField.Parent, setupGrid))
        {
            return;
        }

        var layoutMode = PreviewPanelLayoutPolicy.SetupMode(availableWidth);
        if (_previewSetupLayoutMode == layoutMode)
        {
            return;
        }
        _previewSetupLayoutMode = layoutMode;
        switch (layoutMode)
        {
            case PreviewSetupLayoutMode.FourColumns:
                setupGrid.ColumnDefinitions = new ColumnDefinitions("*,*,Auto,Auto");
                setupGrid.RowDefinitions = new RowDefinitions("Auto");
                setupGrid.RowSpacing = 0;
                modeField.MinWidth = 112;
                orientationField.MinWidth = 132;
                PlaceSetupField(deviceField, 0, 0);
                PlaceSetupField(themeField, 1, 0);
                PlaceSetupField(modeField, 2, 0);
                PlaceSetupField(orientationField, 3, 0);
                break;
            case PreviewSetupLayoutMode.TwoColumns:
                setupGrid.ColumnDefinitions = new ColumnDefinitions("*,*");
                setupGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
                setupGrid.RowSpacing = 10;
                modeField.MinWidth = 112;
                orientationField.MinWidth = 132;
                PlaceSetupField(deviceField, 0, 0);
                PlaceSetupField(themeField, 1, 0);
                PlaceSetupField(modeField, 0, 1);
                PlaceSetupField(orientationField, 1, 1);
                break;
            case PreviewSetupLayoutMode.OneColumn:
                setupGrid.ColumnDefinitions = new ColumnDefinitions("*");
                setupGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto");
                setupGrid.RowSpacing = 10;
                modeField.MinWidth = 0;
                orientationField.MinWidth = 0;
                PlaceSetupField(deviceField, 0, 0);
                PlaceSetupField(themeField, 0, 1);
                PlaceSetupField(modeField, 0, 2);
                PlaceSetupField(orientationField, 0, 3);
                break;
            default:
                throw new InvalidOperationException($"Unsupported Preview Setup layout '{layoutMode}'.");
        }
    }

    private static void PlaceSetupField(Control field, int column, int row)
    {
        Grid.SetColumn(field, column);
        Grid.SetRow(field, row);
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
            index == pathNodes.Count - 1 ? null : () => _selectNodeById(node.Id))).ToList();
        var shotId = ProductionShotId();
        var hasShotContext = !string.IsNullOrWhiteSpace(shotId);
        var actorName = "";
        var device = "";
        var theme = "";
        var mode = "";
        if (hasShotContext)
        {
            var inherited =
                PreparedProductionSession()
                    .Shot(shotId)
                    .Context;
            actorName = inherited.Actor;
            device = inherited.Device;
            theme = inherited.Theme;
            mode = EditorUiText.IdentifierLabel(_selectedMode);
        }
        ProductionPreviewContextStrip.Render(
            _productionContextHost,
            new ProductionPreviewContextMetadata(
                path,
                actorName,
                device,
                theme,
                mode,
                ToggleProductionPreviewMode,
                hasShotContext));
    }

    private void ToggleProductionPreviewMode()
    {
        if (_modeComboBox.ItemsSource is not IEnumerable<FieldOption> modeOptions)
        {
            return;
        }
        var nextMode = _selectedMode == "light" ? "dark" : "light";
        _modeComboBox.SelectedItem = modeOptions.FirstOrDefault((option) =>
            option.Value == nextMode);
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
        var context =
            PreparedProductionSession()
                .Shot(shotId)
                .Context;
        return context.IsValid
            ? null
            : new PreviewContextState(
                PreviewContextStateKind.Error,
                "Shot context is incomplete",
                context.Error);
    }


    private void EnsureSelectedOptionsExist()
    {
        if (string.IsNullOrWhiteSpace(_projectId) || _isRefreshingOptions)
        {
            return;
        }

        if (_visualContextSnapshot is not { } snapshot
            || !snapshot.ProjectId.Equals(
                _projectId,
                StringComparison.Ordinal))
        {
            return;
        }

        var deviceOptions = snapshot.DeviceOptions;
        var selectedDevice = PreferredResourceOption(deviceOptions, SelectedDeviceId);

        var themeOptions = snapshot.ThemeOptions;
        var selectedTheme = PreferredResourceOption(themeOptions, _selectedThemeId);

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

    private string PreparedMediaRoot()
    {
        return _visualContextSnapshot is { } snapshot
            && snapshot.ProjectId.Equals(
                _projectId,
                StringComparison.Ordinal)
                ? snapshot.MediaRoot
                : "";
    }

    private ProductionPreviewSessionSnapshot
        PreparedProductionSession()
    {
        return _productionSessionSnapshot
            ?? throw new InvalidOperationException(
                "Production Preview requires its prepared session snapshot.");
    }

    private DevicePreviewMetrics PreparedDeviceMetrics(
        string deviceId,
        DesignPreviewPayload? payload)
    {
        var shotId = ProductionPayloadShotId(payload);
        if (string.IsNullOrWhiteSpace(shotId))
        {
            shotId = ProductionShotId();
        }
        if (!string.IsNullOrWhiteSpace(shotId))
        {
            var screenId = RuntimeContextValue(payload, "moduleInstanceId");
            if (string.IsNullOrWhiteSpace(screenId))
            {
                var shot = PreparedProductionSession().Shot(shotId);
                if (!shot.DeviceId.Equals(deviceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Production Preview Shot '{shotId}' resolved Device '{shot.DeviceId}', not '{deviceId}'.");
                }
                return shot.DeviceMetrics;
            }
            var screen = PreparedProductionSession().Screen(screenId);
            if (!screen.DeviceId.Equals(
                    deviceId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Production Preview Screen '{screenId}' resolved Device '{screen.DeviceId}', not '{deviceId}'.");
            }
            return screen.DeviceMetrics;
        }
        if (_visualContextSnapshot is not { } snapshot
            || !snapshot.ProjectId.Equals(
                _projectId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Preview Project '{_projectId}' requires its prepared visual context.");
        }

        return snapshot.DeviceMetrics(deviceId);
    }

    internal static string ProductionPayloadShotId(
        DesignPreviewPayload? payload)
    {
        if (payload?.Kind != "moduleInstance")
        {
            return "";
        }
        var shotId = RuntimeContextValue(
            payload,
            "shotId");
        if (string.IsNullOrWhiteSpace(shotId))
        {
            throw new InvalidOperationException(
                "Production Preview payload requires its exact Shot id before resolving Device metrics.");
        }
        return shotId;
    }

    internal static FieldOption? PreferredResourceOption(
        IReadOnlyList<FieldOption> options,
        string? selectedValue)
    {
        return options.FirstOrDefault((option) => option.Value.Equals(selectedValue, StringComparison.Ordinal))
            ?? options.FirstOrDefault();
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

    private sealed record PreviewContextLock(
        EditorWorkspace Workspace,
        PreviewNodeKey Node,
        string ContextText,
        IReadOnlyList<PreviewContextPathItem> Path);

    private sealed record PreviewContextPathItem(
        string Id,
        string Name);

    private sealed record DesignPreviewHistoryEntry(
        PreviewNodeKey Key,
        string Name);
}
