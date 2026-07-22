using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class JsonPath
{
    public static JsonObject ParseRequiredObject(string json, string context)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"{context} cannot be blank.");
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException($"{context} must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{context} contains invalid JSON.", exception);
        }
    }

    public static JsonArray ParseRequiredArray(string json, string context)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"{context} cannot be blank.");
        }

        try
        {
            return JsonNode.Parse(json) as JsonArray
                ?? throw new InvalidOperationException($"{context} must be a JSON array.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{context} contains invalid JSON.", exception);
        }
    }

    public static JsonObject RequiredObject(JsonObject root, string key, string context)
    {
        return root[key] as JsonObject
            ?? throw new InvalidOperationException($"{context} must contain an object '{key}'.");
    }

    public static JsonArray RequiredArray(JsonObject root, string key, string context)
    {
        return root[key] as JsonArray
            ?? throw new InvalidOperationException($"{context} must contain an array '{key}'.");
    }

    public static string RequiredString(
        JsonObject root,
        string key,
        string context,
        bool allowEmpty = false)
    {
        if (root[key] is not JsonValue value
            || !value.TryGetValue<string>(out var text)
            || (!allowEmpty && string.IsNullOrWhiteSpace(text)))
        {
            var qualifier = allowEmpty ? "a string" : "a non-empty string";
            throw new InvalidOperationException($"{context} must contain {qualifier} '{key}'.");
        }

        return text;
    }

    public static bool RequiredBoolean(JsonObject root, string key, string context)
    {
        if (root[key] is not JsonValue value || !value.TryGetValue<bool>(out var result))
        {
            throw new InvalidOperationException($"{context} must contain an explicit boolean '{key}'.");
        }

        return result;
    }

    public static double RequiredNumber(JsonObject root, string key, string context)
    {
        if (root[key] is not JsonValue value
            || !value.TryGetValue<double>(out var result)
            || double.IsNaN(result)
            || double.IsInfinity(result))
        {
            throw new InvalidOperationException($"{context} must contain a finite number '{key}'.");
        }

        return result;
    }

    public static int RequiredInteger(JsonObject root, string key, string context)
    {
        var number = RequiredNumber(root, key, context);
        if (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue)
        {
            throw new InvalidOperationException($"{context} must contain an integer '{key}'.");
        }

        return (int)number;
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

    public static bool Remove(JsonObject root, IReadOnlyList<string> path)
    {
        if (path.Count == 0)
        {
            return false;
        }

        var current = root;
        for (var index = 0; index < path.Count - 1; index++)
        {
            if (current[path[index]] is not JsonObject child)
            {
                return false;
            }

            current = child;
        }

        return current.Remove(path[^1]);
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
        return ParseRequiredNumberNode(value, "Numeric value");
    }

    public static JsonNode ParseRequiredNumberNode(string value, string context)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || double.IsNaN(number)
            || double.IsInfinity(number))
        {
            throw new InvalidOperationException($"{context} must be a finite number.");
        }

        return value.Contains('.', StringComparison.Ordinal)
            || value.Contains('e', StringComparison.OrdinalIgnoreCase)
            ? JsonValue.Create(number)!
            : long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                ? JsonValue.Create(integer)!
                : JsonValue.Create(number)!;
    }
}
