using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ComponentPreviewActions
{
    public static IReadOnlyList<ComponentPreviewActionDefinition> Read(JsonObject preview)
    {
        if (preview["actions"] is JsonArray actions)
        {
            return actions
                .OfType<JsonObject>()
                .Select(ParseAction)
                .Where((action) => action is not null)
                .Cast<ComponentPreviewActionDefinition>()
                .ToList();
        }

        return [];
    }

    private static ComponentPreviewActionDefinition? ParseAction(JsonObject action)
    {
        var playInputId = JsonString(action, "playInputId");
        var durationInputId = JsonString(action, "durationInputId");
        var durationCollectionJsonKey = JsonString(action, "durationCollectionJsonKey");
        var durationSeconds = JsonNumber(action, "durationSeconds", 0);
        var timeJsonKey = JsonString(action, "timeJsonKey");
        if (string.IsNullOrWhiteSpace(playInputId)
            || (string.IsNullOrWhiteSpace(durationInputId)
                && string.IsNullOrWhiteSpace(durationCollectionJsonKey)
                && durationSeconds <= 0)
            || string.IsNullOrWhiteSpace(timeJsonKey))
        {
            return null;
        }

        var id = JsonString(action, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = playInputId;
        }

        var label = JsonString(action, "label");
        if (string.IsNullOrWhiteSpace(label))
        {
            label = "Play";
        }

        return new ComponentPreviewActionDefinition(
            id,
            label,
            playInputId,
            durationInputId,
            durationSeconds,
            durationCollectionJsonKey,
            JsonStringArray(action, "durationItemNumberKeys"),
            JsonStringArray(action, "durationCollectionMultiplierNumberKeys"),
            JsonNumber(action, "durationBaseFrames", 0),
            timeJsonKey,
            ParseTimeUnit(JsonString(action, "timeUnit")),
            JsonBoolean(action, "prewarmFrames", true),
            JsonString(action, "prewarmWhenJsonKey"),
            JsonString(action, "prewarmWhenConfigPath"),
            JsonString(action, "prewarmWhenValue"),
            JsonStringArray(action, "activateInputIds"),
            JsonStringArray(action, "deactivateInputIds"));
    }

    private static ComponentPreviewActionTimeUnit ParseTimeUnit(string value)
    {
        return value.Equals("frames", StringComparison.OrdinalIgnoreCase)
            ? ComponentPreviewActionTimeUnit.Frames
            : ComponentPreviewActionTimeUnit.Seconds;
    }

    private static IReadOnlyList<string> JsonStringArray(JsonObject owner, string key)
    {
        if (owner[key] is not JsonArray values)
        {
            return [];
        }

        return values
            .OfType<JsonValue>()
            .Select((value) => value.TryGetValue<string>(out var text) ? text : "")
            .Where((text) => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private static string JsonString(JsonObject owner, string key)
    {
        return owner[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : "";
    }

    private static double JsonNumber(JsonObject owner, string key, double fallback)
    {
        if (owner[key] is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<double>(out var number))
        {
            return number;
        }

        return value.TryGetValue<string>(out var text)
            && double.TryParse(text.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
    }

    private static bool JsonBoolean(JsonObject owner, string key, bool fallback)
    {
        if (owner[key] is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        return value.TryGetValue<string>(out var text)
            ? BooleanText.Parse(text)
            : fallback;
    }
}

internal sealed record ComponentPreviewActionDefinition(
    string Id,
    string Label,
    string PlayInputId,
    string DurationInputId,
    double DurationSeconds,
    string DurationCollectionJsonKey,
    IReadOnlyList<string> DurationItemNumberKeys,
    IReadOnlyList<string> DurationCollectionMultiplierNumberKeys,
    double DurationBaseFrames,
    string TimeJsonKey,
    ComponentPreviewActionTimeUnit TimeUnit,
    bool PrewarmFrames,
    string PrewarmWhenJsonKey,
    string PrewarmWhenConfigPath,
    string PrewarmWhenValue,
    IReadOnlyList<string> ActivateInputIds,
    IReadOnlyList<string> DeactivateInputIds);

internal enum ComponentPreviewActionTimeUnit
{
    Seconds,
    Frames,
}
