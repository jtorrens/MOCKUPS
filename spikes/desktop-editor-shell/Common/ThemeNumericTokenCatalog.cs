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
        Token("theme.cursor.blinkDurationMs", ["cursor", "blinkDurationMs"]),
        Token("theme.keyboard.height", ["keyboard", "height"]),
        Token("theme.keyboard.keyGap", ["keyboard", "keyGap"]),
        Token("theme.keyboard.rowGap", ["keyboard", "rowGap"]),
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
        Token("theme.iconSizes.xs", ["iconSizes", "xs"]),
        Token("theme.iconSizes.s", ["iconSizes", "s"]),
        Token("theme.iconSizes.m", ["iconSizes", "m"]),
        Token("theme.iconSizes.l", ["iconSizes", "l"]),
        Token("theme.iconSizes.xl", ["iconSizes", "xl"]),
        Token("theme.spacing.none", ["spacing", "none"]),
        Token("theme.spacing.xs", ["spacing", "xs"]),
        Token("theme.spacing.s", ["spacing", "s"]),
        Token("theme.spacing.m", ["spacing", "m"]),
        Token("theme.spacing.l", ["spacing", "l"]),
        Token("theme.spacing.xl", ["spacing", "xl"]),
        Token("theme.spacing.xxl", ["spacing", "xxl"]),
        Token("theme.radii.none", ["radii", "none"]),
        Token("theme.radii.xs", ["radii", "xs"]),
        Token("theme.radii.s", ["radii", "s"]),
        Token("theme.radii.m", ["radii", "m"]),
        Token("theme.radii.l", ["radii", "l"]),
        Token("theme.radii.xl", ["radii", "xl"]),
        Token("theme.radii.xxl", ["radii", "xxl"]),
        Token("theme.radii.full", ["radii", "full"]),
        Token("theme.shadows.default.alpha", ["shadows", "default", "color", "alpha"]),
        Token("theme.shadows.default.offsetX", ["shadows", "default", "offsetX"]),
        Token("theme.shadows.default.offsetY", ["shadows", "default", "offsetY"]),
        Token("theme.shadows.default.blur", ["shadows", "default", "blur"]),
        Token("theme.motion.fade.durationMs", ["motion", "transitions", "fade", "durationMs"]),
        Token("theme.motion.fade.delayMs", ["motion", "transitions", "fade", "delayMs"]),
        Token("theme.motion.slide.durationMs", ["motion", "transitions", "slide", "durationMs"]),
        Token("theme.motion.slide.delayMs", ["motion", "transitions", "slide", "delayMs"]),
        Token("theme.motion.swipe.durationMs", ["motion", "transitions", "swipe", "durationMs"]),
        Token("theme.motion.swipe.delayMs", ["motion", "transitions", "swipe", "delayMs"]),
        Token("theme.motion.scale.durationMs", ["motion", "transitions", "scale", "durationMs"]),
        Token("theme.motion.scale.delayMs", ["motion", "transitions", "scale", "delayMs"]),
        Token("theme.motion.buttonPushedDurationMs", ["motion", "buttonPushedDurationMs"]),
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
