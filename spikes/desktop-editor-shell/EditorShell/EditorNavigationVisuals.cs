using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorNavigationVisuals
{
    public static IBrush RowBackground(bool isSelected, bool isDark)
    {
        return isSelected
            ? new SolidColorBrush(Color.Parse(isDark ? "#253f5f" : "#dfefff"))
            : Brushes.Transparent;
    }

    public static IBrush TextBrush(bool isSelected, bool isDark)
    {
        return isSelected
            ? new SolidColorBrush(Color.Parse(isDark ? "#7DB7FF" : "#1368CE"))
            : new SolidColorBrush(Color.Parse(isDark ? "#F1F5F9" : "#1F2937"));
    }

    public static IBrush MutedTextBrush(bool isSelected, bool isDark)
    {
        return isSelected
            ? new SolidColorBrush(Color.Parse(isDark ? "#A8CEFF" : "#2F7EDB"))
            : new SolidColorBrush(Color.Parse(isDark ? "#B8C0CE" : "#667085"));
    }

    public static Avalonia.Controls.Shapes.Ellipse UsedDot(ProjectTreeNode node, bool isDark)
    {
        return new Avalonia.Controls.Shapes.Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = node.IsUsed ? new SolidColorBrush(Color.Parse("#D6A638")) : Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.Parse(isDark ? "#8d96a6" : "#667085")),
            StrokeThickness = 1,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
    }

    public static Border PaletteSwatch(string? colorHex, bool isDark)
    {
        return new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(4),
            Background = SafeColorBrush(colorHex, "#808080"),
            BorderBrush = new SolidColorBrush(Color.Parse(isDark ? "#B7C0D2" : "#667085")),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
    }

    public static Button ToggleButton(bool isExpanded, string tooltip, EventHandler<RoutedEventArgs> onClick)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = isExpanded ? "v" : ">",
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        ToolTip.SetTip(button, tooltip);
        button.Click += onClick;
        return button;
    }

    public static IBrush SafeColorBrush(string? hex, string fallback)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(string.IsNullOrWhiteSpace(hex) ? fallback : hex));
        }
        catch (FormatException)
        {
            return new SolidColorBrush(Color.Parse(fallback));
        }
    }
}
