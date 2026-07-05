using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record ThemeColorTokenPath(
    string Id,
    IReadOnlyList<string> LightPath,
    IReadOnlyList<string> DarkPath,
    IReadOnlyList<string>? LightAlphaPath = null,
    IReadOnlyList<string>? DarkAlphaPath = null)
{
    public bool HasAlpha => LightAlphaPath is not null && DarkAlphaPath is not null;
}

internal static class ThemeColorTokenCatalog
{
    public static IReadOnlyList<ThemeColorTokenPath> ColorTokens { get; } =
    [
        Token("theme.colors.background", ["modes", "light", "colors", "background"], ["modes", "dark", "colors", "background"]),
        Token("theme.colors.textPrimary", ["modes", "light", "colors", "textPrimary"], ["modes", "dark", "colors", "textPrimary"]),
        Token("theme.colors.textSecondary", ["modes", "light", "colors", "textSecondary"], ["modes", "dark", "colors", "textSecondary"]),
        Token("theme.colors.accent", ["modes", "light", "colors", "accent"], ["modes", "dark", "colors", "accent"]),
        Token("theme.icons.primary", ["modes", "light", "colors", "icons.primary"], ["modes", "dark", "colors", "icons.primary"]),
        Token("theme.icons.secondary", ["modes", "light", "colors", "icons.secondary"], ["modes", "dark", "colors", "icons.secondary"]),
        Token("theme.icons.accent", ["modes", "light", "colors", "icons.accent"], ["modes", "dark", "colors", "icons.accent"]),
        Token("theme.borders.primary", ["modes", "light", "colors", "borders.primary"], ["modes", "dark", "colors", "borders.primary"]),
        Token("theme.borders.secondary", ["modes", "light", "colors", "borders.secondary"], ["modes", "dark", "colors", "borders.secondary"]),
        Token("theme.borders.alternate", ["modes", "light", "colors", "borders.alternate"], ["modes", "dark", "colors", "borders.alternate"]),
        Token("theme.cursor.color", ["modes", "light", "colors", "theme.cursor.color"], ["modes", "dark", "colors", "theme.cursor.color"]),
        Token("theme.statusBar.foreground", ["modes", "light", "statusBar", "foreground"], ["modes", "dark", "statusBar", "foreground"]),
        TokenWithAlpha(
            "theme.statusBar.background",
            ["modes", "light", "statusBar", "background", "color"],
            ["modes", "dark", "statusBar", "background", "color"],
            ["modes", "light", "statusBar", "background", "alpha"],
            ["modes", "dark", "statusBar", "background", "alpha"]),
        Token("theme.navigationBar.foreground", ["modes", "light", "navigationBar", "foreground"], ["modes", "dark", "navigationBar", "foreground"]),
        TokenWithAlpha(
            "theme.navigationBar.background",
            ["modes", "light", "navigationBar", "background", "color"],
            ["modes", "dark", "navigationBar", "background", "color"],
            ["modes", "light", "navigationBar", "background", "alpha"],
            ["modes", "dark", "navigationBar", "background", "alpha"]),
        Token("theme.keyboard.background", ["modes", "light", "keyboard", "background"], ["modes", "dark", "keyboard", "background"]),
        Token("theme.keyboard.keyBackground", ["modes", "light", "keyboard", "keyBackground"], ["modes", "dark", "keyboard", "keyBackground"]),
        Token("theme.keyboard.specialKeyBackground", ["modes", "light", "keyboard", "specialKeyBackground"], ["modes", "dark", "keyboard", "specialKeyBackground"]),
        Token("theme.keyboard.pressedKeyBackground", ["modes", "light", "keyboard", "pressedKeyBackground"], ["modes", "dark", "keyboard", "pressedKeyBackground"]),
        Token("theme.keyboard.popoverBackground", ["modes", "light", "keyboard", "popoverBackground"], ["modes", "dark", "keyboard", "popoverBackground"]),
        Token("theme.keyboard.text", ["modes", "light", "keyboard", "text"], ["modes", "dark", "keyboard", "text"]),
    ];

    public static bool TryGet(string id, out ThemeColorTokenPath token)
    {
        token = ColorTokens.FirstOrDefault((candidate) => candidate.Id.Equals(id, StringComparison.Ordinal))
            ?? new ThemeColorTokenPath("", [], []);
        return token.Id.Length > 0;
    }

    private static ThemeColorTokenPath Token(string id, string[] lightPath, string[] darkPath)
    {
        return new ThemeColorTokenPath(id, lightPath, darkPath);
    }

    private static ThemeColorTokenPath TokenWithAlpha(
        string id,
        string[] lightPath,
        string[] darkPath,
        string[] lightAlphaPath,
        string[] darkAlphaPath)
    {
        return new ThemeColorTokenPath(id, lightPath, darkPath, lightAlphaPath, darkAlphaPath);
    }
}
