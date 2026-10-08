using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

public static class ModuleInstanceAnimationDocumentContract
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

        ValidateRootProperties(animation, owner);

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

    public static void ValidateRootProperties(JsonObject animation, string owner)
    {
        foreach (var (key, _) in animation)
        {
            if (key is not ("schemaVersion" or "tracks"))
            {
                throw new InvalidOperationException($"{owner} contains undeclared property '{key}'.");
            }
        }
    }

    private static void ValidateKeyframes(
        JsonArray keyframes,
        ISet<string> keyframeIds,
        string trackContext)
    {
        var frames = new HashSet<int>();
        var previousFrame = int.MinValue;
        var hasEnabledKeyframe = false;
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
                || !enabled.TryGetValue<bool>(out var isEnabled))
            {
                throw new InvalidOperationException($"{context} must contain a boolean 'enabled'.");
            }
            hasEnabledKeyframe |= isEnabled;
            if (keyframe["value"] is null)
            {
                throw new InvalidOperationException($"{context} must contain a non-null 'value'.");
            }
            if (!keyframeIds.Add(id))
            {
                throw new InvalidOperationException($"{trackContext} has duplicate keyframe id '{id}'.");
            }
            if (!frames.Add(frame))
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

        if (keyframes.Count == 0 || !hasEnabledKeyframe)
        {
            throw new InvalidOperationException(
                $"{trackContext} must contain at least one enabled keyframe.");
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
