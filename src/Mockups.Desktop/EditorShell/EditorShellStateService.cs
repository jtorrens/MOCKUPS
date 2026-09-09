using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorShellStateService
{
    private const string CurrentSchema = "mockups_shell_window_state";
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly Window _window;
    private readonly Grid _shellColumns;
    private readonly string _statePath;

    public EditorShellStateService(
        Window window,
        Grid shellColumns,
        string? statePath = null)
    {
        _window = window;
        _shellColumns = shellColumns;
        _statePath = string.IsNullOrWhiteSpace(statePath)
            ? DefaultShellStatePath()
            : Path.GetFullPath(statePath);
    }

    public bool IsDark { get; private set; } = true;
    public string SukiColor { get; private set; } = "Blue";
    public double UiTextScale { get; private set; } = 1;
    public double UiCardPaddingScale { get; private set; } = 1;
    public bool IsNavigationPanelCollapsed { get; private set; }
    public double NavigationPanelExpandedWidth { get; private set; } =
        PreviewPanelLayoutPolicy.DefaultLeftColumnWidth;
    public double NavigationPanelExpandedEditorWidth { get; private set; } =
        PreviewPanelLayoutPolicy.ForWindow(
            PreviewPanelLayoutPolicy.DefaultWindowWidth).EditorPanelWidth;
    public double NavigationPanelExpandedPreviewWidth { get; private set; } =
        PreviewPanelLayoutPolicy.ForWindow(
            PreviewPanelLayoutPolicy.DefaultWindowWidth).PreviewPanelWidth;

    public void Restore()
    {
        if (!File.Exists(_statePath)) return;

        ShellWindowState state;
        try
        {
            state = JsonSerializer.Deserialize<ShellWindowState>(
                    File.ReadAllText(_statePath),
                    JsonOptions)
                ?? throw new InvalidDataException(
                    "The shell window state document is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The shell window state document is malformed or incomplete.",
                exception);
        }
        RequireCurrent(state);

        _window.Width = state.Width;
        _window.Height = state.Height;
        if (state.PositionX.HasValue && state.PositionY.HasValue)
        {
            _window.Position = new PixelPoint(
                state.PositionX.Value,
                state.PositionY.Value);
        }

        var columns = PreviewPanelLayoutPolicy.ClampRestoredColumns(
            _window.Width,
            state.LeftPanelWidth,
            state.EditorPanelWidth);
        _shellColumns.ColumnDefinitions[0].Width =
            new GridLength(columns.LeftPanelWidth);
        _shellColumns.ColumnDefinitions[2].Width =
            new GridLength(columns.EditorPanelWidth);
        _shellColumns.ColumnDefinitions[4].Width =
            new GridLength(1, GridUnitType.Star);
        NavigationPanelExpandedWidth = columns.LeftPanelWidth;
        NavigationPanelExpandedEditorWidth = columns.EditorPanelWidth;
        NavigationPanelExpandedPreviewWidth = state.RightPanelWidth;
        IsNavigationPanelCollapsed = state.IsNavigationPanelCollapsed;
        IsDark = state.IsDark;
        SukiColor = state.SukiColor;
        UiTextScale = state.UiTextScale;
        UiCardPaddingScale = state.UiCardPaddingScale;
    }

    public void SetTheme(bool isDark, string color)
    {
        IsDark = isDark;
        SukiColor = string.IsNullOrWhiteSpace(color) ? "Blue" : color;
    }

    public void SetUiTextScale(double value) =>
        UiTextScale = RequireScale(value, 0.5, 1.75, "UI text scale");

    public void SetUiCardPaddingScale(double value) =>
        UiCardPaddingScale = RequireScale(
            value,
            0.1,
            1.5,
            "UI card padding scale");

    public void Save(EditorNavigationPanelState? navigationPanel = null)
    {
        var directory = Path.GetDirectoryName(_statePath)
            ?? throw new InvalidOperationException(
                "The shell window state has no parent directory.");
        Directory.CreateDirectory(directory);
        var navigationState = navigationPanel
            ?? new EditorNavigationPanelState(
                false,
                _shellColumns.ColumnDefinitions[0].ActualWidth,
                _shellColumns.ColumnDefinitions[2].ActualWidth,
                _shellColumns.ColumnDefinitions[4].ActualWidth);
        var state = new ShellWindowState
        {
            Schema = CurrentSchema,
            Version = CurrentVersion,
            Width = _window.Width,
            Height = _window.Height,
            PositionX = _window.Position.X,
            PositionY = _window.Position.Y,
            LeftPanelWidth = navigationState.ExpandedWidth,
            EditorPanelWidth = navigationState.ExpandedEditorWidth,
            RightPanelWidth = navigationState.ExpandedPreviewWidth,
            IsNavigationPanelCollapsed = navigationState.IsCollapsed,
            IsDark = IsDark,
            SukiColor = SukiColor,
            UiTextScale = UiTextScale,
            UiCardPaddingScale = UiCardPaddingScale,
        };
        RequireCurrent(state);
        File.WriteAllText(
            _statePath,
            JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string DefaultShellStatePath()
    {
        var root = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(
            root,
            "..",
            "..",
            "..",
            "data",
            "window-state.json"));
    }

    private void RequireCurrent(ShellWindowState state)
    {
        if (state.Schema != CurrentSchema
            || state.Version != CurrentVersion
            || state.Width < _window.MinWidth
            || state.Height < _window.MinHeight
            || state.LeftPanelWidth <= 0
            || state.EditorPanelWidth <= 0
            || state.RightPanelWidth <= 0
            || string.IsNullOrWhiteSpace(state.SukiColor))
        {
            throw new InvalidDataException(
                "The shell window state document does not satisfy its current contract.");
        }
        _ = RequireScale(state.UiTextScale, 0.5, 1.75, "UI text scale");
        _ = RequireScale(
            state.UiCardPaddingScale,
            0.1,
            1.5,
            "UI card padding scale");
    }

    private static double RequireScale(
        double value,
        double minimum,
        double maximum,
        string label) =>
        double.IsFinite(value) && value >= minimum && value <= maximum
            ? value
            : throw new InvalidDataException(
                $"The shell window state {label} is outside its current contract.");

    private sealed class ShellWindowState
    {
        public required string Schema { get; init; }
        public required int Version { get; init; }
        public required double Width { get; init; }
        public required double Height { get; init; }
        public required int? PositionX { get; init; }
        public required int? PositionY { get; init; }
        public required double LeftPanelWidth { get; init; }
        public required double EditorPanelWidth { get; init; }
        public required double RightPanelWidth { get; init; }
        public required bool IsNavigationPanelCollapsed { get; init; }
        public required bool IsDark { get; init; }
        public required string SukiColor { get; init; }
        public required double UiTextScale { get; init; }
        public required double UiCardPaddingScale { get; init; }
    }
}
