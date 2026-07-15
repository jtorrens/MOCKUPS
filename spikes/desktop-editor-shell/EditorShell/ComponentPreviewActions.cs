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
        var definitions = new List<ComponentPreviewActionDefinition>();
        if (preview["actions"] is JsonArray actions)
        {
            definitions.AddRange(actions
                .OfType<JsonObject>()
                .Select((action) => ParseAction(action))
                .Where((action) => action is not null)
                .Cast<ComponentPreviewActionDefinition>());
        }

        if (preview["collections"] is not JsonArray collections)
        {
            return definitions;
        }

        foreach (var collection in collections.OfType<JsonObject>())
        {
            var collectionJsonKey = JsonString(collection, "jsonKey");
            if (string.IsNullOrWhiteSpace(collectionJsonKey)
                || collection["itemActions"] is not JsonArray itemActions)
            {
                continue;
            }

            var items = DesignPreviewTestValues.CollectionItems(
                preview,
                new RuntimeInputCollectionDefinition(
                    JsonString(collection, "id"),
                    JsonString(collection, "label"),
                    collectionJsonKey,
                    string.IsNullOrWhiteSpace(JsonString(collection, "itemLabel"))
                        ? "Item"
                        : JsonString(collection, "itemLabel"),
                    [],
                    JsonString(collection, "sourceCollectionJsonKey")));
            foreach (var item in items)
            {
                var itemId = JsonString(item, "id");
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                foreach (var itemAction in itemActions.OfType<JsonObject>())
                {
                    var action = itemAction.DeepClone() as JsonObject ?? new JsonObject();
                    var baseId = JsonString(action, "id");
                    if (string.IsNullOrWhiteSpace(baseId))
                    {
                        continue;
                    }
                    action["id"] = $"{baseId}:{itemId}";
                    var parsed = ParseAction(action, collectionJsonKey, itemId);
                    if (parsed is not null)
                    {
                        definitions.Add(parsed);
                    }
                }
            }
        }

        return definitions;
    }

    public static JsonNode? Value(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string key)
    {
        return Target(preview, action)?[key];
    }

    public static void SetValue(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string key,
        object value)
    {
        var target = Target(preview, action);
        if (target is not null)
        {
            target[key] = JsonValue.Create(value);
        }
    }

    public static void RemoveValue(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string key)
    {
        Target(preview, action)?.Remove(key);
    }

    public static bool AppliesToItem(ComponentPreviewActionDefinition action, JsonObject item)
    {
        if (string.IsNullOrWhiteSpace(action.VisibleWhenItemJsonKey)
            || action.VisibleWhenItemValues is not { Count: > 0 })
        {
            return true;
        }

        var current = JsonString(item, action.VisibleWhenItemJsonKey);
        return action.VisibleWhenItemValues.Contains(current, StringComparer.Ordinal);
    }

    public static bool IsApplicable(JsonObject preview, ComponentPreviewActionDefinition action)
    {
        if (!action.IsCollectionItemAction) return true;
        var item = Target(preview, action);
        return item is not null && AppliesToItem(action, item);
    }

    public static IReadOnlyList<ComponentPreviewActionDefinition> ReadApplicable(JsonObject preview)
    {
        return Read(preview).Where((action) => IsApplicable(preview, action)).ToList();
    }

    private static JsonObject? Target(
        JsonObject preview,
        ComponentPreviewActionDefinition action)
    {
        if (!action.IsCollectionItemAction)
        {
            return preview;
        }

        return preview[action.CollectionJsonKey] is JsonArray items
            ? items.OfType<JsonObject>().FirstOrDefault((item) =>
                item["id"] is JsonValue value
                && value.TryGetValue<string>(out var id)
                && id == action.CollectionItemId)
            : null;
    }

    private static ComponentPreviewActionDefinition? ParseAction(
        JsonObject action,
        string collectionJsonKey = "",
        string collectionItemId = "")
    {
        var playInputId = JsonString(action, "playInputId");
        var durationInputId = JsonString(action, "durationInputId");
        var durationBehaviorTimingInputId = JsonString(action, "durationBehaviorTimingInputId");
        var durationCollectionJsonKey = JsonString(action, "durationCollectionJsonKey");
        var durationThemeToken = JsonString(action, "durationThemeToken");
        var durationOwnerTimeline = JsonBoolean(action, "durationOwnerTimeline", false);
        var durationSeconds = JsonNumber(action, "durationSeconds", 0);
        var timeJsonKey = JsonString(action, "timeJsonKey");
        if (string.IsNullOrWhiteSpace(playInputId)
            || (string.IsNullOrWhiteSpace(durationInputId)
                && string.IsNullOrWhiteSpace(durationBehaviorTimingInputId)
                && string.IsNullOrWhiteSpace(durationCollectionJsonKey)
                && string.IsNullOrWhiteSpace(durationThemeToken)
                && !durationOwnerTimeline
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
            durationBehaviorTimingInputId,
            durationSeconds,
            durationCollectionJsonKey,
            durationThemeToken,
            JsonStringArray(action, "durationItemNumberKeys"),
            JsonStringArray(action, "durationCollectionMultiplierNumberKeys"),
            JsonNumber(action, "durationBaseFrames", 0),
            durationOwnerTimeline,
            timeJsonKey,
            ParseTimeUnit(JsonString(action, "timeUnit")),
            ParseCompletionBehavior(JsonString(action, "completionBehavior")),
            JsonBoolean(action, "prewarmFrames", true),
            JsonString(action, "prewarmWhenJsonKey"),
            JsonString(action, "prewarmWhenConfigPath"),
            JsonString(action, "prewarmWhenValue"),
            JsonStringArray(action, "activateInputIds"),
            JsonStringArray(action, "deactivateInputIds"),
            JsonString(action, "targetInputId"),
            ParseTargetMode(JsonString(action, "targetMode")),
            collectionJsonKey,
            collectionItemId,
            JsonString(action, "visibleWhenItemJsonKey"),
            JsonStringArray(action, "visibleWhenItemValues"));
    }

    private static ComponentPreviewActionTargetMode ParseTargetMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "" => ComponentPreviewActionTargetMode.None,
            "toggle" => ComponentPreviewActionTargetMode.Toggle,
            "option" => ComponentPreviewActionTargetMode.Option,
            "value" => ComponentPreviewActionTargetMode.Value,
            _ => throw new InvalidOperationException($"Unknown component preview action targetMode '{value}'."),
        };
    }

    private static ComponentPreviewActionTimeUnit ParseTimeUnit(string value)
    {
        if (value.Equals("milliseconds", StringComparison.OrdinalIgnoreCase))
        {
            return ComponentPreviewActionTimeUnit.Milliseconds;
        }
        return value.Equals("frames", StringComparison.OrdinalIgnoreCase)
            ? ComponentPreviewActionTimeUnit.Frames
            : ComponentPreviewActionTimeUnit.Seconds;
    }

    private static ComponentPreviewActionCompletionBehavior ParseCompletionBehavior(string value)
    {
        if (value.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            return ComponentPreviewActionCompletionBehavior.Reset;
        }
        if (value.Equals("holdFinal", StringComparison.OrdinalIgnoreCase))
        {
            return ComponentPreviewActionCompletionBehavior.HoldFinal;
        }
        throw new InvalidOperationException($"Missing or unknown component preview action completionBehavior '{value}'.");
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
    string DurationBehaviorTimingInputId,
    double DurationSeconds,
    string DurationCollectionJsonKey,
    string DurationThemeToken,
    IReadOnlyList<string> DurationItemNumberKeys,
    IReadOnlyList<string> DurationCollectionMultiplierNumberKeys,
    double DurationBaseFrames,
    bool DurationOwnerTimeline,
    string TimeJsonKey,
    ComponentPreviewActionTimeUnit TimeUnit,
    ComponentPreviewActionCompletionBehavior CompletionBehavior,
    bool PrewarmFrames,
    string PrewarmWhenJsonKey,
    string PrewarmWhenConfigPath,
    string PrewarmWhenValue,
    IReadOnlyList<string> ActivateInputIds,
    IReadOnlyList<string> DeactivateInputIds,
    string TargetInputId,
    ComponentPreviewActionTargetMode TargetMode,
    string CollectionJsonKey = "",
    string CollectionItemId = "",
    string VisibleWhenItemJsonKey = "",
    IReadOnlyList<string>? VisibleWhenItemValues = null)
{
    public bool IsCollectionItemAction => !string.IsNullOrWhiteSpace(CollectionJsonKey)
        && !string.IsNullOrWhiteSpace(CollectionItemId);
}

internal enum ComponentPreviewActionTargetMode
{
    None,
    Toggle,
    Option,
    Value,
}

internal enum ComponentPreviewActionTimeUnit
{
    Seconds,
    Frames,
    Milliseconds,
}

internal enum ComponentPreviewActionCompletionBehavior
{
    Reset,
    HoldFinal,
}
