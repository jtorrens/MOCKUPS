using System;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

internal static class IconTokenRules
{
    public static string TokenFromText(string value)
    {
        var token = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9_]+", "_");
        token = Regex.Replace(token, "_+", "_").Trim('_');
        return token;
    }

    public static string CategoryFromToken(string token)
    {
        var index = token.IndexOf('_', StringComparison.Ordinal);
        return index <= 0 ? "misc" : token[..index];
    }
}
