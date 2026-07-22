using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ComponentPreviewActions
{
    public static IReadOnlyList<ComponentPreviewActionDefinition> ReadWithEmbedded(
        JsonObject preview,
        Func<string, JsonObject> componentVariantRuntimeContract)
    {
        var definitions = Read(preview).ToList();
        if (preview["collections"] is not JsonArray collections) return definitions;
        foreach (var collection in collections.OfType<JsonObject>())
        {
            var collectionJsonKey = JsonString(collection, "jsonKey");
            var itemRuntimeContractJsonKey = JsonString(collection, "itemRuntimeContractJsonKey");
            if (!string.IsNullOrWhiteSpace(collectionJsonKey)
                && !string.IsNullOrWhiteSpace(itemRuntimeContractJsonKey)
                && preview[collectionJsonKey] is JsonArray runtimeItems)
            {
                foreach (var item in runtimeItems.OfType<JsonObject>())
                {
                    var itemId = JsonString(item, "id");
                    if (string.IsNullOrWhiteSpace(itemId)
                        || item[itemRuntimeContractJsonKey] is not JsonObject itemContract) continue;
                    definitions.AddRange(Read(itemContract)
                        .Where((action) => !action.IsCollectionItemAction)
                        .Select((action) => action with
                        {
                            Id = $"embedded:{collectionJsonKey}:{itemId}:{itemRuntimeContractJsonKey}:{action.Id}",
                            CollectionJsonKey = collectionJsonKey,
                            CollectionItemId = itemId,
                            TargetJsonPath = itemRuntimeContractJsonKey,
                        }));
                }
            }
            var componentItems = collection["componentItems"] as JsonObject;
            if (string.IsNullOrWhiteSpace(collectionJsonKey) || componentItems is null) continue;
            var variantReferenceJsonKey = JsonString(componentItems, "variantReferenceJsonKey");
            var inputsJsonKey = JsonString(componentItems, "inputsJsonKey");
            if (string.IsNullOrWhiteSpace(variantReferenceJsonKey) || string.IsNullOrWhiteSpace(inputsJsonKey)) continue;
            if (preview[collectionJsonKey] is not JsonArray items) continue;
            foreach (var item in items.OfType<JsonObject>())
            {
                var itemId = JsonString(item, "id");
                var variantReference = JsonString(item, variantReferenceJsonKey);
                if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(variantReference)) continue;
                var childContract = componentVariantRuntimeContract(variantReference);
                definitions.AddRange(Read(childContract)
                    .Where((action) => !action.IsCollectionItemAction)
                    .Select((action) => action with
                    {
                        Id = $"embedded:{collectionJsonKey}:{itemId}:{inputsJsonKey}:{action.Id}",
                        CollectionJsonKey = collectionJsonKey,
                        CollectionItemId = itemId,
                        TargetJsonPath = inputsJsonKey,
                    }));
            }
        }
        return definitions;
    }

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

    public static void SetStoredValue(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string key,
        string value)
    {
        var target = Target(preview, action);
        if (target is null) return;
        target[key] = target[key] switch
        {
            JsonValue existing when existing.TryGetValue<bool>(out _) => JsonValue.Create(BooleanText.Parse(value)),
            JsonValue existing when existing.TryGetValue<int>(out _)
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) => JsonValue.Create(integer),
            JsonValue existing when existing.TryGetValue<double>(out _)
                && double.TryParse(value.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
            _ => JsonValue.Create(value),
        };
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

        var item = preview[action.CollectionJsonKey] is JsonArray items
            ? items.OfType<JsonObject>().FirstOrDefault((item) =>
                item["id"] is JsonValue value
                && value.TryGetValue<string>(out var id)
                && id == action.CollectionItemId)
            : null;
        if (item is null || string.IsNullOrWhiteSpace(action.TargetJsonPath)) return item;
        JsonNode? target = item;
        foreach (var segment in action.TargetJsonPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            target = target is JsonObject owner ? owner[segment] : null;
        }
        return target as JsonObject;
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
        var durationStateCollectionJsonKey = JsonString(action, "durationStateCollectionJsonKey");
        var durationOwnerTimeline = JsonBoolean(action, "durationOwnerTimeline", false);
        var durationSeconds = JsonNumber(action, "durationSeconds", 0);
        var timeJsonKey = JsonString(action, "timeJsonKey");
        if (string.IsNullOrWhiteSpace(playInputId)
            || (string.IsNullOrWhiteSpace(durationInputId)
                && string.IsNullOrWhiteSpace(durationBehaviorTimingInputId)
                && string.IsNullOrWhiteSpace(durationCollectionJsonKey)
                && string.IsNullOrWhiteSpace(durationThemeToken)
                && string.IsNullOrWhiteSpace(durationStateCollectionJsonKey)
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
            durationStateCollectionJsonKey,
            JsonString(action, "durationStateIdJsonKey"),
            JsonString(action, "durationEnterMotionJsonKey"),
            JsonString(action, "durationExitMotionJsonKey"),
            JsonStringArray(action, "durationAdditionalThemeTokens"),
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
            JsonString(action, "targetFromJsonKey"),
            ParseTargetOptions(action),
            collectionJsonKey,
            collectionItemId,
            "",
            JsonString(action, "visibleWhenItemJsonKey"),
            JsonStringArray(action, "visibleWhenItemValues"));
    }

    public static double MotionStateTransitionDurationMilliseconds(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string themeTokensJson)
    {
        if (string.IsNullOrWhiteSpace(action.DurationStateCollectionJsonKey)) return 0;
        var target = Target(preview, action);
        if (target?[action.DurationStateCollectionJsonKey] is not JsonArray states) return 0;
        var theme = JsonPath.ParseRequiredObject(themeTokensJson, "Theme tokens");
        var stateIdKey = string.IsNullOrWhiteSpace(action.DurationStateIdJsonKey)
            ? "id"
            : action.DurationStateIdJsonKey;
        var targetId = JsonString(target, action.TargetInputId);
        var fromId = JsonString(target, action.TargetFromJsonKey);
        var entering = states.OfType<JsonObject>().FirstOrDefault((state) => JsonString(state, stateIdKey) == targetId);
        var outgoing = states.OfType<JsonObject>().FirstOrDefault((state) => JsonString(state, stateIdKey) == fromId);
        var duration = Math.Max(
            MotionDurationMilliseconds(theme, entering?[action.DurationEnterMotionJsonKey] as JsonObject),
            MotionDurationMilliseconds(theme, outgoing?[action.DurationExitMotionJsonKey] as JsonObject));
        foreach (var token in action.DurationAdditionalThemeTokens)
        {
            duration = Math.Max(duration, ThemeTokenNumber(theme, token));
        }
        return Math.Max(0, duration);
    }

    private static double MotionDurationMilliseconds(JsonObject theme, JsonObject? motion)
    {
        if (motion is null) return 0;
        var transition = JsonString(motion, "transition");
        var fade = motion["fade"] is JsonValue fadeValue
            && fadeValue.TryGetValue<bool>(out var enabled)
            && enabled;
        if (transition == "none" && !fade) return 0;
        var timingKey = transition == "none" ? "fade" : transition;
        var timing = theme["motion"]?["transitions"]?[timingKey] as JsonObject;
        return timing is null
            ? 0
            : Math.Max(0, JsonNumber(timing, "delayMs", 0) + JsonNumber(timing, "durationMs", 0));
    }

    private static double ThemeTokenNumber(JsonObject theme, string token)
    {
        JsonNode? current = theme;
        foreach (var segment in token.Split('.', StringSplitOptions.RemoveEmptyEntries)
                     .SkipWhile((segment) => segment == "theme"))
        {
            current = current is JsonObject owner ? owner[segment] : null;
        }
        return current is JsonValue value && value.TryGetValue<double>(out var number) ? number : 0;
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

    private static IReadOnlyList<FieldOption> ParseTargetOptions(JsonObject action)
    {
        return action["targetOptions"] is JsonArray options
            ? options.OfType<JsonObject>()
                .Select((option) => new FieldOption(JsonString(option, "value"), JsonString(option, "label")))
                .Where((option) => !string.IsNullOrWhiteSpace(option.Value))
                .ToList()
            : [];
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
    string DurationStateCollectionJsonKey,
    string DurationStateIdJsonKey,
    string DurationEnterMotionJsonKey,
    string DurationExitMotionJsonKey,
    IReadOnlyList<string> DurationAdditionalThemeTokens,
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
    string TargetFromJsonKey,
    IReadOnlyList<FieldOption> TargetOptions,
    string CollectionJsonKey = "",
    string CollectionItemId = "",
    string TargetJsonPath = "",
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
