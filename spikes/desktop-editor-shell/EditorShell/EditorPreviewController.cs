using Avalonia;
using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorPreviewController
{
    private const int LoadingPreviewFrameThreshold = 30;
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
    private readonly TextBlock _designContextText;
    private readonly Button _designContextLockButton;
    private readonly RuntimeWebPreviewPane _runtimePreviewPane = new();
    private readonly DesignWebPreviewPane _designPreviewPane = new();
    private readonly ComponentInputsPanel _designInputsPanel;
    private string? _projectId;
    private string? _selectedThemeId;
    private PreviewNodeKey? _lastDesignPreviewNode;
    private PreviewNodeKey? _activeDesignPreviewNode;
    private PreviewNodeKey? _lockedDesignPreviewNode;
    private string _selectedMode = "light";
    private string _selectedOrientation = "portrait";
    private string _selectedScale = "fit";
    private bool _showDesignMarks;
    private bool _isDesignPreviewContextLocked;
    private bool _isRefreshingOptions;
    private bool? _renderedLockState;

    public EditorPreviewController(
        SpikeDatabase database,
        EditorInstantComboBox deviceComboBox,
        EditorInstantComboBox themeComboBox,
        EditorInstantComboBox modeComboBox,
        EditorInstantComboBox orientationComboBox,
        IEditorShellMessageSink messages,
        ContentControl previewSetupHost,
        ContentControl previewInputsHost,
        ContentControl runtimePreviewHost,
        ContentControl designPreviewHost,
        TextBlock designContextText,
        Button designContextLockButton,
        Func<bool> isDark,
        Func<ProjectTreeNode?> selectedNode,
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
        _designContextText = designContextText;
        _designContextLockButton = designContextLockButton;
        _designInputsPanel = new ComponentInputsPanel(database, Refresh, owner, PreparePlaybackFramesAsync);

        WrapPreviewSetup(previewSetupHost);
        previewInputsHost.Content = _designInputsPanel;
        runtimePreviewHost.Content = _runtimePreviewPane;
        designPreviewHost.Content = _designPreviewPane;
        AttachControlEvents();
        _designContextLockButton.Click += (_, _) => ToggleDesignPreviewContextLock();
        UpdateDesignContextChrome(null);
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
            if (string.IsNullOrWhiteSpace(SelectedDeviceId))
            {
                _messages.Warning("Preview", "No device selected.");
                return;
            }

            var metrics = ApplyPreviewOrientation(_database.GetDevicePreviewMetrics(SelectedDeviceId));
            var themeName = _themeComboBox.SelectedItem?.Label ?? "No theme";
            _runtimePreviewPane.Update(metrics, _isDark(), themeName, _selectedMode, _selectedScale);
            var designPayload = DesignPreviewPayloadForSelection();
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
            _messages.Clear();
        }
        catch (Exception exception)
        {
            _messages.Error("Preview", exception);
        }
    }

    private async Task PreparePlaybackFramesAsync()
    {
        EnsureSelectedOptionsExist();
        if (string.IsNullOrWhiteSpace(SelectedDeviceId)
            || string.IsNullOrWhiteSpace(_projectId))
        {
            return;
        }

        var designPayload = DesignPreviewPayloadForSelection();
        if (designPayload is null)
        {
            return;
        }

        var metrics = ApplyPreviewOrientation(_database.GetDevicePreviewMetrics(SelectedDeviceId));
        var themeName = _themeComboBox.SelectedItem?.Label ?? "No theme";
        var payload = _designInputsPanel.ApplyInputs(designPayload, _selectedMode, _projectId);
        var projectFps = _database.GetProjectSettings(_projectId).DefaultFps;
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
            return;
        }

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
            _designPreviewPane.ShowLoading(
                metrics,
                _isDark(),
                themeName,
                _selectedMode,
                _selectedScale,
                _showDesignMarks,
                $"Caching {frames.Count} frames at {PreviewPlaybackTiming.FrameRateMultiplier}x project FPS...");
        }

        WebDesignPreviewRenderer.ReserveFrameCacheCapacity(frames.Count);
        for (var index = 0; index < frames.Count; index++)
        {
            var frameStopwatch = Stopwatch.StartNew();
            var frame = frames[index];
            await WebDesignPreviewRenderer.PrewarmBodyAsync(
                metrics,
                _selectedMode,
                _showDesignMarks,
                frame);
            PreviewDebugLog.Write(
                "preview.playback.frames.prewarm",
                ("component", payload.ComponentType),
                ("frame", index + 1),
                ("frames", frames.Count),
                ("ms", frameStopwatch.Elapsed.TotalMilliseconds),
                ("time", PlaybackFrameTime(frame)));
        }
        PreviewDebugLog.Write(
            "preview.playback.frames.end",
            ("component", payload.ComponentType),
            ("name", payload.Name),
            ("frames", frames.Count),
            ("ms", totalStopwatch.Elapsed.TotalMilliseconds));
    }

    private static IEnumerable<DesignPreviewPayload> PlaybackFramePayloads(DesignPreviewPayload payload, int projectFps)
    {
        var fps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        if (JsonNode.Parse(string.IsNullOrWhiteSpace(payload.DesignPreviewJson) ? "{}" : payload.DesignPreviewJson) is not JsonObject preview
            || preview["animation"] is not JsonObject animation)
        {
            yield break;
        }

        var timeJsonKey = JsonString(animation, "timeJsonKey");
        var durationInputId = JsonString(animation, "durationInputId");
        var animationDurationSeconds = JsonNumber(animation, "durationSeconds", 0);
        if (string.IsNullOrWhiteSpace(timeJsonKey)
            || (string.IsNullOrWhiteSpace(durationInputId) && animationDurationSeconds <= 0))
        {
            yield break;
        }

        var duration = animationDurationSeconds > 0
            ? animationDurationSeconds
            : Math.Max(0, JsonNumber(preview, durationInputId, 0));
        if (duration <= 0)
        {
            yield break;
        }

        var frameCount = Math.Max(1, (int)Math.Ceiling(duration * fps));
        for (var frame = 0; frame <= frameCount; frame++)
        {
            var framePreview = JsonNode.Parse(preview.ToJsonString()) as JsonObject ?? new JsonObject();
            framePreview[timeJsonKey] = Math.Min(duration, frame / (double)fps);
            yield return payload with { DesignPreviewJson = framePreview.ToJsonString() };
        }
    }

    private static string PlaybackFrameTime(DesignPreviewPayload payload)
    {
        try
        {
            if (JsonNode.Parse(string.IsNullOrWhiteSpace(payload.DesignPreviewJson) ? "{}" : payload.DesignPreviewJson) is not JsonObject preview
                || preview["animation"] is not JsonObject animation)
            {
                return "";
            }

            var timeJsonKey = JsonString(animation, "timeJsonKey");
            if (string.IsNullOrWhiteSpace(timeJsonKey))
            {
                return "";
            }

            return preview[timeJsonKey]?.ToJsonString() ?? "";
        }
        catch
        {
            return "";
        }
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
            var lockedPayload = DesignPreviewPayloadFactory.Create(_database, _lockedDesignPreviewNode.ToNode(), _selectedThemeId);
            if (lockedPayload is not null)
            {
                _activeDesignPreviewNode = _lockedDesignPreviewNode;
                return lockedPayload;
            }

            _isDesignPreviewContextLocked = false;
            _lockedDesignPreviewNode = null;
        }

        var selectedNode = _selectedNode();
        var selectedPayload = DesignPreviewPayloadFactory.Create(_database, selectedNode, _selectedThemeId);
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

        var fallbackPayload = DesignPreviewPayloadFactory.Create(_database, _lastDesignPreviewNode.ToNode(), _selectedThemeId);
        _activeDesignPreviewNode = fallbackPayload is null ? null : _lastDesignPreviewNode;
        return fallbackPayload;
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
}
