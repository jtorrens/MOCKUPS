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
                _shellColumns.ColumnDefinitions[0].Width = new GridLength(state.LeftPanelWidth);
                _shellColumns.ColumnDefinitions[2].Width = new GridLength(state.EditorPanelWidth);
                _shellColumns.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
            }
        }
        catch
        {
            // Local UI state should never block opening the editor shell.
        }
    }

    public void Save()
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

    private sealed class ShellWindowState
    {
        public double Width { get; init; }
        public double Height { get; init; }
        public int? PositionX { get; init; }
        public int? PositionY { get; init; }
        public double LeftPanelWidth { get; init; }
        public double EditorPanelWidth { get; init; }
        public double RightPanelWidth { get; init; }
    }
}
