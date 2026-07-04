using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static partial class EditorSearchMatcher
{
    public static bool Matches(string query, params string?[] candidateParts)
    {
        var queryTokens = Tokens(query);
        if (queryTokens.Length == 0) return true;

        var candidate = Normalize(string.Join(" ", candidateParts));
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        return queryTokens.All((token) => candidate.Contains(token));
    }

    private static string[] Tokens(string value)
    {
        return TokenSplitRegex()
            .Split(Normalize(value))
            .Where((token) => !string.IsNullOrWhiteSpace(token))
            .ToArray();
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace(".", " ")
            .Replace("/", " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex TokenSplitRegex();
}
