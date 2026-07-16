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
    private static void ReconcileModuleInstanceRuntimePayload(
        SqliteConnection connection,
        string moduleInstanceId)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT mi.content_json, m.design_preview_json, m.id, m.metadata_json, mi.metadata_json FROM module_instances mi JOIN modules m ON m.id = mi.module_id WHERE mi.id = $id";
        select.Parameters.AddWithValue("$id", moduleInstanceId);
        using var reader = select.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing module instance '{moduleInstanceId}'.");
        }

        var original = ReadString(reader, 0);
        var content = ParseJsonObject(original);
        var contract = EffectiveModuleInstanceContract(
            reader.GetString(2), ReadString(reader, 3), ReadString(reader, 4), ReadString(reader, 1));
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
        reader.Close();
        if (next == original) return;
        Execute(connection, "UPDATE module_instances SET content_json = $json WHERE id = $id",
            ("$json", next), ("$id", moduleInstanceId));
    }

    private static void SynchronizeTimelineDurations(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT mi.id, mi.duration_frames, mi.content_json, mi.animation_json, m.design_preview_json,
                   COALESCE(
                     (SELECT t.tokens_json FROM shots s JOIN actors actor ON actor.id = s.owner_actor_id JOIN themes t ON t.id = actor.default_theme_id WHERE s.id = mi.shot_id),
                     (SELECT t.tokens_json FROM apps a JOIN themes t ON t.project_id = a.project_id WHERE a.id = mi.app_id ORDER BY t.name, t.id LIMIT 1),
                     '{}'), m.id, m.metadata_json, mi.metadata_json
            FROM module_instances mi
            JOIN modules m ON m.id = mi.module_id
            """;
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, int Duration)>();
        while (reader.Read())
        {
            var stored = reader.GetInt32(1);
            var contract = EffectiveModuleInstanceContract(
                reader.GetString(6), ReadString(reader, 7), ReadString(reader, 8), ReadString(reader, 4));
            if (RuntimeDurationContract.Policy(contract) == RuntimeDurationPolicy.Explicit) continue;
            var duration = RuntimeTimeline.DurationFrames(contract.ToJsonString(), ReadString(reader, 2), ReadString(reader, 3), stored, ReadString(reader, 5));
            if (duration != stored) updates.Add((reader.GetString(0), duration));
        }
        reader.Close();
        foreach (var update in updates)
        {
            Execute(connection, "UPDATE module_instances SET duration_frames = $duration WHERE id = $id",
                ("$duration", update.Duration), ("$id", update.Id));
        }

        Execute(
            connection,
            """
            UPDATE shots
            SET duration_frames = MAX(1, COALESCE(
              (SELECT SUM(mi.duration_frames) FROM module_instances mi WHERE mi.shot_id = shots.id),
              0))
            WHERE duration_frames <> MAX(1, COALESCE(
              (SELECT SUM(mi.duration_frames) FROM module_instances mi WHERE mi.shot_id = shots.id),
              0))
            """);
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
