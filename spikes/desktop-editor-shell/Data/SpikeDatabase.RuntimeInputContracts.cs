using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private void ReconcileModuleInstanceRuntimePayload(
        SqliteConnection connection,
        string moduleInstanceId)
    {
        var instance = _moduleInstanceRepository.Get(connection, moduleInstanceId);
        var module = _appModuleRepository.GetModule(connection, instance.ModuleId);
        var original = instance.ContentJson;
        var content = ParseJsonObject(original);
        var contract = EffectiveModuleInstanceContract(
            module.Id,
            module.MetadataJson,
            instance.MetadataJson,
            module.DesignPreviewJson);
        foreach (var input in (contract["inputs"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var jsonKey = input["jsonKey"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(jsonKey)) continue;
            if (!RuntimeInputDefinition(input))
            {
                content.Remove(jsonKey);
                continue;
            }
            if (content[jsonKey] is null) content[jsonKey] = RuntimeDefaultValue(input);
        }

        foreach (var collection in (contract["collections"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var storageKey = RuntimeCollectionStorageKey(collection);
            if (string.IsNullOrWhiteSpace(storageKey)) continue;
            var projected = collection["storageCollectionJsonKey"] is JsonValue;
            var items = projected
                ? ReconcileProjectedRuntimeCollection(
                    content[storageKey] as JsonArray,
                    contract[collection["jsonKey"]?.GetValue<string>() ?? ""] as JsonArray)
                : content[storageKey] as JsonArray ?? new JsonArray();
            content[storageKey] = items;
            foreach (var (item, index) in items.OfType<JsonObject>().Select((item, index) => (item, index)))
            {
                item["id"] ??= $"{storageKey}_{index + 1:000}";
                foreach (var field in (collection["fields"] as JsonArray)?.OfType<JsonObject>() ?? [])
                {
                    if (!RuntimeInputDefinition(field)) continue;
                    var jsonKey = field["jsonKey"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrWhiteSpace(jsonKey) || item[jsonKey] is not null) continue;
                    item[jsonKey] = RuntimeDefaultValue(field);
                }
            }
        }

        var next = content.ToJsonString();
        if (next == original) return;
        _moduleInstanceRepository.UpdateContent(connection, moduleInstanceId, next);
    }

    private void SynchronizeTimelineDurations(SqliteConnection connection, string? shotId = null)
    {
        var instances = shotId is null
            ? _moduleInstanceRepository.QueryAll(connection)
            : _moduleInstanceRepository.QueryByShot(connection, shotId);
        var modules = _appModuleRepository.QueryModules(connection)
            .ToDictionary((module) => module.Id, StringComparer.Ordinal);
        var updates = new List<(string Id, int Duration)>();
        foreach (var instance in instances)
        {
            if (!modules.TryGetValue(instance.ModuleId, out var module))
            {
                throw new InvalidOperationException($"Missing module '{instance.ModuleId}'.");
            }
            var contract = EffectiveModuleInstanceContract(
                module.Id,
                module.MetadataJson,
                instance.MetadataJson,
                module.DesignPreviewJson);
            if (RuntimeDurationContract.Policy(contract) == RuntimeDurationPolicy.Explicit) continue;
            var duration = RuntimeTimeline.DurationFrames(
                contract.ToJsonString(),
                instance.ContentJson,
                instance.AnimationJson,
                instance.DurationFrames,
                _moduleInstanceThemeContextService.GetTokensJson(connection, instance.Id));
            if (duration != instance.DurationFrames) updates.Add((instance.Id, duration));
        }
        foreach (var update in updates)
        {
            _moduleInstanceRepository.UpdateDuration(connection, update.Id, update.Duration);
        }

        var durationByShot = _moduleInstanceRepository.QueryAll(connection)
            .GroupBy((instance) => instance.ShotId, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => Math.Max(1, group.Sum((instance) => instance.DurationFrames)),
                StringComparer.Ordinal);
        foreach (var shot in QueryShotRows(connection))
        {
            var duration = durationByShot.GetValueOrDefault(shot.Id, 1);
            if (duration == shot.DurationFrames) continue;
            Execute(
                connection,
                "UPDATE shots SET duration_frames = $duration WHERE id = $id",
                ("$duration", duration),
                ("$id", shot.Id));
        }
    }

    private static void ValidateAnimationJson(JsonObject animation, string instanceId)
    {
        if (animation["schemaVersion"]?.GetValue<int>() != 2 || animation["tracks"] is not JsonArray tracks)
            throw new InvalidOperationException($"Module instance '{instanceId}' has invalid animation_json v2.");
        if (animation["retime"] is JsonObject retime)
        {
            ValidatePositiveFrameCount(retime["targetDurationFrames"], instanceId);
            if (retime["targets"] is JsonObject retimeTargets)
            {
                foreach (var target in retimeTargets)
                {
                    if (string.IsNullOrWhiteSpace(target.Key) || target.Value is not JsonObject targetRetime)
                        throw new InvalidOperationException($"Module instance '{instanceId}' has an invalid animation retime target.");
                    ValidatePositiveFrameCount(targetRetime["targetDurationFrames"], instanceId);
                }
            }
            else if (retime["targets"] is not null)
                throw new InvalidOperationException($"Module instance '{instanceId}' has invalid animation retime targets.");
        }
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var trackIds = new HashSet<string>(StringComparer.Ordinal);
        var keyframeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var track in tracks.OfType<JsonObject>())
        {
            var id = track["id"]?.GetValue<string>() ?? "";
            var fieldId = track["fieldId"]?.GetValue<string>() ?? "";
            var targetId = track["targetId"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(id) || !trackIds.Add(id) || string.IsNullOrWhiteSpace(fieldId) || track["keyframes"] is not JsonArray keyframes)
                throw new InvalidOperationException($"Module instance '{instanceId}' has an invalid animation track.");
            if (!targets.Add($"{fieldId}\u001f{targetId}")) throw new InvalidOperationException($"Module instance '{instanceId}' has duplicate animation targets.");
            var frames = new HashSet<int>();
            foreach (var keyframe in keyframes.OfType<JsonObject>())
            {
                var frame = keyframe["frame"]?.GetValue<int>() ?? -1;
                var keyframeId = keyframe["id"]?.GetValue<string>() ?? "";
                if (frame < 0 || string.IsNullOrWhiteSpace(keyframeId) || !keyframeIds.Add(keyframeId) || keyframe["value"] is null || !frames.Add(frame))
                    throw new InvalidOperationException($"Module instance '{instanceId}' has an invalid animation keyframe.");
            }
            var origin = keyframes.OfType<JsonObject>()
                .FirstOrDefault((keyframe) => keyframe["frame"]?.GetValue<int>() == 0);
            if (origin is null || origin["enabled"]?.GetValue<bool>() == false)
                throw new InvalidOperationException($"Module instance '{instanceId}' must keep an enabled origin keyframe at frame 0.");
        }
    }

    private static void ValidatePositiveFrameCount(JsonNode? node, string instanceId)
    {
        if (node is null) return;
        if (node is not JsonValue value || !value.TryGetValue<int>(out var frames) || frames <= 0)
            throw new InvalidOperationException($"Module instance '{instanceId}' has an invalid target duration.");
    }

    private static bool RuntimeInputDefinition(JsonObject definition)
    {
        var source = definition["source"]?.GetValue<string>() ?? "runtime";
        return source == "runtime";
    }

    private static string RuntimeCollectionStorageKey(JsonObject collection) =>
        collection["storageCollectionJsonKey"]?.GetValue<string>()
        ?? collection["sourceCollectionJsonKey"]?.GetValue<string>()
        ?? collection["jsonKey"]?.GetValue<string>()
        ?? "";

    private static JsonArray ReconcileProjectedRuntimeCollection(JsonArray? current, JsonArray? defaults)
    {
        if (defaults is null)
            throw new InvalidOperationException("Projected runtime collection has no contract defaults.");
        var currentById = (current ?? [])
            .OfType<JsonObject>()
            .Where((item) => item["id"]?.GetValue<string>() is { Length: > 0 })
            .ToDictionary((item) => item["id"]!.GetValue<string>(), StringComparer.Ordinal);
        var result = new JsonArray();
        foreach (var defaultItem in defaults.OfType<JsonObject>())
        {
            var next = defaultItem.DeepClone() as JsonObject ?? new JsonObject();
            var id = next["id"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Projected runtime collection item has no stable id.");
            if (currentById.TryGetValue(id, out var currentItem))
            {
                foreach (var key in next.Select((entry) => entry.Key).ToList())
                {
                    if (currentItem[key] is { } value) next[key] = value.DeepClone();
                }
            }
            result.Add(next);
        }
        return result;
    }

    private static JsonNode RuntimeDefaultValue(JsonObject definition)
    {
        var value = definition["defaultValue"]?.GetValue<string>() ?? "";
        return (definition["kind"]?.GetValue<string>() ?? "text") switch
        {
            "boolean" => JsonValue.Create(bool.TryParse(value, out var boolean) && boolean)!,
            "number" when decimal.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                => JsonValue.Create(number)!,
            "iconList" => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value) ?? new JsonArray(),
            "behaviorTiming" => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "{}" : value) ?? new JsonObject(),
            _ => JsonValue.Create(value)!,
        };
    }

}
