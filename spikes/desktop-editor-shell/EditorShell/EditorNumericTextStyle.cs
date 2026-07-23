using Avalonia.Controls;
using Avalonia.Media;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorNumericTextStyle
{
    private static readonly FontFamily NumericFontFamily = new("SF Mono, Menlo, Consolas, monospace");

    public static TextBox Apply(TextBox textBox)
    {
        EditorTextBoxBehavior.Configure(textBox);
        EditorTextBoxBehavior.EnableSelectAllOnDoubleClick(textBox);
        textBox.FontFamily = NumericFontFamily;
        textBox.TextAlignment = TextAlignment.Left;
        return textBox;
    }

    public static NumericUpDown Apply(NumericUpDown numeric)
    {
        numeric.FontFamily = NumericFontFamily;
        return numeric;
    }
}
