using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

public sealed record IconThemeTokenMapping(string Token, string Category, string File, string Description);

public static class IconTokenRules
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

    public static JsonObject Categories(JsonObject tokens)
    {
        var categories = new SortedDictionary<string, JsonArray>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in tokens.OrderBy((pair) => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var tokenObject = pair.Value as JsonObject ?? throw new InvalidOperationException("Invalid icon token object.");
            var category = JsonPath.RequiredString(tokenObject, "category", pair.Key);
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
        if (mapping["schemaVersion"]?.GetValue<int>() != 1) throw new InvalidOperationException("Invalid icon mapping version.");
        var tokens = JsonPath.RequiredObject(mapping, "tokens", "Icon Theme mapping");
        var categories = JsonPath.RequiredObject(mapping, "categories", "Icon Theme mapping");
        if (!JsonNode.DeepEquals(categories, Categories(tokens))) throw new InvalidOperationException("Icon categories must match their explicit tokens.");

        return tokens
            .OrderBy((pair) => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select((pair) =>
            {
                var tokenObject = pair.Value as JsonObject ?? throw new InvalidOperationException("Invalid icon token object.");
                var file = JsonPath.RequiredString(tokenObject, "file", pair.Key);
                if (!Regex.IsMatch(pair.Key, "^[a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)*$")
                    || Path.GetFileName(file) != file || file.Contains('\\') || file.Contains(':')
                    || !file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Invalid icon token identity or exact file reference.");
                var description = tokenObject["description"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing icon description.");
                return new IconThemeTokenMapping(
                    pair.Key,
                    JsonPath.RequiredString(tokenObject, "category", pair.Key),
                    file,
                    description);
            })
            .ToList();
    }
}
