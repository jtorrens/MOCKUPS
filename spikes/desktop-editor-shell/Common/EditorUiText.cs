using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

internal static partial class EditorUiText
{
    public static string Count(int count, string singular, string? plural = null)
    {
        return $"{count} {Noun(count, singular, plural)}";
    }

    public static string Noun(int count, string singular, string? plural = null)
    {
        return count == 1 ? singular : plural ?? $"{singular}s";
    }

    public static string IdentifierLabel(string value)
    {
        var spaced = WordBoundaryRegex().Replace(value.Replace('_', ' ').Replace('-', ' '), "$1 $2");
        if (string.IsNullOrWhiteSpace(spaced)) return "Component";
        spaced = WhitespaceRegex().Replace(spaced.Trim(), " ").ToLowerInvariant();
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundaryRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
