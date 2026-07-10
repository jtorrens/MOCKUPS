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
    private readonly ToggleSwitch _marksToggle = new()
    {
        IsChecked = false,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
    private readonly IEditorShellMessageSink _messages;
    private readonly Func<bool> _isDark;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly Func<EditorViewState?> _captureCurrentEditorViewState;
    private readonly Func<string, EditorViewState?, bool> _selectNodeById;
    private readonly TextBlock _designContextText;
    private readonly Button _designContextHistoryButton;
    private readonly Button _designContextAddHistoryButton;
    private readonly Button _designContextLockButton;
    private readonly Popup _designContextHistoryPopup;
    private readonly StackPanel _designContextHistoryItems = new() { Spacing = 1 };
    private readonly DesignWebPreviewPane _designPreviewPane = new();
    private readonly ComponentInputsPanel _designInputsPanel;
    private readonly ContentControl _previewBusyHost;
    private readonly EditorLoadingScrim _previewLoadingScrim = new();
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
    private string? _projectId;
    private string? _selectedThemeId;
    private PreviewNodeKey? _lastDesignPreviewNode;
    private PreviewNodeKey? _activeDesignPreviewNode;
    private PreviewNodeKey? _lockedDesignPreviewNode;
    private readonly List<DesignPreviewHistoryEntry> _designHistory = [];
    private string _selectedMode = "light";
    private string _selectedOrientation = "portrait";
    private string _selectedScale = "fit";
    private bool _showDesignMarks;
    private bool _isDesignPreviewContextLocked;
    private bool _isRefreshingOptions;
    private bool? _renderedLockState;
    private CancellationTokenSource? _previewLoadingCancellation;
    private CancellationTokenSource? _aheadPreloadCancellation;
    private readonly HashSet<string> _aheadPreloadedFrameKeys = new(StringComparer.Ordinal);
    private bool _isAheadPreloading;

    public EditorPreviewController(
        SpikeDatabase database,
        EditorInstantComboBox deviceComboBox,
        EditorInstantComboBox themeComboBox,
        EditorInstantComboBox modeComboBox,
        EditorInstantComboBox orientationComboBox,
        IEditorShellMessageSink messages,
        ContentControl previewSetupHost,
        ContentControl previewInputsHost,
        ContentControl previewBusyHost,
        ContentControl designPreviewHost,
        TextBlock designContextText,
        Button designContextHistoryButton,
        Button designContextAddHistoryButton,
        Button designContextLockButton,
        Func<bool> isDark,
        Func<ProjectTreeNode?> selectedNode,
        Func<EditorViewState?> captureCurrentEditorViewState,
        Func<string, EditorViewState?, bool> selectNodeById,
        Window owner)
    {
        _database = database;
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
        _designContextHistoryPopup = CreateDesignContextHistoryPopup();
        _previewBusyHost = previewBusyHost;
        _previewBusyHost.Content = _previewLoadingScrim;
        _previewBusyHost.IsVisible = false;
        _designInputsPanel = new ComponentInputsPanel(database, Refresh, owner, PreparePlaybackFramesAsync);
        _designPreviewPane.FrameStatusChanged += OnDesignPreviewFrameStatusChanged;

        WrapPreviewSetup(previewSetupHost);
        previewInputsHost.Content = _designInputsPanel;
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
        var key = _activeDesignPreviewNode ?? _lockedDesignPreviewNode ?? _lastDesignPreviewNode;
        if (key is null)
        {
            return;
        }

        var payload = DesignPreviewPayloadFactory.Create(_database, key.ToNode(), _selectedThemeId, _selectedMode);
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
        if (_designHistory.Count == 0)
        {
            return;
        }

        RenderDesignContextHistoryItems();
        _designContextHistoryPopup.IsOpen = !_designContextHistoryPopup.IsOpen;
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
        _designHistory.RemoveAll((entry) => entry.Key.Id.Equals(key.Id, StringComparison.Ordinal));
        _designHistory.Insert(0, new DesignPreviewHistoryEntry(key, name, viewState));
        if (_designHistory.Count > 10)
        {
            _designHistory.RemoveRange(10, _designHistory.Count - 10);
        }
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

    private void RefreshDesignContextHistoryChrome()
    {
        _designContextHistoryButton.IsEnabled = _designHistory.Count > 0;
        _designContextHistoryButton.Opacity = _designHistory.Count > 0 ? 1 : 0.38;
        var canAddCurrentContext = _activeDesignPreviewNode is not null
            || _lockedDesignPreviewNode is not null
            || _lastDesignPreviewNode is not null;
        _designContextAddHistoryButton.IsEnabled = canAddCurrentContext;
        _designContextAddHistoryButton.Opacity = canAddCurrentContext ? 1 : 0.38;
        ToolTip.SetTip(
            _designContextHistoryButton,
            _designHistory.Count > 0 ? "Recent design contexts" : "No recent design contexts");
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
            EditorIcons.Create(EditorIcons.Design, 18)));

        var actions = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        actions.Children.Add(_scaleComboBox);
        actions.Children.Add(_marksToggle);
        ToolTip.SetTip(_previewPerformanceDot, "Preview frame status");
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
                    Child = setupContent,
                },
                isExpanded: true),
        };
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
            _marksToggle.IsChecked = _showDesignMarks;
        }
        finally
        {
            _isRefreshingOptions = false;
        }

        Refresh();
    }

    private void AttachControlEvents()
    {
        _deviceComboBox.SelectionChanged += (_, _) => OnDeviceChanged();
        _themeComboBox.SelectionChanged += (_, _) => OnThemeChanged();
        _modeComboBox.SelectionChanged += (_, _) => OnModeChanged();
        _orientationComboBox.SelectionChanged += (_, _) => OnOrientationChanged();
        _scaleComboBox.SelectionChanged += (_, _) => OnScaleChanged();
        _marksToggle.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleSwitch.IsCheckedProperty)
            {
                OnMarksChanged();
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

    private void OnMarksChanged()
    {
        _showDesignMarks = _marksToggle.IsChecked == true;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        try
        {
            EnsureSelectedOptionsExist();
            var designPayload = DesignPreviewPayloadForSelection();
            var deviceId = PreviewDeviceId(designPayload);
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                _messages.Warning("Preview", "No device selected.");
                return;
            }

            var metrics = ApplyPreviewOrientation(_database.GetDevicePreviewMetrics(deviceId));
            var themeName = _themeComboBox.SelectedItem?.Label ?? "No theme";
            _designInputsPanel.UpdateForPayload(designPayload, _projectId);
            designPayload = designPayload is null
                ? null
                : _designInputsPanel.ApplyInputs(designPayload, _selectedMode, _projectId);
            UpdateDesignContextChrome(designPayload);
            _designPreviewPane.Update(
                metrics,
                _isDark(),
                themeName,
                _selectedMode,
                _selectedScale,
                _showDesignMarks,
                designPayload,
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
        _designInputsPanel.TriggerAction(actionId);
    }

    public void SetDesignPreviewTestValue(string jsonKey, string value)
    {
        _designInputsPanel.SetExternalInputValue(jsonKey, value);
    }

    private async Task<bool> PreparePlaybackFramesAsync()
    {
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
        var payload = _designInputsPanel.ApplyInputs(designPayload, _selectedMode, _projectId);
        var projectFps = payload.FrameRate;
        var previewFps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        var frames = PlaybackFramePayloads(payload, projectFps).ToList();
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
                $"Buffering {Math.Min(frames.Count, InitialPlaybackPreloadFrames)} of {frames.Count} frames...",
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
            WebDesignPreviewRenderer.ReserveFrameCacheCapacity(Math.Min(frames.Count, InitialPlaybackPreloadFrames + AheadPlaybackPreloadFrames));
            var initialFrames = frames.Take(InitialPlaybackPreloadFrames).ToList();
            foreach (var frame in initialFrames)
            {
                _aheadPreloadedFrameKeys.Add(PlaybackFrameKey(frame));
            }

            await PreloadPlaybackFramesAsync(
                metrics,
                payload,
                initialFrames,
                cancellation.Token,
                "initial");
            PreviewDebugLog.Write(
                "preview.playback.frames.end",
                ("component", payload.ComponentType),
                ("name", payload.Name),
                ("frames", initialFrames.Count),
                ("totalFrames", frames.Count),
                ("ms", totalStopwatch.Elapsed.TotalMilliseconds));
            return true;
        }
        catch (OperationCanceledException)
        {
            PreviewDebugLog.Write(
                "preview.playback.frames.cancelled",
                ("component", payload.ComponentType),
                ("name", payload.Name),
                ("frames", frames.Count),
                ("ms", totalStopwatch.Elapsed.TotalMilliseconds));
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
            var bodyContent = await WebDesignPreviewRenderer.RenderBodyAsync(
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
        _designPreviewPane.IsVisible = false;
        _previewBusyHost.IsVisible = true;
        _previewLoadingScrim.Show(message, cancel);
        SetPreviewPerformanceStatus(PreviewPerformanceStatus.Loading);
    }

    private void HidePreviewLoading()
    {
        _previewLoadingScrim.Hide();
        _previewBusyHost.IsVisible = false;
        _designPreviewPane.IsVisible = true;
        if (!_designInputsPanel.IsPlaybackActive)
        {
            SetPreviewPerformanceStatus(PreviewPerformanceStatus.Idle);
        }
    }

    private void OnDesignPreviewFrameStatusChanged(DesignWebPreviewPane.DesignPreviewFrameStatus status)
    {
        if (!_designInputsPanel.IsPlaybackActive)
        {
            SetPreviewPerformanceStatus(PreviewPerformanceStatus.Idle);
            return;
        }

        var frameBudgetMs = 1000.0 / Math.Max(1, _designInputsPanel.PlaybackFrameRate);
        var keepsFrameRate = status.RenderError is false
            && status.IsAnimationOnly
            && status.UsedDomPatch
            && status.ElapsedMilliseconds <= frameBudgetMs;
        SetPreviewPerformanceStatus(keepsFrameRate ? PreviewPerformanceStatus.Good : PreviewPerformanceStatus.Slow);
    }

    private void SetPreviewPerformanceStatus(PreviewPerformanceStatus status)
    {
        _previewPerformanceDot.Background = status switch
        {
            PreviewPerformanceStatus.Loading => PreviewStatusLoadingBrush,
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
        Good,
        Slow,
    }

    private static IEnumerable<DesignPreviewPayload> PlaybackFramePayloads(DesignPreviewPayload payload, int projectFps)
    {
        var fps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        if (JsonNode.Parse(string.IsNullOrWhiteSpace(payload.DesignPreviewJson) ? "{}" : payload.DesignPreviewJson) is not JsonObject preview)
        {
            yield break;
        }

        var action = PlaybackFrameAction(preview);
        if (action is null)
        {
            yield break;
        }

        var timeJsonKey = action.TimeJsonKey;
        var durationInputId = action.DurationInputId;
        var animationDurationSeconds = action.DurationSeconds;
        if (string.IsNullOrWhiteSpace(timeJsonKey)
            || (string.IsNullOrWhiteSpace(durationInputId) && animationDurationSeconds <= 0))
        {
            yield break;
        }

        var frameCount = PlaybackDurationFrames(action, preview, fps);
        if (frameCount <= 0)
        {
            yield break;
        }

        for (var frame = 0; frame <= frameCount; frame++)
        {
            var framePreview = JsonNode.Parse(preview.ToJsonString()) as JsonObject ?? new JsonObject();
            framePreview[timeJsonKey] = action.TimeUnit == ComponentPreviewActionTimeUnit.Frames
                ? frame
                : frame / (double)fps;
            framePreview[action.PlayInputId] = true;
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

        var durationFrames = PlaybackDurationFrames(action, preview, fps);
        if (durationFrames <= 0)
        {
            yield break;
        }

        var currentFrame = action.TimeUnit == ComponentPreviewActionTimeUnit.Frames
            ? Math.Max(0, (int)Math.Floor(JsonNumber(preview, action.TimeJsonKey, 0)))
            : Math.Max(0, (int)Math.Floor(JsonNumber(preview, action.TimeJsonKey, 0) * fps));
        for (var index = 1; index <= AheadPlaybackPreloadFrames * 2; index++)
        {
            var frame = currentFrame + index;
            if (frame > durationFrames)
            {
                yield break;
            }

            var framePreview = JsonNode.Parse(preview.ToJsonString()) as JsonObject ?? new JsonObject();
            framePreview[action.TimeJsonKey] = action.TimeUnit == ComponentPreviewActionTimeUnit.Frames
                ? frame
                : frame / (double)fps;
            framePreview[action.PlayInputId] = true;
            yield return payload with { DesignPreviewJson = framePreview.ToJsonString() };
        }
    }

    private static int PlaybackDurationFrames(ComponentPreviewActionDefinition action, JsonObject preview, int fps)
    {
        if (action.TimeUnit == ComponentPreviewActionTimeUnit.Frames)
        {
            return Math.Max(0, (int)Math.Round(JsonNumber(preview, action.DurationInputId, 0), MidpointRounding.AwayFromZero));
        }

        var duration = action.DurationSeconds > 0
            ? action.DurationSeconds
            : Math.Max(0, JsonNumber(preview, action.DurationInputId, 0));
        return duration <= 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(duration * Math.Max(1, fps)));
    }

    private static string PlaybackFrameKey(DesignPreviewPayload payload)
    {
        return string.Join(
            "\u001f",
            payload.ComponentType,
            payload.Name,
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

            return preview[action.TimeJsonKey]?.ToJsonString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static ComponentPreviewActionDefinition? PlaybackFrameAction(JsonObject preview)
    {
        var actions = ComponentPreviewActions.Read(preview);
        return actions.FirstOrDefault((action) => JsonBoolean(preview, action.PlayInputId))
            ?? actions.FirstOrDefault();
    }

    private static bool JsonBoolean(JsonObject owner, string key)
    {
        if (owner[key] is not JsonValue value)
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
        };
    }

    private DesignPreviewPayload? DesignPreviewPayloadForSelection()
    {
        if (_isDesignPreviewContextLocked && _lockedDesignPreviewNode is not null)
        {
            var lockedPayload = DesignPreviewPayloadFactory.Create(_database, _lockedDesignPreviewNode.ToNode(), _selectedThemeId, _selectedMode);
            if (lockedPayload is not null)
            {
                _activeDesignPreviewNode = _lockedDesignPreviewNode;
                return lockedPayload;
            }

            _isDesignPreviewContextLocked = false;
            _lockedDesignPreviewNode = null;
        }

        var selectedNode = _selectedNode();
        var selectedPayload = DesignPreviewPayloadFactory.Create(_database, selectedNode, _selectedThemeId, _selectedMode);
        if (selectedPayload is not null && selectedNode is not null)
        {
            _lastDesignPreviewNode = PreviewNodeKey.From(selectedNode);
            _activeDesignPreviewNode = _lastDesignPreviewNode;
            return selectedPayload;
        }

        if (_lastDesignPreviewNode is null)
        {
            _activeDesignPreviewNode = null;
            return null;
        }

        var fallbackPayload = DesignPreviewPayloadFactory.Create(_database, _lastDesignPreviewNode.ToNode(), _selectedThemeId, _selectedMode);
        _activeDesignPreviewNode = fallbackPayload is null ? null : _lastDesignPreviewNode;
        return fallbackPayload;
    }

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
        _designContextText.Text = payload?.Name ?? "";
        _designContextText.IsVisible = !string.IsNullOrWhiteSpace(_designContextText.Text);
        _designContextText.Foreground = EditorNavigationVisuals.VariantLockBrush(true);
        _designContextText.Opacity = 1;
        RefreshDesignContextHistoryChrome();
        ToolTip.SetTip(
            _designContextText,
            _activeDesignPreviewNode is not null
                ? "Open this component variant in the editor"
                : null);

        _designContextLockButton.IsEnabled = _activeDesignPreviewNode is not null
            || _lastDesignPreviewNode is not null
            || _lockedDesignPreviewNode is not null;
        if (_renderedLockState != _isDesignPreviewContextLocked)
        {
            _designContextLockButton.Content = EditorIcons.Create(
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
        var node = _activeDesignPreviewNode ?? _lockedDesignPreviewNode ?? _lastDesignPreviewNode;
        if (node is null)
        {
            return;
        }

        _selectNodeById(node.Id, null);
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
