using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryPaletteOptionTemplate
{
    public static IDataTemplate Create()
    {
        return new FuncDataTemplate<FieldOption>((option, _) =>
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };
            panel.Children.Add(new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(4),
                Background = DictionaryFieldColorValue.SafeBrush(option?.ColorHex, "#808080"),
                BorderBrush = new SolidColorBrush(Color.Parse("#667085")),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
            });
            panel.Children.Add(new TextBlock
            {
                Text = option?.Label ?? "",
                Foreground = TextBrush(),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            return panel;
        });
    }

    private static IBrush TextBrush()
    {
        var isLight = Application.Current?.ActualThemeVariant == ThemeVariant.Light;
        return new SolidColorBrush(Color.Parse(isLight ? "#1F2937" : "#F1F5F9"));
    }
}
