using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record ThemeNumericTokenPath(
    string Id,
    IReadOnlyList<string> Path);

internal static class ThemeNumericTokenCatalog
{
    public static IReadOnlyList<ThemeNumericTokenPath> NumericTokens { get; } =
    [
        Token("theme.cursor.width", ["cursor", "width"]),
        Token("theme.cursor.blinkFrames", ["cursor", "blinkFrames"]),
        Token("theme.typography.size", ["typography", "size"]),
        Token("theme.typography.sizes.xs", ["typography", "sizes", "xs"]),
        Token("theme.typography.sizes.s", ["typography", "sizes", "s"]),
        Token("theme.typography.sizes.m", ["typography", "sizes", "m"]),
        Token("theme.typography.sizes.l", ["typography", "sizes", "l"]),
        Token("theme.typography.sizes.xl", ["typography", "sizes", "xl"]),
        Token("theme.typography.weight", ["typography", "weight"]),
        Token("theme.radii.control", ["radii", "control"]),
        Token("theme.radii.card", ["radii", "card"]),
        Token("theme.radii.panel", ["radii", "panel"]),
        Token("theme.radii.surface", ["radii", "surface"]),
        Token("theme.radii.pill", ["radii", "pill"]),
        Token("theme.radii.avatar", ["radii", "avatar"]),
        Token("theme.radii.full", ["radii", "full"]),
    ];

    public static bool TryGet(string id, out ThemeNumericTokenPath token)
    {
        token = NumericTokens.FirstOrDefault((candidate) => candidate.Id.Equals(id, StringComparison.Ordinal))
            ?? new ThemeNumericTokenPath("", []);
        return token.Id.Length > 0;
    }

    private static ThemeNumericTokenPath Token(string id, string[] path)
    {
        return new ThemeNumericTokenPath(id, path);
    }
}
