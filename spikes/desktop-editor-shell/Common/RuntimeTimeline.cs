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
        var animationDuration = LastAnimationEndFrame(contract, runtime, Parse(animationJson));
        var animatedItemActionDuration = LastAnimatedCollectionItemActionEndFrame(contract, runtime, Parse(animationJson));
        var itemActionDuration = LastCollectionItemActionEndFrame(contract, runtime);
        return Math.Max(1, Math.Max(
            declaredDuration > 0 ? declaredDuration : storedFallback,
            Math.Max(Math.Max(animationDuration, animatedItemActionDuration), itemActionDuration)));
    }

    private static int LastAnimatedCollectionItemActionEndFrame(JsonObject contract, JsonObject runtime, JsonObject animation)
    {
        var lastEnd = 0;
        foreach (var collection in (contract["collections"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var collectionKey = collection["sourceCollectionJsonKey"]?.GetValue<string>()
                ?? collection["jsonKey"]?.GetValue<string>()
                ?? "";
            if (string.IsNullOrWhiteSpace(collectionKey) || runtime[collectionKey] is not JsonArray items) continue;
            foreach (var action in (collection["itemActions"] as JsonArray)?.OfType<JsonObject>()
                .Where((candidate) => candidate["extendsModuleDuration"]?.GetValue<bool>() == true) ?? [])
            {
                var playFieldId = action["playInputId"]?.GetValue<string>() ?? "";
                var durationKey = action["durationInputId"]?.GetValue<string>() ?? "";
                foreach (var track in (animation["tracks"] as JsonArray)?.OfType<JsonObject>()
                    .Where((candidate) => candidate["fieldId"]?.GetValue<string>() == playFieldId) ?? [])
                {
                    var targetId = track["targetId"]?.GetValue<string>() ?? "";
                    var item = items.OfType<JsonObject>().FirstOrDefault((candidate) => candidate["id"]?.GetValue<string>() == targetId);
                    if (item is null) continue;
                    var origin = RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, playFieldId, targetId);
                    var keyframes = (track["keyframes"] as JsonArray)?.OfType<JsonObject>()
                        .Where((keyframe) => keyframe["enabled"]?.GetValue<bool>() != false)
                        .OrderBy((keyframe) => NonNegativeInt(keyframe["frame"]))
                        .ToList() ?? [];
                    for (var index = 0; index < keyframes.Count; index++)
                    {
                        var keyframe = keyframes[index];
                        if (keyframe["value"] is not JsonValue value
                            || !value.TryGetValue<bool>(out var enabled)
                            || !enabled) continue;
                        var start = origin + NonNegativeInt(keyframe["frame"]);
                        var authoredEnd = start + Math.Max(1, NonNegativeInt(item[durationKey]));
                        var replacementEnd = index + 1 < keyframes.Count
                            ? origin + NonNegativeInt(keyframes[index + 1]["frame"])
                            : int.MaxValue;
                        lastEnd = Math.Max(lastEnd, Math.Min(authoredEnd, replacementEnd));
                    }
                }
            }
        }
        return lastEnd;
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

    private static int LastAnimationEndFrame(JsonObject contract, JsonObject runtime, JsonObject animation)
    {
        var endFrame = 0;
        foreach (var track in (animation["tracks"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var origin = RuntimeAnimationFrameOrigin.ScreenFrame(
                contract,
                runtime,
                track["fieldId"]?.GetValue<string>() ?? "",
                track["targetId"]?.GetValue<string>() ?? "");
            foreach (var item in (track["keyframes"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (item["enabled"] is JsonValue enabled
                    && enabled.TryGetValue<bool>(out var isEnabled)
                    && !isEnabled) continue;
                endFrame = Math.Max(endFrame, origin + NonNegativeInt(item["frame"]) + 1);
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
