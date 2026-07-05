using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class JsonPath
{
    public static JsonObject ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static bool MergeMissing(JsonObject target, JsonObject defaults)
    {
        var changed = false;
        foreach (var pair in defaults)
        {
            if (!target.TryGetPropertyValue(pair.Key, out var existing) || existing is null)
            {
                target[pair.Key] = pair.Value?.DeepClone();
                changed = true;
                continue;
            }

            if (existing is JsonObject existingObject && pair.Value is JsonObject defaultObject)
            {
                changed |= MergeMissing(existingObject, defaultObject);
            }
        }

        return changed;
    }

    public static JsonNode? Get(JsonObject root, IReadOnlyList<string> path)
    {
        JsonNode? current = root;
        foreach (var part in path)
        {
            if (current is not JsonObject currentObject || !currentObject.TryGetPropertyValue(part, out current))
            {
                return null;
            }
        }

        return current;
    }

    public static void Set(JsonObject root, IReadOnlyList<string> path, JsonNode value)
    {
        var current = root;
        for (var index = 0; index < path.Count - 1; index++)
        {
            var part = path[index];
            if (current[part] is not JsonObject child)
            {
                child = [];
                current[part] = child;
            }

            current = child;
        }

        current[path[^1]] = value;
    }

    public static void SetNumber(JsonObject root, IReadOnlyList<string> path, int value)
    {
        Set(root, path, JsonValue.Create(value)!);
    }

    public static void SetPair(
        JsonObject root,
        string pairValue,
        IReadOnlyList<string> firstPath,
        IReadOnlyList<string> secondPath,
        bool asNumber = true)
    {
        var parts = pairValue.Split('|', 2);
        var first = parts.ElementAtOrDefault(0) ?? "";
        var second = parts.ElementAtOrDefault(1) ?? "";
        Set(root, firstPath, asNumber ? NumberNode(first) : JsonValue.Create(first)!);
        Set(root, secondPath, asNumber ? NumberNode(second) : JsonValue.Create(second)!);
    }

    public static string Pair(JsonObject root, IReadOnlyList<string> firstPath, IReadOnlyList<string> secondPath)
    {
        return $"{NumberString(root, firstPath)}|{NumberString(root, secondPath)}";
    }

    public static string NumberString(JsonObject root, IReadOnlyList<string> path)
    {
        var node = Get(root, path);
        if (node is null)
        {
            return "0";
        }

        if (node.GetValueKind() == JsonValueKind.Number)
        {
            return node.ToJsonString();
        }

        return node.GetValue<string?>() ?? "0";
    }

    public static string NumberString(JsonObject root, IReadOnlyList<string> path, string fallback)
    {
        var value = NumberString(root, path);
        return value == "0" && Get(root, path) is null ? fallback : value;
    }

    public static double NumberDouble(JsonObject root, IReadOnlyList<string> path, double fallback)
    {
        var node = Get(root, path);
        if (node is null)
        {
            return fallback;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<double>(out var number))
            {
                return number;
            }

            if (value.TryGetValue<string>(out var text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    public static string String(JsonObject root, IReadOnlyList<string> path)
    {
        var node = Get(root, path);
        if (node is null)
        {
            return "";
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return node.ToJsonString().Trim('"');
    }

    public static string String(JsonObject root, string key, string fallback)
    {
        return root.TryGetPropertyValue(key, out var node) && node is not null
            ? node.ToString()
            : fallback;
    }

    public static bool Bool(JsonObject root, IReadOnlyList<string> path)
    {
        var node = Get(root, path);
        return node is JsonValue value && value.TryGetValue<bool>(out var boolean) && boolean;
    }

    public static bool Bool(JsonObject root, string key, bool fallback)
    {
        if (!root.TryGetPropertyValue(key, out var node) || node is null)
        {
            return fallback;
        }

        return bool.TryParse(node.ToString(), out var parsed) ? parsed : fallback;
    }

    public static double Number(JsonObject root, string key, double fallback)
    {
        if (!root.TryGetPropertyValue(key, out var node) || node is null)
        {
            return fallback;
        }

        return double.TryParse(node.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public static double NumberAt(JsonObject root, IReadOnlyList<string> path, double fallback)
    {
        return NumberDouble(root, path, fallback);
    }

    public static JsonNode NumberNode(string value)
    {
        return value.Contains('.', StringComparison.Ordinal)
            ? JsonValue.Create(double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue) ? decimalValue : 0)!
            : JsonValue.Create(int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue) ? integerValue : 0)!;
    }
}
