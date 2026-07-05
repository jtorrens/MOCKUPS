using System;

namespace Mockups.DesktopEditorShell.Common;

internal static class BooleanText
{
    public static bool Parse(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    public static string Format(bool value)
    {
        return value ? "true" : "false";
    }
}
