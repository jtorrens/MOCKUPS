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
        Token("theme.colors.surface", ["modes", "light", "colors", "surface"], ["modes", "dark", "colors", "surface"]),
        Token("theme.colors.card", ["modes", "light", "colors", "card"], ["modes", "dark", "colors", "card"]),
        Token("theme.colors.label", ["modes", "light", "colors", "label"], ["modes", "dark", "colors", "label"]),
        Token("theme.colors.text", ["modes", "light", "colors", "text"], ["modes", "dark", "colors", "text"]),
        Token("theme.colors.textPrimary", ["modes", "light", "colors", "textPrimary"], ["modes", "dark", "colors", "textPrimary"]),
        Token("theme.colors.textSecondary", ["modes", "light", "colors", "textSecondary"], ["modes", "dark", "colors", "textSecondary"]),
        Token("theme.colors.icon", ["modes", "light", "colors", "icon"], ["modes", "dark", "colors", "icon"]),
        Token("theme.colors.button", ["modes", "light", "colors", "button"], ["modes", "dark", "colors", "button"]),
        Token("theme.colors.field", ["modes", "light", "colors", "field"], ["modes", "dark", "colors", "field"]),
        Token("theme.colors.checkbox", ["modes", "light", "colors", "checkbox"], ["modes", "dark", "colors", "checkbox"]),
        Token("theme.colors.radio", ["modes", "light", "colors", "radio"], ["modes", "dark", "colors", "radio"]),
        Token("theme.colors.switch", ["modes", "light", "colors", "switch"], ["modes", "dark", "colors", "switch"]),
        Token("theme.colors.tab", ["modes", "light", "colors", "tab"], ["modes", "dark", "colors", "tab"]),
        Token("theme.colors.menuItem", ["modes", "light", "colors", "menuItem"], ["modes", "dark", "colors", "menuItem"]),
        Token("theme.colors.badge", ["modes", "light", "colors", "badge"], ["modes", "dark", "colors", "badge"]),
        Token("theme.colors.toast", ["modes", "light", "colors", "toast"], ["modes", "dark", "colors", "toast"]),
        Token("theme.colors.divider", ["modes", "light", "colors", "divider"], ["modes", "dark", "colors", "divider"]),
        Token("theme.colors.accent", ["modes", "light", "colors", "accent"], ["modes", "dark", "colors", "accent"]),
        Token("theme.icons.primary", ["modes", "light", "colors", "icons.primary"], ["modes", "dark", "colors", "icons.primary"]),
        Token("theme.icons.secondary", ["modes", "light", "colors", "icons.secondary"], ["modes", "dark", "colors", "icons.secondary"]),
        Token("theme.icons.alternate", ["modes", "light", "colors", "icons.alternate"], ["modes", "dark", "colors", "icons.alternate"]),
        Token("theme.icons.accent", ["modes", "light", "colors", "icons.accent"], ["modes", "dark", "colors", "icons.accent"]),
        Token("theme.borders.primary", ["modes", "light", "colors", "borders.primary"], ["modes", "dark", "colors", "borders.primary"]),
        Token("theme.borders.secondary", ["modes", "light", "colors", "borders.secondary"], ["modes", "dark", "colors", "borders.secondary"]),
        Token("theme.borders.alternate", ["modes", "light", "colors", "borders.alternate"], ["modes", "dark", "colors", "borders.alternate"]),
        Token("theme.cursor.color", ["modes", "light", "colors", "theme.cursor.color"], ["modes", "dark", "colors", "theme.cursor.color"]),
        Token("theme.keyboard.background", ["modes", "light", "keyboard", "background"], ["modes", "dark", "keyboard", "background"]),
        Token("theme.keyboard.keyBackground", ["modes", "light", "keyboard", "keyBackground"], ["modes", "dark", "keyboard", "keyBackground"]),
        Token("theme.keyboard.specialKeyBackground", ["modes", "light", "keyboard", "specialKeyBackground"], ["modes", "dark", "keyboard", "specialKeyBackground"]),
        Token("theme.keyboard.pressedKeyBackground", ["modes", "light", "keyboard", "pressedKeyBackground"], ["modes", "dark", "keyboard", "pressedKeyBackground"]),
        Token("theme.keyboard.keyBorder", ["modes", "light", "keyboard", "keyBorder"], ["modes", "dark", "keyboard", "keyBorder"]),
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

}
