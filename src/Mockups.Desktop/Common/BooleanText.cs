using System;

namespace Mockups.DesktopEditorShell.Common;

internal static class BooleanText
{
    public static bool Parse(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    public static bool ParseRequired(string value, string context)
    {
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1")
        {
            return true;
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0")
        {
            return false;
        }

        throw new InvalidOperationException($"{context} must be true or false.");
    }

    public static string Format(bool value)
    {
        return value ? "true" : "false";
    }
}
