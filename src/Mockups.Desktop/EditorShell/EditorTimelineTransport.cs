using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorTimelineTransport
{
    public static Button CreateNavigationButton(Control icon, string accessibleName, double width = 34)
    {
        var button = new Button
        {
            Content = icon,
            Width = width,
            Height = 30,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        EditorAccessibility.Describe(button, accessibleName);
        return button;
    }

    public static void ApplyPrimaryStyle(Button button)
    {
        button.Background = EditorSukiWindowTheme.AccentBrush();
        button.Foreground = new SolidColorBrush(Color.Parse("#E6E6E6"));
        button.BorderBrush = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
    }

    public static Control CreateKeyframeStepIcon(bool next)
    {
        var chevron = EditorIcons.Create(
            next ? EditorIcons.TimelineNextFrame : EditorIcons.TimelinePreviousFrame,
            12);
        var diamond = CreateKeyframeGlyph(
            filled: true,
            size: 14,
            brush: EditorAnimationVisuals.OtherKeyframeBrush);
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (next)
        {
            content.Children.Add(chevron);
            content.Children.Add(diamond);
        }
        else
        {
            content.Children.Add(diamond);
            content.Children.Add(chevron);
        }
        return content;
    }

    public static Control CreateKeyframeGlyph(
        bool filled,
        double size = 16,
        IBrush? brush = null)
    {
        var icon = EditorIcons.Create(
            filled ? EditorIcons.TimelineKeyframe : EditorIcons.TimelineKeyframeEmpty,
            size);
        EditorIcons.ApplyBrush(icon, brush);
        return icon;
    }

    public static Control CreateAnimationActivationGlyph(
        bool filled,
        bool extendsOwnerDuration,
        double size = 16,
        IBrush? brush = null)
    {
        if (extendsOwnerDuration) return CreateKeyframeGlyph(filled, size, brush);
        var color = brush ?? EditorAnimationVisuals.InactiveTrackBrush;
        return new Ellipse
        {
            Width = size * 0.68,
            Height = size * 0.68,
            Fill = filled ? color : Brushes.Transparent,
            Stroke = color,
            StrokeThickness = 1.4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    public static Control CreateSeparator(bool isDark, double height = 22) => new Border
    {
        Width = 1,
        Height = height,
        Margin = new Thickness(2, 0),
        Background = EditorUiVisuals.ScrollbarSeparatorBrush(isDark),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
