using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

internal static class SlugText
{
    public static string LowerSnake(string value, string fallback)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? fallback : slug;
    }

    public static string LowerSnakeOrName(string slug, string name, string fallback)
    {
        return string.IsNullOrWhiteSpace(slug)
            ? LowerSnake(name, fallback)
            : LowerSnake(slug, fallback);
    }
}
