using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record IconThemeTokenMapping(string Token, string Category, string File, string Description);

internal static class IconTokenRules
{
    public static string TokenFromText(string value)
    {
        var token = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9_]+", "_");
        token = Regex.Replace(token, "_+", "_").Trim('_');
        return token;
    }

    public static string CategoryFromToken(string token)
    {
        var index = token.IndexOf('_', StringComparison.Ordinal);
        return index <= 0 ? "misc" : token[..index];
    }

    public static HashSet<string> SvgTokenSet(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        return Directory
            .EnumerateFiles(directory, "*.svg", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where((token) => !string.IsNullOrWhiteSpace(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    public static JsonObject BuildMapping(string currentMappingJson, HashSet<string> commonTokens)
    {
        var current = JsonPath.ParseRequiredObject(currentMappingJson, "Icon Theme mapping");
        var currentTokens = current["tokens"] as JsonObject ?? [];
        var nextTokens = new JsonObject();
        foreach (var token in commonTokens.OrderBy((token) => token, StringComparer.OrdinalIgnoreCase))
        {
            var existing = currentTokens[token] as JsonObject ?? [];
            var category = JsonPath.String(existing, ["category"]);
            if (string.IsNullOrWhiteSpace(category)) category = CategoryFromToken(token);
            nextTokens[token] = new JsonObject
            {
                ["category"] = category,
                ["file"] = $"{token}.svg",
                ["description"] = JsonPath.String(existing, ["description"]),
            };
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["tokens"] = nextTokens,
            ["categories"] = Categories(nextTokens),
        };
    }

    public static JsonObject Categories(JsonObject tokens)
    {
        var categories = new SortedDictionary<string, JsonArray>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in tokens.OrderBy((pair) => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var tokenObject = pair.Value as JsonObject ?? [];
            var category = JsonPath.String(tokenObject, ["category"]);
            if (string.IsNullOrWhiteSpace(category)) category = CategoryFromToken(pair.Key);
            if (!categories.TryGetValue(category, out var categoryTokens))
            {
                categoryTokens = [];
                categories[category] = categoryTokens;
            }

            categoryTokens.Add(pair.Key);
        }

        return new JsonObject(categories.Select((pair) => KeyValuePair.Create<string, JsonNode?>(pair.Key, pair.Value)));
    }

    public static IReadOnlyList<IconThemeTokenMapping> Tokens(string mappingJson)
    {
        var mapping = JsonPath.ParseRequiredObject(mappingJson, "Icon Theme mapping");
        var tokens = mapping["tokens"] as JsonObject;
        if (tokens is null) return [];

        return tokens
            .OrderBy((pair) => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select((pair) =>
            {
                var tokenObject = pair.Value as JsonObject ?? [];
                return new IconThemeTokenMapping(
                    pair.Key,
                    JsonPath.String(tokenObject, ["category"]),
                    JsonPath.String(tokenObject, ["file"]),
                    JsonPath.String(tokenObject, ["description"]));
            })
            .ToList();
    }
}
