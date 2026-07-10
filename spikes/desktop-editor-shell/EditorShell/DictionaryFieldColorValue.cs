using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryFieldColorValue
{
    public static string NormalizeHex(string value)
    {
        return ColorValue.NormalizeHex(value);
    }

    public static Color Parse(string value)
    {
        return ColorValue.Parse(value);
    }

    public static IBrush SafeBrush(string? value, string fallback)
    {
        return ColorValue.SafeBrush(value, fallback);
    }
}
