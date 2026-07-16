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
    private static void NormalizeRuntimeInputContracts(SqliteConnection connection)
    {
        RemovePersistedDesignPreviewTestValues(connection);
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, component_type, design_preview_json FROM component_classes";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string Json)>();
        while (reader.Read())
        {
            var preview = ParseJsonObject(ReadString(reader, 2));
            var authoritativeInputs = ComponentInputsForComponent(reader.GetString(1));
            if (preview["inputs"] is JsonArray currentInputs
                && JsonNode.DeepEquals(currentInputs, authoritativeInputs))
            {
                continue;
            }

            preview["inputs"] = authoritativeInputs.DeepClone();
            updates.Add((reader.GetString(0), preview.ToJsonString()));
        }
        reader.Close();

        foreach (var update in updates)
        {
            Execute(
                connection,
                "UPDATE component_classes SET design_preview_json = $json WHERE id = $id",
                ("$json", update.Json),
                ("$id", update.Id));
        }

        NormalizeConversationRuntimeInputContracts(connection);
    }

    private static void NormalizeModuleInstanceRuntimePayloads(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT mi.id, mi.content_json, m.design_preview_json, m.id, m.metadata_json, mi.metadata_json FROM module_instances mi JOIN modules m ON m.id = mi.module_id";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string Json)>();
        while (reader.Read())
        {
            var original = ReadString(reader, 1);
            var content = ParseJsonObject(original);
            var contract = EffectiveModuleInstanceContract(
                reader.GetString(3), ReadString(reader, 4), ReadString(reader, 5), ReadString(reader, 2));
            foreach (var input in (contract["inputs"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                var jsonKey = input["jsonKey"]?.GetValue<string>() ?? "";
                if (string.IsNullOrWhiteSpace(jsonKey)) continue;
                if (!RuntimeInputDefinition(input))
                {
                    content.Remove(jsonKey);
                    continue;
                }
                if (content[jsonKey] is not null) continue;
                content[jsonKey] = RuntimeDefaultValue(input);
            }

            foreach (var collection in (contract["collections"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                var storageKey = RuntimeCollectionStorageKey(collection);
                if (string.IsNullOrWhiteSpace(storageKey)) continue;
                var projected = collection["storageCollectionJsonKey"] is JsonValue;
                var items = projected
                    ? NormalizeProjectedRuntimeCollection(
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
            if (next != original) updates.Add((reader.GetString(0), next));
        }
        reader.Close();

        foreach (var update in updates)
        {
            Execute(connection, "UPDATE module_instances SET content_json = $json WHERE id = $id",
                ("$json", update.Json), ("$id", update.Id));
        }
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
            "UPDATE shots SET duration_frames = MAX(1, COALESCE((SELECT SUM(mi.duration_frames) FROM module_instances mi WHERE mi.shot_id = shots.id), 0))");
    }

    private static void NormalizeAnimationJson(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT mi.id, mi.animation_json, mi.content_json, m.design_preview_json, m.id, m.metadata_json, mi.metadata_json FROM module_instances mi JOIN modules m ON m.id = mi.module_id";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string Json)>();
        while (reader.Read())
        {
            var animation = ParseJsonObject(ReadString(reader, 1));
            var contract = EffectiveModuleInstanceContract(
                reader.GetString(4), ReadString(reader, 5), ReadString(reader, 6), ReadString(reader, 3));
            var version = animation["schemaVersion"]?.GetValue<int>() ?? 1;
            if (version == 2)
            {
                if (EnsureInitialAnimationKeyframes(animation, ParseJsonObject(ReadString(reader, 2)), contract))
                    updates.Add((reader.GetString(0), animation.ToJsonString()));
                ValidateAnimationJson(animation, reader.GetString(0));
                continue;
            }
            if (version != 1) throw new InvalidOperationException($"Unsupported animation schemaVersion {version}.");
            var tracks = animation["tracks"] as JsonArray ?? new JsonArray();
            foreach (var (track, trackIndex) in tracks.OfType<JsonObject>().Select((value, index) => (value, index)))
            {
                if (track["events"] is JsonArray { Count: > 0 })
                    throw new InvalidOperationException("Animation events cannot be migrated; convert them explicitly to v2 keyframes.");
                track["id"] ??= $"track-{trackIndex + 1:D4}";
                track["fieldId"] = track["parameterId"]?.DeepClone() ?? throw new InvalidOperationException("Animation track is missing parameterId.");
                track.Remove("parameterId");
                if (track["itemId"] is JsonNode targetId) track["targetId"] = targetId.DeepClone();
                track.Remove("itemId");
                track.Remove("label");
                var keyframes = track["keyframes"] as JsonArray ?? new JsonArray();
                track["keyframes"] = keyframes;
                foreach (var (keyframe, keyframeIndex) in keyframes.OfType<JsonObject>().Select((value, index) => (value, index)))
                {
                    keyframe["id"] ??= $"{track["id"]!.GetValue<string>()}-keyframe-{keyframeIndex + 1:D4}";
                    keyframe["interpolation"] ??= "hold";
                    keyframe["enabled"] ??= true;
                }
            }
            animation["schemaVersion"] = 2;
            EnsureInitialAnimationKeyframes(animation, ParseJsonObject(ReadString(reader, 2)), contract);
            ValidateAnimationJson(animation, reader.GetString(0));
            updates.Add((reader.GetString(0), animation.ToJsonString()));
        }
        reader.Close();
        foreach (var update in updates)
            Execute(connection, "UPDATE module_instances SET animation_json = $json WHERE id = $id", ("$json", update.Json), ("$id", update.Id));
    }

    private static bool EnsureInitialAnimationKeyframes(JsonObject animation, JsonObject runtime, JsonObject contract)
    {
        var changed = false;
        foreach (var track in (animation["tracks"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var keyframes = track["keyframes"] as JsonArray ?? new JsonArray();
            track["keyframes"] = keyframes;
            var existingOrigin = keyframes.OfType<JsonObject>()
                .FirstOrDefault((keyframe) => keyframe["frame"]?.GetValue<int>() == 0);
            if (existingOrigin is not null)
            {
                if (existingOrigin["enabled"]?.GetValue<bool>() == false)
                {
                    existingOrigin["enabled"] = true;
                    changed = true;
                }
                continue;
            }
            var fieldId = track["fieldId"]?.GetValue<string>() ?? "";
            var targetId = track["targetId"]?.GetValue<string>() ?? "";
            var definition = FindAnimationFieldDefinition(contract, fieldId, targetId, runtime, out var runtimeOwner);
            if (definition is null || runtimeOwner is null) continue;
            var jsonKey = definition["jsonKey"]?.GetValue<string>() ?? "";
            var value = runtimeOwner[jsonKey]?.DeepClone() ?? RuntimeDefaultValue(definition);
            var interpolation = (definition["animationInterpolations"] as JsonArray)?.OfType<JsonValue>()
                .Select((candidate) => candidate.GetValue<string>())
                .FirstOrDefault() ?? "hold";
            keyframes.Add(new JsonObject
            {
                ["id"] = $"{track["id"]?.GetValue<string>() ?? "track"}-keyframe-0000",
                ["frame"] = 0,
                ["value"] = value,
                ["interpolation"] = interpolation,
                ["enabled"] = true,
            });
            changed = true;
        }
        return changed;
    }

    private static JsonObject? FindAnimationFieldDefinition(
        JsonObject contract,
        string fieldId,
        string targetId,
        JsonObject runtime,
        out JsonObject? runtimeOwner)
    {
        runtimeOwner = null;
        if (string.IsNullOrWhiteSpace(targetId))
        {
            var definition = (contract["inputs"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault((candidate) =>
                candidate["id"]?.GetValue<string>() == fieldId
                && candidate["animatable"]?.GetValue<bool>() == true);
            if (definition is not null) runtimeOwner = runtime;
            return definition;
        }

        foreach (var collection in (contract["collections"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var definition = (collection["fields"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault((candidate) =>
                candidate["id"]?.GetValue<string>() == fieldId
                && candidate["animatable"]?.GetValue<bool>() == true);
            var collectionKey = RuntimeCollectionStorageKey(collection);
            runtimeOwner = (runtime[collectionKey] as JsonArray)?.OfType<JsonObject>().FirstOrDefault((item) =>
                item["id"]?.GetValue<string>() == targetId);
            if (runtimeOwner is null) continue;
            if (definition is not null) return definition;
            var inputsJsonKey = collection["componentItems"]?["inputsJsonKey"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(inputsJsonKey) || runtimeOwner[inputsJsonKey] is not JsonObject componentInputs) continue;
            definition = (componentInputs["inputs"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault((candidate) =>
                candidate["id"]?.GetValue<string>() == fieldId
                && candidate["animatable"]?.GetValue<bool>() == true);
            if (definition is null) continue;
            runtimeOwner = componentInputs;
            return definition;
        }
        return null;
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

    private static JsonArray NormalizeProjectedRuntimeCollection(JsonArray? current, JsonArray? defaults)
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

    private static void RemovePersistedDesignPreviewTestValues(SqliteConnection connection)
    {
        foreach (var table in new[] { "component_classes", "modules" })
        {
            using var select = connection.CreateCommand();
            select.CommandText = $"SELECT id, design_preview_json FROM {table}";
            using var reader = select.ExecuteReader();
            var updates = new List<(string Id, string Json)>();
            while (reader.Read())
            {
                var preview = ParseJsonObject(ReadString(reader, 1));
                if (!preview.Remove("testValues"))
                {
                    continue;
                }
                updates.Add((reader.GetString(0), preview.ToJsonString()));
            }
            reader.Close();

            foreach (var update in updates)
            {
                Execute(
                    connection,
                    $"UPDATE {table} SET design_preview_json = $json WHERE id = $id",
                    ("$json", update.Json),
                    ("$id", update.Id));
            }
        }
    }

    private static void NormalizeConversationRuntimeInputContracts(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT m.id, m.design_preview_json, a.project_id FROM modules m JOIN apps a ON a.id = m.app_id WHERE m.record_class_id = 'module.core.chat'";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string Json)>();
        var defaults = DefaultConversationDesignPreviewJson();
        while (reader.Read())
        {
            var preview = ParseJsonObject(ReadString(reader, 1));
            var projectId = reader.GetString(2);
            var changed = false;
            changed |= preview.Remove("headerTitle");
            MigrateConversationPreviewMessages(preview, defaults, ref changed);
            foreach (var property in defaults.Where((property) => property.Key is not "inputs" and not "actions" and not "collections" and not "messages"))
            {
                if (preview[property.Key] is not null) continue;
                preview[property.Key] = property.Value?.DeepClone();
                changed = true;
            }

            foreach (var contractKey in new[] { "inputs", "collections", "actions" })
            {
                if (JsonNode.DeepEquals(preview[contractKey], defaults[contractKey])) continue;
                preview[contractKey] = defaults[contractKey]?.DeepClone();
                changed = true;
            }

            var buttonClassId = ScalarString(connection, "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = 'button'", ("$projectId", projectId))!;
            var beforeButtonReferences = preview.ToJsonString();
            NormalizeIconRowNodes(preview, $"{buttonClassId}::preset::{DefaultComponentPresetId}");
            changed |= !string.Equals(beforeButtonReferences, preview.ToJsonString(), System.StringComparison.Ordinal);

            if (changed)
            {
                updates.Add((reader.GetString(0), preview.ToJsonString()));
            }
        }
        reader.Close();

        foreach (var update in updates)
        {
            Execute(
                connection,
                "UPDATE modules SET design_preview_json = $json WHERE id = $id",
                ("$json", update.Json),
                ("$id", update.Id));
        }
    }

    private static void NormalizeConversationHeaderComposition(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT m.id, m.config_json, m.design_preview_json, a.project_id FROM modules m JOIN apps a ON a.id = m.app_id WHERE m.record_class_id = 'module.core.chat'";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string ConfigJson, string PreviewJson)>();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var originalConfig = ReadString(reader, 1);
            var originalPreview = ReadString(reader, 2);
            var config = ParseJsonObject(originalConfig);
            var preview = ParseJsonObject(originalPreview);
            var projectId = reader.GetString(3);
            var conversation = config["conversation"] as JsonObject ?? new JsonObject();
            config["conversation"] = conversation;
            conversation.Remove("statusBarVariant");
            conversation.Remove("navigationBarVariant");
            conversation["headerAvatarAlignment"] ??= "left";
            NormalizeConversationHeaderIconRowSlot(conversation, "headerLeftIconRowSlot", "headerLeftIconRowVariant", projectId);
            NormalizeConversationHeaderIconRowSlot(conversation, "headerRightIconRowSlot", "headerRightIconRowVariant", projectId);
            conversation["headerLeftIconRowInputs"] ??= HeaderIconRowInputs(projectId, []);
            conversation["headerRightIconRowInputs"] ??= HeaderIconRowInputs(projectId, ["media_camera"]);
            var currentAvatar = conversation["headerAvatarVariant"]?.GetValue<string>() ?? "";
            if (currentAvatar.EndsWith("::preset::default", System.StringComparison.Ordinal))
            {
                var avatarClassId = ScalarString(connection, "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = 'avatar'", ("$projectId", projectId));
                if (!string.IsNullOrWhiteSpace(avatarClassId))
                {
                    var metadata = ParseJsonObject(ScalarString(connection, "SELECT metadata_json FROM component_classes WHERE id = $id", ("$id", avatarClassId)) ?? "{}");
                    if ((metadata["presets"] as JsonArray)?.OfType<JsonObject>().Any((preset) => preset["id"]?.GetValue<string>() == "avatar_chat_header") == true)
                    {
                        conversation["headerAvatarVariant"] = $"{avatarClassId}::preset::avatar_chat_header";
                    }
                }
            }
            preview.Remove("headerLeftButtons");
            preview.Remove("headerRightButtons");
            if (preview["collections"] is JsonArray collections)
            {
                foreach (var retired in collections.OfType<JsonObject>()
                    .Where((collection) => collection["jsonKey"]?.GetValue<string>() is "headerLeftButtons" or "headerRightButtons")
                    .ToList())
                {
                    collections.Remove(retired);
                }
            }
            var configJson = config.ToJsonString();
            var previewJson = preview.ToJsonString();
            if (configJson != originalConfig || previewJson != originalPreview)
            {
                updates.Add((id, configJson, previewJson));
            }
        }
        reader.Close();
        foreach (var update in updates)
        {
            Execute(connection, "UPDATE modules SET config_json = $configJson, design_preview_json = $previewJson WHERE id = $id",
                ("$id", update.Id), ("$configJson", update.ConfigJson), ("$previewJson", update.PreviewJson));
        }
    }

    private static void NormalizeConversationHeaderIconRowSlot(JsonObject conversation, string slotKey, string retiredKey, string projectId)
    {
        var presetId = conversation[retiredKey]?.GetValue<string>();
        conversation[slotKey] ??= new JsonObject
        {
            ["presetId"] = string.IsNullOrWhiteSpace(presetId) ? SeededComponentPresetReference(projectId, "iconRow") : presetId,
            ["overrides"] = new JsonObject(),
        };
        conversation.Remove(retiredKey);
    }

    private static void MigrateConversationPreviewMessages(JsonObject preview, JsonObject defaults, ref bool changed)
    {
        if (preview["messages"] is JsonArray existingMessages)
        {
            var defaultMessages = defaults["messages"] as JsonArray ?? new JsonArray();
            for (var index = 0; index < existingMessages.Count; index++)
            {
                if (existingMessages[index] is not JsonObject existing
                    || (defaultMessages.ElementAtOrDefault(index) as JsonObject
                        ?? defaultMessages.ElementAtOrDefault(0) as JsonObject) is not JsonObject defaultsForMessage)
                {
                    continue;
                }

                foreach (var (key, value) in defaultsForMessage)
                {
                    if (existing[key] is not null)
                    {
                        continue;
                    }
                    existing[key] = value?.DeepClone();
                    changed = true;
                }
            }
            NormalizeConversationTiming(preview, defaults, existingMessages, ref changed);
            RemoveLegacyConversationPreviewKeys(preview, ref changed);
            return;
        }

        var messages = defaults["messages"]?.DeepClone() as JsonArray ?? new JsonArray();
        var first = messages.ElementAtOrDefault(0) as JsonObject;
        var second = messages.ElementAtOrDefault(1) as JsonObject;
        var third = messages.ElementAtOrDefault(2) as JsonObject;
        CopyScalar(preview, "message1Text", first, "text");
        CopyScalar(preview, "message2Text", second, "text");
        CopyScalar(preview, "message3Text", third, "text");
        CopyScalar(preview, "message2StatusState", second, "statusState");
        CopyScalar(preview, "message2StatusText", second, "statusText");
        preview["messages"] = messages;
        NormalizeConversationTiming(preview, defaults, messages, ref changed);
        RemoveLegacyConversationPreviewKeys(preview, ref changed);
        changed = true;
    }

    private static void NormalizeConversationTiming(
        JsonObject preview,
        JsonObject defaults,
        JsonArray messages,
        ref bool changed)
    {
        foreach (var (message, index) in messages.OfType<JsonObject>().Select((message, index) => (message, index)))
        {
            if (message["writeOnTiming"] is null)
            {
                var defaultMessage = (defaults["messages"] as JsonArray)?.ElementAtOrDefault(index) as JsonObject;
                message["writeOnTiming"] = defaultMessage?["writeOnTiming"]?.DeepClone()
                    ?? new JsonObject
                    {
                        ["mode"] = "fixed",
                        ["fixedFrames"] = 0,
                        ["paceToken"] = "theme.motion.naturalPace.normal",
                    };
                changed = true;
            }
            if (message["postWriteOnHoldFrames"] is null)
            {
                var defaultMessage = (defaults["messages"] as JsonArray)?.ElementAtOrDefault(index) as JsonObject;
                message["postWriteOnHoldFrames"] = defaultMessage?["postWriteOnHoldFrames"]?.DeepClone() ?? 0;
                changed = true;
            }
            PromoteMessageValue(preview, message, "bubbleRevealMode", ref changed);
            PromoteMessageValue(preview, message, "textInputVisible", ref changed);
            PromoteMessageValue(preview, message, "keyboardVisible", ref changed);
            RemoveMessageTimingKey(message, "bubbleRevealMode", ref changed);
            RemoveMessageTimingKey(message, "textInputVisible", ref changed);
            RemoveMessageTimingKey(message, "keyboardVisible", ref changed);
            if (message.Remove("textReveal"))
            {
                changed = true;
            }
        }

        foreach (var key in new[]
        {
            "bubbleRevealMode",
            "incomingRevealMode",
            "textInputVisible",
            "keyboardVisible",
            "typingIndicatorText",
            "typingIndicatorSizeToken",
            "typingIndicatorAnimation",
        })
        {
            if (preview[key] is null && defaults[key] is JsonNode defaultValue)
            {
                preview[key] = defaultValue.DeepClone();
                changed = true;
            }
        }
    }

    private static void PromoteMessageValue(JsonObject preview, JsonObject message, string key, ref bool changed)
    {
        if (preview[key] is null && message[key] is JsonNode value)
        {
            preview[key] = value.DeepClone();
            changed = true;
        }
    }

    private static void RemoveMessageTimingKey(JsonObject message, string key, ref bool changed)
    {
        if (message.Remove(key))
        {
            changed = true;
        }
    }

    private static void RemoveLegacyConversationPreviewKeys(JsonObject preview, ref bool changed)
    {
        foreach (var key in new[]
        {
            "message1Text",
            "message2Text",
            "message3Text",
            "message2StatusState",
            "message2StatusText",
            "writeOnDurationFrames",
            "postWriteOnHoldFrames",
        })
        {
            if (preview.Remove(key))
            {
                changed = true;
            }
        }
    }

    private static void CopyScalar(JsonObject source, string sourceKey, JsonObject? target, string targetKey)
    {
        if (target is not null && source[sourceKey] is JsonNode value)
        {
            target[targetKey] = value.DeepClone();
        }
    }
}
