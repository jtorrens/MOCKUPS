using System;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class TypographyStyleValue
{
    public const string FontFamilyId = "fontFamilyId";
    public const string Weight = "weight";
    public const string Style = "style";
    public const string SizeToken = "sizeToken";
    public const string LineHeight = "lineHeight";

    public static string CreateDefault(
        string sizeToken,
        string fontFamilyId = "theme",
        string weight = "theme.typography.weight",
        string style = "theme.typography.style",
        string lineHeight = "theme.typography.lineHeights.normal")
    {
        return new JsonObject
        {
            [FontFamilyId] = fontFamilyId,
            [Weight] = weight,
            [Style] = style,
            [SizeToken] = sizeToken,
            [LineHeight] = lineHeight,
        }.ToJsonString();
    }

    public static JsonObject Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("inherited", StringComparison.Ordinal))
        {
            return [];
        }

        return JsonNode.Parse(value) as JsonObject ?? [];
    }

    public static bool IsEmpty(string value)
    {
        return Parse(value).Count == 0;
    }

    public static string String(JsonObject value, string key, string fallback = "")
    {
        return value[key] is JsonValue node && node.TryGetValue<string>(out var text)
            ? text
            : fallback;
    }

    public static string NumberString(JsonObject value, string key, string fallback = "")
    {
        if (value[key] is not JsonValue node)
        {
            return fallback;
        }

        if (node.TryGetValue<decimal>(out var number))
        {
            return number.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return node.TryGetValue<string>(out var text) ? text : fallback;
    }
}
