using System.Linq;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorSearchMatcher
{
    public static bool Matches(string query, params string?[] candidateParts)
    {
        var queryTokens = SearchText.Tokens(query);
        if (queryTokens.Length == 0) return true;

        var candidate = SearchText.Normalize(string.Join(" ", candidateParts));
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        return queryTokens.All((token) => candidate.Contains(token));
    }

}
