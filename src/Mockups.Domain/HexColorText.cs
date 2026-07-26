using System;

namespace Mockups.DesktopEditorShell.Common;

public static class HexColorText
{
    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 6 && !trimmed.StartsWith("#", StringComparison.Ordinal)
            ? $"#{trimmed}"
            : trimmed;
    }
}
