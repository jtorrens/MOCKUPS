using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

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

        if (preview["animation"] is JsonObject animation)
        {
            var legacy = ParseAction(animation);
            return legacy is null ? [] : [legacy];
        }

        return [];
    }

    private static ComponentPreviewActionDefinition? ParseAction(JsonObject action)
    {
        var playInputId = JsonString(action, "playInputId");
        var durationInputId = JsonString(action, "durationInputId");
        var durationSeconds = JsonNumber(action, "durationSeconds", 0);
        var timeJsonKey = JsonString(action, "timeJsonKey");
        if (string.IsNullOrWhiteSpace(playInputId)
            || (string.IsNullOrWhiteSpace(durationInputId) && durationSeconds <= 0)
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
            timeJsonKey,
            JsonStringArray(action, "activateInputIds"));
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
}

internal sealed record ComponentPreviewActionDefinition(
    string Id,
    string Label,
    string PlayInputId,
    string DurationInputId,
    double DurationSeconds,
    string TimeJsonKey,
    IReadOnlyList<string> ActivateInputIds);
