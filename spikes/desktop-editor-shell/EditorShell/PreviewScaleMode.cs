using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class PreviewScaleMode
{
    public static bool TryFixedScale(string value, out double scale)
    {
        scale = value switch
        {
            "actual" => 1,
            "2x" => 2,
            "3x" => 3,
            "4x" => 4,
            _ => 0,
        };
        return scale > 0;
    }

    public static string WebMode(string value)
    {
        return TryFixedScale(value, out var scale)
            ? scale.ToString("0.###", CultureInfo.InvariantCulture)
            : "fit";
    }
}
