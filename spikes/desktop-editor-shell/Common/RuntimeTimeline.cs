using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class RuntimeTimeline
{
    public static int DurationFrames(string contractJson, string runtimeJson, string animationJson, int storedFallback)
    {
        var contract = Parse(contractJson);
        var runtime = Parse(runtimeJson);
        var declaredDuration = (contract["actions"] as JsonArray)?.OfType<JsonObject>()
            .Where((action) => action["definesModuleDuration"]?.GetValue<bool>() == true)
            .Select((action) => DeclaredActionDuration(runtime, action))
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        var animationDuration = LastAnimationEndFrame(Parse(animationJson));
        var itemActionDuration = LastCollectionItemActionEndFrame(contract, runtime);
        return Math.Max(1, Math.Max(
            declaredDuration > 0 ? declaredDuration : storedFallback,
            Math.Max(animationDuration, itemActionDuration)));
    }

    private static int LastCollectionItemActionEndFrame(JsonObject contract, JsonObject runtime)
    {
        var lastEnd = 0;
        foreach (var collection in (contract["collections"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var collectionKey = collection["sourceCollectionJsonKey"]?.GetValue<string>()
                ?? collection["jsonKey"]?.GetValue<string>()
                ?? "";
            if (string.IsNullOrWhiteSpace(collectionKey) || runtime[collectionKey] is not JsonArray items) continue;
            var sequenceAction = (contract["actions"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault((action) =>
                action["definesModuleDuration"]?.GetValue<bool>() == true
                && action["durationCollectionJsonKey"]?.GetValue<string>() == collectionKey);
            var sequenceKeys = StringArray(sequenceAction?["durationItemNumberKeys"]);
            var cursor = 0;
            foreach (var item in items.OfType<JsonObject>())
            {
                foreach (var action in (collection["itemActions"] as JsonArray)?.OfType<JsonObject>()
                    .Where((candidate) => candidate["extendsModuleDuration"]?.GetValue<bool>() == true) ?? [])
                {
                    var enabledKey = action["durationEnabledInputId"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrWhiteSpace(enabledKey)
                        && item[enabledKey] is JsonValue enabled
                        && enabled.TryGetValue<bool>(out var isEnabled)
                        && !isEnabled)
                    {
                        continue;
                    }
                    var start = cursor + StringArray(action["startAfterItemNumberKeys"]).Sum((key) => NonNegativeInt(item[key]));
                    var durationKey = action["durationInputId"]?.GetValue<string>() ?? "";
                    lastEnd = Math.Max(lastEnd, start + NonNegativeInt(item[durationKey]));
                }
                cursor += sequenceKeys.Sum((key) => NonNegativeInt(item[key]));
            }
        }
        return lastEnd;
    }

    private static string[] StringArray(JsonNode? value) =>
        (value as JsonArray)?.OfType<JsonValue>().Select((item) => item.GetValue<string>()).ToArray() ?? [];

    private static int DeclaredActionDuration(JsonObject runtime, JsonObject action)
    {
        var total = NonNegativeInt(action["durationBaseFrames"]);
        var collectionKey = action["durationCollectionJsonKey"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(collectionKey) || runtime[collectionKey] is not JsonArray items) return total;
        var itemKeys = (action["durationItemNumberKeys"] as JsonArray)?.OfType<JsonValue>()
            .Select((value) => value.GetValue<string>())
            .ToArray() ?? [];
        foreach (var item in items.OfType<JsonObject>())
        {
            foreach (var key in itemKeys) total += NonNegativeInt(item[key]);
        }
        return total;
    }

    private static int LastAnimationEndFrame(JsonObject animation)
    {
        var endFrame = 0;
        foreach (var track in (animation["tracks"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            foreach (var item in (track["events"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                endFrame = Math.Max(endFrame, NonNegativeInt(item["startFrame"]) + NonNegativeInt(item["durationFrames"]));
            }
        }
        return endFrame;
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();

    private static int NonNegativeInt(JsonNode? value)
    {
        if (value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var integer)) return Math.Max(0, integer);
        if (value is JsonValue decimalValue && decimalValue.TryGetValue<decimal>(out var number)) return Math.Max(0, (int)Math.Ceiling(number));
        return 0;
    }
}
