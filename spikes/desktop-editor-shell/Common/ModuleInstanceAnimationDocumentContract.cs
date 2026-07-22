using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class ModuleInstanceAnimationDocumentContract
{
    public static JsonObject Parse(string json, string owner)
    {
        var animation = JsonPath.ParseRequiredObject(json, owner);
        Validate(animation, owner);
        return animation;
    }

    public static void Validate(JsonObject animation, string owner)
    {
        if (animation["schemaVersion"] is not JsonValue schemaVersion
            || !schemaVersion.TryGetValue<int>(out var version)
            || version != 2
            || animation["tracks"] is not JsonArray tracks)
        {
            throw new InvalidOperationException($"{owner} must be a current animation_json v2 document.");
        }

        ValidateRetime(animation["retime"], owner);

        var targets = new HashSet<string>(StringComparer.Ordinal);
        var trackIds = new HashSet<string>(StringComparer.Ordinal);
        var keyframeIds = new HashSet<string>(StringComparer.Ordinal);
        for (var trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
        {
            var track = tracks[trackIndex] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{owner} track at index {trackIndex} must be an object.");
            var context = $"{owner} track at index {trackIndex}";
            var id = RequiredString(track, "id", context);
            var fieldId = RequiredString(track, "fieldId", context);
            var targetId = OptionalString(track, "targetId", context);
            var keyframes = track["keyframes"] as JsonArray
                ?? throw new InvalidOperationException($"{context} must contain a keyframes array.");
            if (!trackIds.Add(id))
            {
                throw new InvalidOperationException($"{owner} has duplicate animation track id '{id}'.");
            }
            if (!targets.Add($"{fieldId}\u001f{targetId}"))
            {
                throw new InvalidOperationException(
                    $"{owner} has duplicate animation target '{fieldId}'/'{targetId}'.");
            }

            ValidateKeyframes(keyframes, keyframeIds, context);
        }
    }

    private static void ValidateRetime(JsonNode? node, string owner)
    {
        if (node is null) return;
        var retime = node as JsonObject
            ?? throw new InvalidOperationException($"{owner} retime must be an object.");
        ValidatePositiveFrameCount(retime["targetDurationFrames"], owner);
        if (retime["targets"] is null) return;
        var targets = retime["targets"] as JsonObject
            ?? throw new InvalidOperationException($"{owner} retime targets must be an object.");
        foreach (var (targetId, targetNode) in targets)
        {
            if (string.IsNullOrWhiteSpace(targetId) || targetNode is not JsonObject target)
            {
                throw new InvalidOperationException($"{owner} has an invalid animation retime target.");
            }
            ValidatePositiveFrameCount(target["targetDurationFrames"], owner);
        }
    }

    private static void ValidateKeyframes(
        JsonArray keyframes,
        ISet<string> keyframeIds,
        string trackContext)
    {
        var frames = new HashSet<int>();
        var previousFrame = -1;
        for (var keyframeIndex = 0; keyframeIndex < keyframes.Count; keyframeIndex++)
        {
            var keyframe = keyframes[keyframeIndex] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{trackContext} keyframe at index {keyframeIndex} must be an object.");
            var context = $"{trackContext} keyframe at index {keyframeIndex}";
            var id = RequiredString(keyframe, "id", context);
            var frame = RequiredInteger(keyframe, "frame", context);
            _ = RequiredString(keyframe, "interpolation", context);
            if (keyframe["enabled"] is not JsonValue enabled
                || !enabled.TryGetValue<bool>(out _))
            {
                throw new InvalidOperationException($"{context} must contain a boolean 'enabled'.");
            }
            if (keyframe["value"] is null)
            {
                throw new InvalidOperationException($"{context} must contain a non-null 'value'.");
            }
            if (!keyframeIds.Add(id))
            {
                throw new InvalidOperationException($"{trackContext} has duplicate keyframe id '{id}'.");
            }
            if (frame < 0 || !frames.Add(frame))
            {
                throw new InvalidOperationException($"{trackContext} has an invalid keyframe frame '{frame}'.");
            }
            if (frame < previousFrame)
            {
                throw new InvalidOperationException(
                    $"{trackContext} keyframes must be stored in ascending frame order.");
            }
            previousFrame = frame;
        }

        if (keyframes.Count == 0)
        {
            throw new InvalidOperationException(
                $"{trackContext} must begin with an enabled origin keyframe at frame 0.");
        }
        var origin = keyframes[0] as JsonObject;
        if (origin is null
            || RequiredInteger(origin, "frame", trackContext) != 0
            || origin["enabled"] is not JsonValue enabledOrigin
            || !enabledOrigin.TryGetValue<bool>(out var originEnabled)
            || !originEnabled)
        {
            throw new InvalidOperationException(
                $"{trackContext} must begin with an enabled origin keyframe at frame 0.");
        }
    }

    private static void ValidatePositiveFrameCount(JsonNode? node, string owner)
    {
        if (node is null) return;
        if (node is not JsonValue value
            || !value.TryGetValue<int>(out var frames)
            || frames <= 0)
        {
            throw new InvalidOperationException($"{owner} has an invalid positive target duration.");
        }
    }

    private static string RequiredString(JsonObject value, string key, string context)
    {
        if (value[key] is not JsonValue node
            || !node.TryGetValue<string>(out var text)
            || string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"{context} must contain a non-empty string '{key}'.");
        }
        return text;
    }

    private static string OptionalString(JsonObject value, string key, string context)
    {
        if (value[key] is null) return "";
        if (value[key] is JsonValue node
            && node.TryGetValue<string>(out var text)
            && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
        throw new InvalidOperationException($"{context} optional '{key}' must be a non-empty string.");
    }

    private static int RequiredInteger(JsonObject value, string key, string context)
    {
        if (value[key] is not JsonValue node || !node.TryGetValue<int>(out var number))
        {
            throw new InvalidOperationException($"{context} must contain an integer '{key}'.");
        }
        return number;
    }
}
