using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorOverrideVisuals
{
    public static IBrush Brush { get; } = new SolidColorBrush(Color.Parse("#D6A638"));
    public static IBrush BorderBrush { get; } = Brush;
    public static IBrush BackgroundBrush { get; } = new SolidColorBrush(Color.Parse("#24D6A638"));
    public static IBrush HighlightedBackgroundBrush { get; } = new SolidColorBrush(Color.Parse("#38D6A638"));

    public static void ApplyActionButton(Button button, bool isHighlighted = false)
    {
        button.Background = isHighlighted ? HighlightedBackgroundBrush : BackgroundBrush;
        button.BorderBrush = BorderBrush;
        button.Foreground = Brush;
        button.BorderThickness = new Thickness(1);
    }
}
