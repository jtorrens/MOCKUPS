using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ModuleInstanceAnimationDocument
{
    private readonly JsonObject _root;
    private readonly JsonArray _tracks;

    public ModuleInstanceAnimationDocument(string json)
    {
        _root = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject
            ?? throw new InvalidOperationException("animation_json must be an object.");
        if (_root["schemaVersion"]?.GetValue<int>() != 2)
            throw new InvalidOperationException("Animation editor requires animation_json schemaVersion 2.");
        _tracks = _root["tracks"] as JsonArray
            ?? throw new InvalidOperationException("animation_json v2 requires tracks.");
    }

    public IReadOnlyList<AnimationTrackView> Tracks => _tracks.OfType<JsonObject>()
        .Select(ReadTrack)
        .ToList();

    public AnimationTrackView? Track(string fieldId, string targetId) =>
        Tracks.FirstOrDefault((track) => track.FieldId == fieldId && track.TargetId == targetId);

    public bool HasTrack(string fieldId, string targetId) => Track(fieldId, targetId) is not null;

    public int? TargetDurationFrames(string targetId)
    {
        var retime = _root["retime"] as JsonObject;
        var node = string.IsNullOrWhiteSpace(targetId)
            ? retime?["targetDurationFrames"]
            : ((retime?["targets"] as JsonObject)?[targetId] as JsonObject)?["targetDurationFrames"];
        return node is JsonValue value && value.TryGetValue<int>(out var duration) && duration > 0
            ? duration
            : null;
    }

    public void SetTargetDurationFrames(string targetId, int? duration)
    {
        var retime = _root["retime"] as JsonObject ?? new JsonObject();
        _root["retime"] = retime;
        if (string.IsNullOrWhiteSpace(targetId))
        {
            if (duration is > 0) retime["targetDurationFrames"] = duration.Value;
            else retime.Remove("targetDurationFrames");
        }
        else
        {
            var targets = retime["targets"] as JsonObject ?? new JsonObject();
            retime["targets"] = targets;
            var target = targets[targetId] as JsonObject ?? new JsonObject();
            targets[targetId] = target;
            if (duration is > 0) target["targetDurationFrames"] = duration.Value;
            else
            {
                target.Remove("targetDurationFrames");
                if (target.Count == 0) targets.Remove(targetId);
            }
            if (targets.Count == 0) retime.Remove("targets");
        }
        if (retime.Count == 0) _root.Remove("retime");
    }

    public void AddTrack(
        string fieldId,
        string targetId,
        JsonNode initialValue,
        string interpolation)
    {
        if (HasTrack(fieldId, targetId)) return;
        var track = new JsonObject
        {
            ["id"] = $"track-{Guid.NewGuid():N}",
            ["fieldId"] = fieldId,
            ["keyframes"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = $"keyframe-{Guid.NewGuid():N}",
                    ["frame"] = 0,
                    ["value"] = initialValue.DeepClone(),
                    ["interpolation"] = interpolation,
                    ["enabled"] = true,
                },
            },
        };
        if (!string.IsNullOrWhiteSpace(targetId)) track["targetId"] = targetId;
        _tracks.Add(track);
    }

    public void RemoveTrack(string fieldId, string targetId)
    {
        var track = TrackObject(fieldId, targetId);
        if (track is not null) _tracks.Remove(track);
    }

    public void RemoveTarget(string targetId)
    {
        foreach (var track in _tracks.OfType<JsonObject>()
            .Where((candidate) => (candidate["targetId"]?.GetValue<string>() ?? "") == targetId)
            .ToList())
        {
            _tracks.Remove(track);
        }
        SetTargetDurationFrames(targetId, null);
    }

    public void DuplicateTargets(IReadOnlyDictionary<string, string> targetIdMappings)
    {
        foreach (var source in _tracks.OfType<JsonObject>()
            .Where((track) => targetIdMappings.ContainsKey(track["targetId"]?.GetValue<string>() ?? ""))
            .ToList())
        {
            var copy = source.DeepClone().AsObject();
            copy["id"] = $"track-{Guid.NewGuid():N}";
            copy["targetId"] = targetIdMappings[source["targetId"]?.GetValue<string>() ?? ""];
            foreach (var keyframe in (copy["keyframes"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                keyframe["id"] = $"keyframe-{Guid.NewGuid():N}";
            }
            _tracks.Add(copy);
        }
    }

    public void UpsertKeyframe(
        string fieldId,
        string targetId,
        int frame,
        JsonNode value,
        string interpolation)
    {
        var track = TrackObject(fieldId, targetId)
            ?? throw new InvalidOperationException("Animation track does not exist.");
        var keyframes = (JsonArray)track["keyframes"]!;
        var existing = keyframes.OfType<JsonObject>()
            .FirstOrDefault((keyframe) => keyframe["frame"]?.GetValue<int>() == frame);
        if (existing is null)
        {
            existing = new JsonObject
            {
                ["id"] = $"keyframe-{Guid.NewGuid():N}",
                ["frame"] = Math.Max(0, frame),
            };
            keyframes.Add(existing);
        }
        existing["value"] = value.DeepClone();
        existing["interpolation"] = interpolation;
        existing["enabled"] = true;
        var ordered = keyframes.OfType<JsonObject>()
            .OrderBy((keyframe) => keyframe["frame"]?.GetValue<int>() ?? 0)
            .ThenBy((keyframe) => keyframe["id"]?.GetValue<string>() ?? "", StringComparer.Ordinal)
            .Select((keyframe) => keyframe.DeepClone())
            .ToList();
        keyframes.Clear();
        foreach (var keyframe in ordered) keyframes.Add(keyframe);
    }

    public void RemoveKeyframe(string fieldId, string targetId, int frame)
    {
        if (frame == 0) return;
        var track = TrackObject(fieldId, targetId);
        var keyframes = track?["keyframes"] as JsonArray;
        var keyframe = keyframes?.OfType<JsonObject>()
            .FirstOrDefault((candidate) => candidate["frame"]?.GetValue<int>() == frame);
        if (keyframe is not null) keyframes!.Remove(keyframe);
    }

    public string ToJson() => _root.ToJsonString();

    private JsonObject? TrackObject(string fieldId, string targetId) =>
        _tracks.OfType<JsonObject>().FirstOrDefault((track) =>
            track["fieldId"]?.GetValue<string>() == fieldId
            && (track["targetId"]?.GetValue<string>() ?? "") == targetId);

    private static AnimationTrackView ReadTrack(JsonObject track)
    {
        var keyframes = (track["keyframes"] as JsonArray)?.OfType<JsonObject>()
            .Select((keyframe) => new AnimationKeyframeView(
                keyframe["id"]?.GetValue<string>() ?? "",
                keyframe["frame"]?.GetValue<int>() ?? 0,
                keyframe["value"]?.DeepClone(),
                keyframe["interpolation"]?.GetValue<string>() ?? "hold",
                keyframe["enabled"]?.GetValue<bool>() ?? true))
            .OrderBy((keyframe) => keyframe.Frame)
            .ToList() ?? [];
        return new AnimationTrackView(
            track["id"]?.GetValue<string>() ?? "",
            track["fieldId"]?.GetValue<string>() ?? "",
            track["targetId"]?.GetValue<string>() ?? "",
            keyframes);
    }
}

internal sealed record AnimationTrackView(
    string Id,
    string FieldId,
    string TargetId,
    IReadOnlyList<AnimationKeyframeView> Keyframes);

internal sealed record AnimationKeyframeView(
    string Id,
    int Frame,
    JsonNode? Value,
    string Interpolation,
    bool Enabled);
