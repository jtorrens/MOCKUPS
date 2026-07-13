using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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

    public static Control CreateSeparator(bool isDark, double height = 22) => new Border
    {
        Width = 1,
        Height = height,
        Margin = new Thickness(2, 0),
        Background = EditorUiVisuals.ScrollbarSeparatorBrush(isDark),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
