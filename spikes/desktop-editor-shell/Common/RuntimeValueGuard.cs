using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class RuntimeValueGuard
{
    public static string RequiredString(JsonObject root, string key, string fieldId)
    {
        var value = JsonPath.String(root, key, "");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw MissingRequired(fieldId);
        }

        return value;
    }

    public static double RequiredThemeNumber(string themeTokensJson, string tokenId, string fieldId)
    {
        if (string.IsNullOrWhiteSpace(tokenId) || !ThemeNumericTokenCatalog.TryGet(tokenId, out var token))
        {
            throw MissingRequired(fieldId);
        }

        var tokens = JsonPath.ParseObject(themeTokensJson);
        var node = JsonPath.Get(tokens, token.Path);
        if (node is null)
        {
            throw new InvalidOperationException($"Theme token '{tokenId}' required by '{fieldId}' is not defined.");
        }

        return JsonPath.NumberAt(tokens, token.Path, 0);
    }

    public static bool RequiredBool(JsonObject root, string key, string fieldId)
    {
        var node = JsonPath.Get(root, [key]);
        if (node is null)
        {
            throw MissingRequired(fieldId);
        }

        if (bool.TryParse(node.ToString(), out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Invalid boolean runtime value for '{fieldId}'.");
    }

    public static double RequiredNumber(JsonObject root, string key, string fieldId)
    {
        var node = JsonPath.Get(root, [key]);
        if (node is null)
        {
            throw MissingRequired(fieldId);
        }

        if (double.TryParse(node.ToString(), System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Invalid numeric runtime value for '{fieldId}'.");
    }

    public static T UseFallback<T>(
        ICollection<string> warnings,
        string fieldId,
        T fallbackValue,
        string reason)
    {
        warnings.Add($"Fallback used · {fieldId} · {reason} · value {Format(fallbackValue)}");
        return fallbackValue;
    }

    private static InvalidOperationException MissingRequired(string fieldId)
    {
        return new InvalidOperationException($"Missing required runtime value for '{fieldId}'.");
    }

    private static string Format<T>(T value)
    {
        return value switch
        {
            null => "<null>",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }
}
