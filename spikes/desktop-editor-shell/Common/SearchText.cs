using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

internal static partial class SearchText
{
    public static string Normalize(string value)
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

    public static string[] Tokens(string value)
    {
        return TokenSplitRegex()
            .Split(Normalize(value))
            .Where((token) => !string.IsNullOrWhiteSpace(token))
            .ToArray();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex TokenSplitRegex();
}
