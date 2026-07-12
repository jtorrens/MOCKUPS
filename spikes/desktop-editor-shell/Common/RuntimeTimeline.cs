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
        return Math.Max(1, Math.Max(declaredDuration > 0 ? declaredDuration : storedFallback, animationDuration));
    }

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
