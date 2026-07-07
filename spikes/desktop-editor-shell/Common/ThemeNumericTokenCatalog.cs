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
        Token("theme.typography.lineHeights.tight", ["typography", "lineHeights", "tight"]),
        Token("theme.typography.lineHeights.compact", ["typography", "lineHeights", "compact"]),
        Token("theme.typography.lineHeights.normal", ["typography", "lineHeights", "normal"]),
        Token("theme.typography.lineHeights.relaxed", ["typography", "lineHeights", "relaxed"]),
        Token("theme.typography.lineHeights.loose", ["typography", "lineHeights", "loose"]),
        Token("theme.spacing.none", ["spacing", "none"]),
        Token("theme.spacing.xs", ["spacing", "xs"]),
        Token("theme.spacing.s", ["spacing", "s"]),
        Token("theme.spacing.m", ["spacing", "m"]),
        Token("theme.spacing.l", ["spacing", "l"]),
        Token("theme.spacing.xl", ["spacing", "xl"]),
        Token("theme.spacing.xxl", ["spacing", "xxl"]),
        Token("theme.radii.none", ["radii", "none"]),
        Token("theme.radii.control", ["radii", "control"]),
        Token("theme.radii.card", ["radii", "card"]),
        Token("theme.radii.panel", ["radii", "panel"]),
        Token("theme.radii.surface", ["radii", "surface"]),
        Token("theme.radii.pill", ["radii", "pill"]),
        Token("theme.radii.avatar", ["radii", "avatar"]),
        Token("theme.radii.full", ["radii", "full"]),
        Token("theme.shadows.default.alpha", ["shadows", "default", "color", "alpha"]),
        Token("theme.shadows.default.offsetX", ["shadows", "default", "offsetX"]),
        Token("theme.shadows.default.offsetY", ["shadows", "default", "offsetY"]),
        Token("theme.shadows.default.blur", ["shadows", "default", "blur"]),
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
