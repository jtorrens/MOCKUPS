using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorGroupBlock
{
    public static Control Create(EditorLayoutGroup group, Control content)
    {
        var panel = new StackPanel
        {
            Spacing = 10,
        };

        if (!string.IsNullOrWhiteSpace(group.Label))
        {
            panel.Children.Add(new TextBlock
            {
                Text = group.Label,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Opacity = 0.82,
            });
        }

        panel.Children.Add(content);

        return new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Child = panel,
        };
    }
}
