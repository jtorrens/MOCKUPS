using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorNavigationVisuals
{
    public static IBrush RowBackground(bool isSelected, bool isDark)
    {
        return isSelected
            ? EditorSukiWindowTheme.SelectionBackgroundBrush(isDark)
            : Brushes.Transparent;
    }

    public static IBrush TextBrush(bool isSelected, bool isDark)
    {
        return isSelected
            ? EditorUiVisuals.SelectedTextBrush(isDark)
            : EditorUiVisuals.PrimaryTextBrush(isDark);
    }

    public static IBrush MutedTextBrush(bool isSelected, bool isDark)
    {
        return isSelected
            ? EditorUiVisuals.SelectedTextBrush(isDark)
            : EditorUiVisuals.SecondaryTextBrush(isDark);
    }

    public static IBrush VariantLockBrush(bool isLocked)
    {
        return new SolidColorBrush(Color.Parse(isLocked ? "#D6A638" : "#FFFFFF"));
    }

    public static Avalonia.Controls.Shapes.Ellipse UsedDot(ProjectTreeNode node, bool isDark)
    {
        return UsedDot(node.IsUsed, isDark);
    }

    public static Avalonia.Controls.Shapes.Ellipse UsedDot(bool isUsed, bool isDark)
    {
        return new Avalonia.Controls.Shapes.Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = isUsed ? new SolidColorBrush(Color.Parse("#F2B51D")) : Brushes.Transparent,
            Stroke = isUsed
                ? Brushes.Transparent
                : new SolidColorBrush(Color.Parse(isDark ? "#8D96A6" : "#667085")),
            StrokeThickness = isUsed ? 0 : 1,
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

    public static Button ToggleButton(bool isExpanded, string accessibleLabel, EventHandler<RoutedEventArgs> onClick)
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
        EditorAccessibility.Describe(button, accessibleLabel);
        button.Click += onClick;
        return button;
    }

    public static IBrush SafeColorBrush(string? hex, string fallback)
    {
        return ColorValue.SafeBrush(hex, fallback);
    }
}
