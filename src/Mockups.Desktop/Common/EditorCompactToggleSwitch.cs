using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mockups.DesktopEditorShell.Common;

internal sealed class EditorCompactToggleSwitch : ToggleButton
{
    private readonly Border _track;
    private readonly Border _thumb;

    public EditorCompactToggleSwitch()
    {
        Width = 40;
        Height = 24;
        Padding = new Thickness(0);
        Background = Brushes.Transparent;
        BorderBrush = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Cursor = new Cursor(StandardCursorType.Hand);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;

        _track = new Border
        {
            Width = 40,
            Height = 24,
            CornerRadius = new CornerRadius(12),
        };
        _thumb = new Border
        {
            Width = 20,
            Height = 20,
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var content = new Grid
        {
            Width = 40,
            Height = 24,
            Children =
            {
                _track,
                _thumb,
            },
        };
        Content = content;
        PropertyChanged += (_, change) =>
        {
            if (change.Property == IsCheckedProperty)
            {
                UpdateVisualState();
            }
        };
        UpdateVisualState();
    }

    internal bool ShowsCheckedState =>
        _thumb.HorizontalAlignment == HorizontalAlignment.Right;

    private void UpdateVisualState()
    {
        var isChecked = IsChecked == true;
        _track.Background = isChecked
            ? EditorSukiWindowTheme.AccentBrush()
            : new SolidColorBrush(Color.Parse("#454950"));
        _thumb.HorizontalAlignment = isChecked
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
    }
}
