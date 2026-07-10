using System;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record MotionTimingValue(
    int? DurationMs,
    int? DelayMs,
    string? Easing,
    decimal? Intensity)
{
    public static MotionTimingValue Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new MotionTimingValue(null, null, null, null);
        }

        var root = JsonNode.Parse(value) as JsonObject
            ?? throw new InvalidOperationException("Motion timing value must be a JSON object.");

        return new MotionTimingValue(
            OptionalInt(root, "durationMs"),
            OptionalInt(root, "delayMs"),
            OptionalString(root, "easing"),
            OptionalDecimal(root, "intensity"));
    }

    public string ToJsonString()
    {
        var root = new JsonObject();
        if (DurationMs is { } durationMs)
        {
            root["durationMs"] = durationMs;
        }

        if (DelayMs is { } delayMs)
        {
            root["delayMs"] = delayMs;
        }

        if (!string.IsNullOrWhiteSpace(Easing))
        {
            root["easing"] = Easing;
        }

        if (Intensity is { } intensity)
        {
            root["intensity"] = intensity;
        }

        return root.ToJsonString();
    }

    public string Summary()
    {
        var duration = DurationMs is { } durationMs ? $"{durationMs} ms" : "duration unset";
        var delay = DelayMs is { } delayMs ? $"{delayMs} ms" : "delay unset";
        var easing = string.IsNullOrWhiteSpace(Easing) ? "easing unset" : Easing;
        var intensity = Intensity is { } value ? value.ToString("0.##", CultureInfo.InvariantCulture) : "intensity unset";
        return $"{duration} · {delay} · {easing} · {intensity}";
    }

    private static int? OptionalInt(JsonObject root, string key)
    {
        if (root[key] is null)
        {
            return null;
        }

        return root[key] is JsonValue value && value.TryGetValue<int>(out var integer)
            ? integer
            : throw new InvalidOperationException($"Motion timing value '{key}' must be an integer.");
    }

    private static string? OptionalString(JsonObject root, string key)
    {
        if (root[key] is null)
        {
            return null;
        }

        return root[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : throw new InvalidOperationException($"Motion timing value '{key}' must be a string.");
    }

    private static decimal? OptionalDecimal(JsonObject root, string key)
    {
        if (root[key] is null)
        {
            return null;
        }

        if (root[key] is JsonValue value)
        {
            if (value.TryGetValue<decimal>(out var decimalValue))
            {
                return decimalValue;
            }

            if (value.TryGetValue<double>(out var doubleValue))
            {
                return (decimal)doubleValue;
            }

            if (value.TryGetValue<string>(out var text)
                && decimal.TryParse(
                    text.Replace(",", ".", StringComparison.Ordinal),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return parsed;
            }
        }

        throw new InvalidOperationException($"Motion timing value '{key}' must be numeric.");
    }
}
