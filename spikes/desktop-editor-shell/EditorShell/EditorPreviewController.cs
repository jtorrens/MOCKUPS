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
    private readonly ComponentPreviewInputSession _designInputsPanel;
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
    public PreviewPlaybackState PlaybackState { get; } = new();
    private string? _projectId;
    private string? _selectedThemeId;
    public string? SelectedThemeId => _selectedThemeId;
    public event Action? ThemeChanged;
    private PreviewNodeKey? _lastDesignPreviewNode;
    private PreviewNodeKey? _activeDesignPreviewNode;
    private PreviewNodeKey? _lockedDesignPreviewNode;
    private readonly List<DesignPreviewHistoryEntry> _designHistory = [];
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
    private int _playbackSummaryGeneration;

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
        _designContextHistoryPopup = CreateDesignContextHistoryPopup();
        _previewBusyHost = previewBusyHost;
        _previewBusyHost.Content = _previewLoadingScrim;
        _previewBusyHost.IsVisible = false;
        _designInputsPanel = new ComponentPreviewInputSession(database, Refresh, PreparePlaybackFramesAsync);
        _designPreviewPane.FrameStatusChanged += OnDesignPreviewFrameStatusChanged;
        _designInputsPanel.PlaybackStarted += OnPlaybackStarted;
        _designInputsPanel.PlaybackStopped += OnPlaybackStopped;
        _designInputsPanel.PlaybackBusyChanged += PlaybackState.SetBusy;

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
                            setupContent,
                        },
                    },
                },
                isExpanded: true),
        };
    }

    private void CreatePreviewControls(ContentControl previewControlsHost)
    {
        ToolTip.SetTip(_marksToggle, "Show design markers");
        ToolTip.SetTip(_canonicalFrameToggle, "Show canonical 360 × 800 frame without the device layer");

        var primaryControls = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                _scaleComboBox,
                _playbackRouteComboBox,
                LabeledToggle("Markers", _marksToggle),
                LabeledToggle("360", _canonicalFrameToggle),
                _referenceViewComboBox,
            },
        };

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

        previewControlsHost.Content = new GlassCard
        {
            Content = new InstantEditorCard(
                EditorCardHeader.Create(
                    "Preview controls",
                    "Display and reference",
                    EditorIcons.Create(EditorIcons.Design, 18)),
                new Border
                {
                    Padding = new Thickness(12, 0, 12, 12),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            primaryControls,
                            _referenceSplitControls,
                        },
                    },
                },
                isExpanded: true),
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
        _referenceStartPreviewFrame = _designInputsPanel.CurrentPreviewFrame;
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
        Math.Max(0, _designInputsPanel.CurrentPreviewFrame - _referenceStartPreviewFrame),
        _designInputsPanel.PlaybackFrameRate,
        string.IsNullOrWhiteSpace(_projectId) ? "" : _database.GetProjectSettings(_projectId).MediaRoot);

    public void Refresh()
    {
        try
        {
            // Static preview changes (including reference images and design marks)
            // must never inherit a playback preparation overlay.
            if (!_designInputsPanel.IsPlaybackActive)
            {
                HidePreviewLoading();
            }
            EnsureSelectedOptionsExist();
            var designPayload = DesignPreviewPayloadForSelection();
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
            _designInputsPanel.UpdateForPayload(designPayload, _projectId);
            designPayload = designPayload is null
                ? null
                : _designInputsPanel.ApplyInputs(designPayload, _selectedMode, _projectId);
            UpdateDesignContextChrome(designPayload);
            if (_selectedPlaybackRoute == "raster"
                && designPayload is not null
                && _designInputsPanel.IsPlaybackActive
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

    private async Task<bool> PreparePlaybackFramesAsync(ComponentPreviewActionDefinition requestedAction)
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
        var frames = PlaybackFramePayloads(payload, projectFps, requestedAction).ToList();
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
        if (_selectedPlaybackRoute != "raster")
        {
            var prewarmStopwatch = Stopwatch.StartNew();
            ShowPreviewLoading($"Preparing HTML 0 / {frames.Count} frames…", () => { });
            try
            {
                WebDesignPreviewRenderer.ReserveFrameCacheCapacity(frames.Count);
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
            await _designPreviewPane.PrepareRasterPlaybackAsync(_rasterPlaybackOrder, CancellationToken.None);
            await _designPreviewPane.SyncRasterViewportAsync();
            PreviewDebugLog.Write(
                "preview.playback.raster-cache-hit",
                ("component", payload.ComponentType),
                ("name", payload.Name),
                ("frames", frames.Count));
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
            WebDesignPreviewRenderer.ReserveFrameCacheCapacity(frames.Count);
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
        if (!_designInputsPanel.IsPlaybackActive)
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
        if (!_designInputsPanel.IsPlaybackActive)
        {
            SetPreviewPerformanceStatus(PreviewPerformanceStatus.Idle);
            return;
        }
        if (actualFps is null)
        {
            SetPreviewPerformanceStatus(PreviewPerformanceStatus.Loading);
            return;
        }
        var targetFps = Math.Max(1, _designInputsPanel.PlaybackFrameRate);
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
        if (_selectedPlaybackRoute == "raster" && _rasterPlaybackOrder.Count > 0)
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
                && animationDurationSeconds <= 0))
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

        var durationFrames = PlaybackDurationFrames(action, preview, fps);
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

    private static int PlaybackDurationFrames(ComponentPreviewActionDefinition action, JsonObject preview, int fps)
    {
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
