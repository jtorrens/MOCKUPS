using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorGroupBlock
{
    public static Control CreatePlain(EditorLayoutGroup group, Control content)
    {
        if (string.IsNullOrWhiteSpace(group.Label) ||
            string.Equals(group.Label, "General", System.StringComparison.OrdinalIgnoreCase))
        {
            return content;
        }

        var panel = new StackPanel
        {
            Spacing = EditorUiDensity.Card(10),
        };
        panel.Children.Add(new TextBlock
        {
            Text = group.Label,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.82,
        });
        panel.Children.Add(content);
        return panel;
    }

    public static Control Create(EditorLayoutGroup group, Control content)
    {
        var panel = new StackPanel
        {
            Spacing = EditorUiDensity.Card(10),
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
            Padding = EditorUiDensity.CardThickness(12),
            Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Child = panel,
        };
    }
}
