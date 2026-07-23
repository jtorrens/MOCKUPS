using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia;
using System;
using System.IO;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorShellStateService
{
    private readonly Window _window;
    private readonly Grid _shellColumns;

    public EditorShellStateService(Window window, Grid shellColumns)
    {
        _window = window;
        _shellColumns = shellColumns;
    }

    public bool IsDark { get; private set; } = true;
    public string SukiColor { get; private set; } = "Blue";
    public double UiTextScale { get; private set; } = 1;
    public double UiCardPaddingScale { get; private set; } = 1;
    public string Workspace { get; private set; } = "design";
    public string ProductionId { get; private set; } = "";
    public EditorSessionHistoryState SessionHistory { get; private set; } = new();

    public void Restore()
    {
        var path = ShellStatePath();
        if (!File.Exists(path)) return;

        try
        {
            var state = JsonSerializer.Deserialize<ShellWindowState>(File.ReadAllText(path));
            if (state is null) return;

            if (state.Width >= _window.MinWidth)
            {
                _window.Width = state.Width;
            }

            if (state.Height >= _window.MinHeight)
            {
                _window.Height = state.Height;
            }

            if (state.PositionX.HasValue && state.PositionY.HasValue)
            {
                _window.Position = new PixelPoint(state.PositionX.Value, state.PositionY.Value);
            }

            if (state.LeftPanelWidth > 0 && state.EditorPanelWidth > 0)
            {
                var columns = PreviewPanelLayoutPolicy.ClampRestoredColumns(
                    _window.Width,
                    state.LeftPanelWidth,
                    state.EditorPanelWidth);
                _shellColumns.ColumnDefinitions[0].Width = new GridLength(columns.LeftPanelWidth);
                _shellColumns.ColumnDefinitions[2].Width = new GridLength(columns.EditorPanelWidth);
                _shellColumns.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
            }

            IsDark = state.IsDark ?? true;
            SukiColor = string.IsNullOrWhiteSpace(state.SukiColor) ? "Blue" : state.SukiColor;
            UiTextScale = ClampScale(state.UiTextScale, 1, 0.5, 1.75);
            UiCardPaddingScale = ClampScale(state.UiCardPaddingScale, 1, 0.1, 1.5);
            Workspace = string.IsNullOrWhiteSpace(state.Workspace) ? "design" : state.Workspace;
            ProductionId = state.ProductionId ?? "";
            SessionHistory = state.SessionHistory ?? new EditorSessionHistoryState();
        }
        catch
        {
            // Local UI state should never block opening the editor shell.
        }
    }

    public void SetTheme(bool isDark, string color)
    {
        IsDark = isDark;
        SukiColor = string.IsNullOrWhiteSpace(color) ? "Blue" : color;
    }

    public void SetUiTextScale(double value)
    {
        UiTextScale = ClampScale(value, 1, 0.5, 1.75);
    }

    public void SetUiCardPaddingScale(double value)
    {
        UiCardPaddingScale = ClampScale(value, 1, 0.1, 1.5);
    }

    public void SetWorkspace(EditorWorkspace workspace)
    {
        Workspace = EditorWorkspaceNavigation.StorageValue(workspace);
    }

    public void SetProductionId(string productionId)
    {
        ProductionId = productionId ?? "";
    }

    public void Save(EditorSessionHistoryState? sessionHistory = null)
    {
        try
        {
            var path = ShellStatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var state = new ShellWindowState
            {
                Width = _window.Width,
                Height = _window.Height,
                PositionX = _window.Position.X,
                PositionY = _window.Position.Y,
                LeftPanelWidth = _shellColumns.ColumnDefinitions[0].ActualWidth,
                EditorPanelWidth = _shellColumns.ColumnDefinitions[2].ActualWidth,
                RightPanelWidth = _shellColumns.ColumnDefinitions[4].ActualWidth,
                IsDark = IsDark,
                SukiColor = SukiColor,
                UiTextScale = UiTextScale,
                UiCardPaddingScale = UiCardPaddingScale,
                Workspace = Workspace,
                ProductionId = ProductionId,
                SessionHistory = sessionHistory ?? SessionHistory,
            };

            File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
        }
        catch
        {
            // Same rule as restore: UI state is nice-to-have, not project data.
        }
    }

    private static string ShellStatePath()
    {
        var root = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, "..", "..", "..", "data", "window-state.json"));
    }

    private static double ClampScale(double? value, double fallback, double min, double max)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return fallback;
        }

        return Math.Clamp(value.Value, min, max);
    }

    private sealed class ShellWindowState
    {
        public double Width { get; init; }
        public double Height { get; init; }
        public int? PositionX { get; init; }
        public int? PositionY { get; init; }
        public double LeftPanelWidth { get; init; }
        public double EditorPanelWidth { get; init; }
        public double RightPanelWidth { get; init; }
        public bool? IsDark { get; init; }
        public string? SukiColor { get; init; }
        public double? UiTextScale { get; init; }
        public double? UiCardPaddingScale { get; init; }
        public string? Workspace { get; init; }
        public string? ProductionId { get; init; }
        public EditorSessionHistoryState? SessionHistory { get; init; }
    }
}
