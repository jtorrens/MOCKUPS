using Avalonia.Controls;
using Avalonia.Layout;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorCardHeader
{
    public static Control Create(string label, string subtitle, Control icon)
    {
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(icon);

        var textPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
        };
        textPanel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Opacity = 0.72,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        header.Children.Add(textPanel);
        return header;
    }
}
