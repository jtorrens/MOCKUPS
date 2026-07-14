using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private sealed record ConversationMessage(
        string Id,
        string Type,
        string Direction,
        string ActorId,
        string Text,
        int DelayAfterPreviousFrames,
        int WriteOnDurationFrames,
        int PostWriteOnHoldFrames,
        string StatusText,
        string DeliveryStatus,
        bool StatusVisible,
        string MediaType,
        string MediaSource,
        string ViewportSize,
        decimal MediaScale,
        string MediaOffset,
        bool IsPlaying,
        decimal CurrentTimeSeconds,
        decimal DurationSeconds,
        bool IsFullScreen,
        bool FullScreenTransition,
        string FullframeOrientation,
        int ControlsElapsedMs);

    public sealed record ModuleInstanceSlot(
        string Id,
        string Name,
        string ModuleName,
        int SortOrder,
        string TransitionType,
        int StoredDurationFrames);

    public ModuleInstanceSettings GetModuleInstanceSettings(string moduleInstanceId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT shot_id, app_id, module_id, name, notes, sort_order, duration_frames,
                   transition_json, content_json, behavior_json, animation_json, metadata_json
            FROM module_instances
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", moduleInstanceId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException($"Missing module instance '{moduleInstanceId}'.");
        return new ModuleInstanceSettings(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            ReadString(reader, 4), reader.GetInt32(5), reader.GetInt32(6), ReadString(reader, 7),
            ReadString(reader, 8), ReadString(reader, 9), ReadString(reader, 10), ReadString(reader, 11));
    }

    public string GetModuleInstanceModuleName(string moduleInstanceId)
    {
        using var connection = OpenConnection();
        return ScalarString(connection, "SELECT m.name FROM module_instances mi JOIN modules m ON m.id = mi.module_id WHERE mi.id = $id", ("$id", moduleInstanceId))
            ?? throw new InvalidOperationException($"Missing module instance '{moduleInstanceId}'.");
    }

    public string GetModuleInstanceTransitionType(string moduleInstanceId)
    {
        var transition = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).TransitionJson);
        return transition["type"]?.GetValue<string>() ?? "cut";
    }

    public string GetModuleInstanceRuntimePreviewJson(string moduleInstanceId)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        var module = GetModuleSettings(instance.ModuleId);
        var preview = RuntimeInputForwardingContract.EffectivePreview(
            ParseJsonObject(module.DesignPreviewJson),
            ParseJsonObject(module.ConfigJson));
        var runtime = ParseJsonObject(instance.ContentJson);
        foreach (var (key, value) in runtime)
        {
            if (key == "schemaVersion") continue;
            preview[key] = value?.DeepClone();
        }
        preview.Remove("testValues");
        return preview.ToJsonString();
    }

    public void UpdateModuleInstanceRuntimeValue(string moduleInstanceId, string jsonKey, JsonNode? value)
    {
        if (string.IsNullOrWhiteSpace(jsonKey)) throw new InvalidOperationException("Runtime input key cannot be empty.");
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        content[jsonKey] = value?.DeepClone();
        SaveModuleInstanceRuntimeContent(moduleInstanceId, content);
    }

    public void UpdateModuleInstanceAnimationJson(string moduleInstanceId, string animationJson)
    {
        var animation = ParseJsonObject(animationJson);
        ValidateAnimationJson(animation, moduleInstanceId);
        using var connection = OpenConnection();
        Execute(
            connection,
            "UPDATE module_instances SET animation_json = $animationJson WHERE id = $id",
            ("$animationJson", animation.ToJsonString()),
            ("$id", moduleInstanceId));
        SynchronizeTimelineDurations(connection);
    }

    public void UpdateModuleInstanceRuntimeCollectionValue(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        string fieldJsonKey,
        JsonNode? value)
    {
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        var item = (content[collectionJsonKey] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault((candidate) => candidate["id"]?.GetValue<string>() == itemId)
            ?? throw new InvalidOperationException($"Missing runtime collection item '{itemId}'.");
        item[fieldJsonKey] = value?.DeepClone();
        SaveModuleInstanceRuntimeContent(moduleInstanceId, content);
    }

    public void AddModuleInstanceRuntimeCollectionItem(string moduleInstanceId, string collectionJsonKey, JsonObject item)
    {
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        var items = content[collectionJsonKey] as JsonArray ?? new JsonArray();
        content[collectionJsonKey] = items;
        items.Add(item.DeepClone());
        SaveModuleInstanceRuntimeContent(moduleInstanceId, content);
    }

    public void InsertModuleInstanceRuntimeCollectionItemAfter(
        string moduleInstanceId,
        string collectionJsonKey,
        string afterItemId,
        JsonObject item)
    {
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        var items = content[collectionJsonKey] as JsonArray ?? new JsonArray();
        content[collectionJsonKey] = items;
        var currentIndex = -1;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index]?["id"]?.GetValue<string>() != afterItemId) continue;
            currentIndex = index;
            break;
        }
        items.Insert(currentIndex < 0 ? items.Count : currentIndex + 1, item.DeepClone());
        SaveModuleInstanceRuntimeContent(moduleInstanceId, content);
    }

    public void DuplicateModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        string duplicateItemId)
    {
        var settings = GetModuleInstanceSettings(moduleInstanceId);
        var content = ParseJsonObject(settings.ContentJson);
        var items = content[collectionJsonKey] as JsonArray
            ?? throw new InvalidOperationException($"Missing runtime collection '{collectionJsonKey}'.");
        var currentIndex = -1;
        JsonObject? source = null;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is not JsonObject candidate
                || candidate["id"]?.GetValue<string>() != itemId) continue;
            currentIndex = index;
            source = candidate;
            break;
        }
        if (source is null) throw new InvalidOperationException($"Missing runtime collection item '{itemId}'.");
        var duplicate = source.DeepClone().AsObject();
        duplicate["id"] = duplicateItemId;
        items.Insert(currentIndex + 1, duplicate);

        var animation = ParseJsonObject(settings.AnimationJson);
        if (animation["tracks"] is JsonArray tracks)
        {
            foreach (var sourceTrack in tracks.OfType<JsonObject>()
                .Where((track) => track["targetId"]?.GetValue<string>() == itemId)
                .ToList())
            {
                var duplicateTrack = sourceTrack.DeepClone().AsObject();
                duplicateTrack["id"] = $"track_{Guid.NewGuid():N}";
                duplicateTrack["targetId"] = duplicateItemId;
                foreach (var keyframe in (duplicateTrack["keyframes"] as JsonArray)?.OfType<JsonObject>() ?? [])
                {
                    keyframe["id"] = $"keyframe_{Guid.NewGuid():N}";
                }
                tracks.Add(duplicateTrack);
            }
        }

        using var connection = OpenConnection();
        Execute(
            connection,
            "UPDATE module_instances SET content_json = $contentJson, animation_json = $animationJson WHERE id = $id",
            ("$contentJson", content.ToJsonString()),
            ("$animationJson", animation.ToJsonString()),
            ("$id", moduleInstanceId));
        SynchronizeTimelineDurations(connection);
    }

    public void DeleteModuleInstanceRuntimeCollectionItem(string moduleInstanceId, string collectionJsonKey, string itemId)
    {
        var settings = GetModuleInstanceSettings(moduleInstanceId);
        var content = ParseJsonObject(settings.ContentJson);
        var items = content[collectionJsonKey] as JsonArray
            ?? throw new InvalidOperationException($"Missing runtime collection '{collectionJsonKey}'.");
        var item = items.OfType<JsonObject>().FirstOrDefault((candidate) => candidate["id"]?.GetValue<string>() == itemId)
            ?? throw new InvalidOperationException($"Missing runtime collection item '{itemId}'.");
        items.Remove(item);
        var animation = ParseJsonObject(settings.AnimationJson);
        if (animation["tracks"] is JsonArray tracks)
        {
            foreach (var track in tracks.OfType<JsonObject>()
                .Where((candidate) => candidate["targetId"]?.GetValue<string>() == itemId)
                .ToList())
            {
                tracks.Remove(track);
            }
        }
        using var connection = OpenConnection();
        Execute(
            connection,
            "UPDATE module_instances SET content_json = $contentJson, animation_json = $animationJson WHERE id = $id",
            ("$contentJson", content.ToJsonString()),
            ("$animationJson", animation.ToJsonString()),
            ("$id", moduleInstanceId));
        SynchronizeTimelineDurations(connection);
    }

    public void MoveModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        int offset)
    {
        if (offset == 0) return;
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        var items = content[collectionJsonKey] as JsonArray
            ?? throw new InvalidOperationException($"Missing runtime collection '{collectionJsonKey}'.");
        var currentIndex = -1;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index]?["id"]?.GetValue<string>() != itemId) continue;
            currentIndex = index;
            break;
        }
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= items.Count) return;
        var item = items[currentIndex];
        items.RemoveAt(currentIndex);
        items.Insert(targetIndex, item);
        SaveModuleInstanceRuntimeContent(moduleInstanceId, content);
    }

    private void SaveModuleInstanceRuntimeContent(string moduleInstanceId, JsonObject content)
    {
        using var connection = OpenConnection();
        Execute(connection, "UPDATE module_instances SET content_json = $contentJson WHERE id = $id",
            ("$contentJson", content.ToJsonString()), ("$id", moduleInstanceId));
        SynchronizeTimelineDurations(connection);
    }

    public IReadOnlyList<ModuleInstanceSlot> GetShotModuleInstanceSlots(string shotId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT mi.id, mi.name, m.name, mi.sort_order, mi.transition_json, mi.duration_frames
            FROM module_instances mi
            JOIN modules m ON m.id = mi.module_id
            WHERE mi.shot_id = $shotId
            ORDER BY mi.sort_order, mi.name, mi.id
            """;
        command.Parameters.AddWithValue("$shotId", shotId);
        using var reader = command.ExecuteReader();
        var slots = new List<ModuleInstanceSlot>();
        while (reader.Read())
        {
            var transition = ParseJsonObject(ReadString(reader, 4));
            slots.Add(new ModuleInstanceSlot(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                transition["type"]?.GetValue<string>() ?? "cut",
                reader.GetInt32(5)));
        }

        return slots;
    }

    public IReadOnlyList<ShotModuleChoice> GetAvailableShotModules(string shotId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.id, m.name, a.name, a.id, m.record_class_id
            FROM modules m
            JOIN apps a ON a.id = m.app_id
            WHERE a.project_id = (
              SELECT e.project_id
              FROM shots s
              JOIN episodes e ON e.id = s.episode_id
              WHERE s.id = $shotId
            )
            ORDER BY a.sort_order, a.name, m.sort_order, m.name
            """;
        command.Parameters.AddWithValue("$shotId", shotId);
        using var reader = command.ExecuteReader();
        var choices = new List<ShotModuleChoice>();
        while (reader.Read())
        {
            choices.Add(new ShotModuleChoice(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4)));
        }
        return choices;
    }

    public ProjectTreeNode AddModuleInstance(ProjectTreeNode shot, ShotModuleChoice module)
    {
        if (shot.Kind != ProjectTreeNodeKind.Shot)
            throw new InvalidOperationException("A module instance can only be added to a Shot.");
        using var connection = OpenConnection();
        var index = NextSortOrder(connection, "module_instances", "shot_id", shot.Id);
        var id = $"module_instance_{Guid.NewGuid():N}";
        var name = $"{module.Name} {index + 1}";
        Execute(
            connection,
            """
            INSERT INTO module_instances (
              id, shot_id, app_id, module_id, name, notes, sort_order, duration_frames,
              transition_json, content_json, behavior_json, animation_json)
            VALUES (
              $id, $shotId, $appId, $moduleId, $name, $notes, $sortOrder, 1,
              '{"type":"cut"}', $contentJson, $behaviorJson, $animationJson)
            """,
            ("$id", id),
            ("$shotId", shot.Id),
            ("$appId", module.AppId),
            ("$moduleId", module.Id),
            ("$name", name),
            ("$notes", $"{module.Name} module instance."),
            ("$sortOrder", index),
            ("$contentJson", "{}"),
            ("$behaviorJson", "{}"),
            ("$animationJson", DefaultModuleAnimationJson()));
        NormalizeModuleInstanceRuntimePayloads(connection);
        SynchronizeTimelineDurations(connection);
        var duration = ScalarLong(connection, "SELECT duration_frames FROM module_instances WHERE id = $id", ("$id", id));
        return new ProjectTreeNode(
            ProjectTreeNodeKind.ModuleInstance,
            id,
            name,
            $"{module.Name} · {duration} frames · Cut",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance),
            shot);
    }

    public void MoveModuleInstance(string moduleInstanceId, int offset)
    {
        if (offset == 0) return;

        var current = GetModuleInstanceSettings(moduleInstanceId);
        var slots = GetShotModuleInstanceSlots(current.ShotId).ToList();
        var currentIndex = slots.FindIndex((slot) => slot.Id == moduleInstanceId);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= slots.Count) return;

        var currentSlot = slots[currentIndex];
        var targetSlot = slots[targetIndex];
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        Execute(connection,
            transaction,
            "UPDATE module_instances SET sort_order = $sortOrder WHERE id = $id",
            ("$sortOrder", targetSlot.SortOrder),
            ("$id", currentSlot.Id));
        Execute(connection,
            transaction,
            "UPDATE module_instances SET sort_order = $sortOrder WHERE id = $id",
            ("$sortOrder", currentSlot.SortOrder),
            ("$id", targetSlot.Id));
        transaction.Commit();
    }

    public void UpdateModuleInstanceField(string moduleInstanceId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "moduleInstance.durationFrames":
                Execute(
                    connection,
                    "UPDATE module_instances SET duration_frames = $value WHERE id = $id",
                    ("$value", Math.Max(1, NumericText.Int32(value, 1))),
                    ("$id", moduleInstanceId));
                return;
            default:
                throw new InvalidOperationException($"Unknown module instance field '{fieldId}'.");
        }
    }

    private void UpdateConversationBehaviorField(
        SqliteConnection connection,
        string moduleInstanceId,
        string fieldId,
        string value)
    {
        var settings = GetModuleInstanceSettings(moduleInstanceId);
        if (GetModuleSettings(settings.ModuleId).RecordClassId != "module.core.chat")
        {
            throw new InvalidOperationException("Conversation timing fields are only supported by Conversation module instances.");
        }

        var behavior = ParseJsonObject(settings.ContentJson);
        switch (fieldId)
        {
            case "moduleInstance.conversation.bubbleRevealMode":
                behavior["bubbleRevealMode"] = value is "afterWriteOn" ? "afterWriteOn" : "duringWriteOn";
                break;
            case "moduleInstance.conversation.incomingRevealMode":
                behavior["incomingRevealMode"] = value is "writeOn" or "typingIndicator" ? value : "instant";
                break;
            case "moduleInstance.conversation.textInputVisible":
                behavior["textInputVisible"] = BooleanText.Parse(value);
                break;
            case "moduleInstance.conversation.keyboardVisible":
                behavior["keyboardVisible"] = BooleanText.Parse(value);
                break;
            case "moduleInstance.conversation.typingIndicatorText":
                behavior["typingIndicatorText"] = string.IsNullOrEmpty(value) ? "•••" : value;
                break;
            case "moduleInstance.conversation.typingIndicatorSizeToken":
                behavior["typingIndicatorSizeToken"] = string.IsNullOrWhiteSpace(value) ? "theme.typography.sizes.m" : value;
                break;
            case "moduleInstance.conversation.typingIndicatorAnimation":
                behavior["typingIndicatorAnimation"] = value is "none" or "wave" ? value : "pulsating";
                break;
        }

        Execute(
            connection,
            "UPDATE module_instances SET content_json = $behaviorJson WHERE id = $id",
            ("$behaviorJson", behavior.ToJsonString()),
            ("$id", moduleInstanceId));
    }

    private IReadOnlyList<ConversationMessage> GetConversationMessages(string moduleInstanceId)
    {
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        var messages = content["messages"] as JsonArray ?? [];
        return messages.OfType<JsonObject>().Select((message, index) => new ConversationMessage(
            message["id"]?.GetValue<string>() ?? $"message_{index + 1:000}",
            message["type"]?.GetValue<string>() ?? "text",
            message["direction"]?.GetValue<string>() ?? "incoming",
            message["actorId"]?.GetValue<string>() ?? "",
            message["text"]?.GetValue<string>() ?? "",
            message["delayAfterPreviousFrames"]?.GetValue<int>() ?? 0,
            message["writeOnDurationFrames"]?.GetValue<int>() ?? (message["textReveal"] as JsonObject)?["durationFrames"]?.GetValue<int>() ?? 0,
            message["postWriteOnHoldFrames"]?.GetValue<int>() ?? 0,
            message["statusText"]?.GetValue<string>() ?? "",
            message["statusState"]?.GetValue<string>() ?? "none",
            message["statusVisible"]?.GetValue<bool>()
                ?? DeliveryStatusVisible(message),
            message["mediaType"]?.GetValue<string>() ?? "none",
            message["mediaSource"]?.GetValue<string>() ?? "",
            message["viewportSize"]?.GetValue<string>() ?? "240|160",
            message["mediaScale"]?.GetValue<decimal>() ?? 1,
            message["mediaOffset"]?.GetValue<string>() ?? "0|0",
            message["isPlaying"]?.GetValue<bool>() ?? false,
            message["currentTimeSeconds"]?.GetValue<decimal>() ?? 0,
            message["durationSeconds"]?.GetValue<decimal>() ?? 12,
            message["isFullScreen"]?.GetValue<bool>() ?? false,
            message["fullScreenTransition"]?.GetValue<bool>() ?? false,
            message["fullframeOrientation"]?.GetValue<string>() ?? "portrait",
            message["controlsElapsedMs"]?.GetValue<int>() ?? 0)).ToList();
    }

    private void AddConversationMessage(string moduleInstanceId)
    {
        UpdateConversationContent(moduleInstanceId, (content) =>
        {
            var messages = content["messages"] as JsonArray ?? new JsonArray();
            content["messages"] = messages;
            messages.Add(new JsonObject
            {
                ["id"] = $"message_{Guid.NewGuid():N}",
                ["type"] = "text",
                ["direction"] = "incoming",
                ["actorId"] = "",
                ["text"] = "",
                ["delayAfterPreviousFrames"] = 0,
                ["writeOnDurationFrames"] = 0,
                ["postWriteOnHoldFrames"] = 0,
                ["statusVisible"] = false,
                ["statusText"] = "",
                ["statusState"] = "none",
                ["mediaType"] = "none",
                ["mediaSource"] = "",
                ["viewportSize"] = "240|160",
                ["mediaScale"] = 1,
                ["mediaOffset"] = "0|0",
                ["isPlaying"] = false,
                ["currentTimeSeconds"] = 0,
                ["durationSeconds"] = 12,
                ["isFullScreen"] = false,
                ["fullScreenTransition"] = false,
                ["fullframeOrientation"] = "portrait",
                ["controlsElapsedMs"] = 0,
            });
        });
    }

    private static bool DeliveryStatusVisible(JsonObject message)
    {
        return message["statusState"]?.GetValue<string>() is string status
            && status != "none";
    }

    private void UpdateConversationMessage(string moduleInstanceId, string messageId, ConversationMessage next)
    {
        UpdateConversationContent(moduleInstanceId, (content) =>
        {
            var message = (content["messages"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault((candidate) => candidate["id"]?.GetValue<string>() == messageId)
                ?? throw new InvalidOperationException($"Missing Conversation message '{messageId}'.");
            message["type"] = next.Type;
            message["direction"] = next.Direction;
            message["actorId"] = next.ActorId;
            message["text"] = next.Text;
            message["delayAfterPreviousFrames"] = Math.Max(0, next.DelayAfterPreviousFrames);
            message["writeOnDurationFrames"] = Math.Max(0, next.WriteOnDurationFrames);
            message["postWriteOnHoldFrames"] = Math.Max(0, next.PostWriteOnHoldFrames);
            message["statusVisible"] = next.StatusVisible;
            message["mediaType"] = next.MediaType is "image" or "video" or "audio" ? next.MediaType : "none";
            message["mediaSource"] = next.MediaSource;
            message["viewportSize"] = next.ViewportSize;
            message["mediaScale"] = Math.Max(0.01m, next.MediaScale);
            message["mediaOffset"] = next.MediaOffset;
            message["isPlaying"] = next.IsPlaying;
            message["currentTimeSeconds"] = Math.Max(0, next.CurrentTimeSeconds);
            message["durationSeconds"] = Math.Max(1, next.DurationSeconds);
            message["isFullScreen"] = next.IsFullScreen;
            message["fullScreenTransition"] = next.FullScreenTransition;
            message["fullframeOrientation"] = next.FullframeOrientation is "landscape" ? "landscape" : "portrait";
            message["controlsElapsedMs"] = Math.Max(0, next.ControlsElapsedMs);
            message["statusText"] = next.StatusText;
            message["statusState"] = next.DeliveryStatus;
        });
    }

    private void DeleteConversationMessage(string moduleInstanceId, string messageId)
    {
        UpdateConversationContent(moduleInstanceId, (content) =>
        {
            var messages = content["messages"] as JsonArray
                ?? throw new InvalidOperationException("Conversation content has no messages collection.");
            var message = messages.OfType<JsonObject>().FirstOrDefault((candidate) => candidate["id"]?.GetValue<string>() == messageId)
                ?? throw new InvalidOperationException($"Missing Conversation message '{messageId}'.");
            messages.Remove(message);
        });
    }

    private void UpdateConversationContent(string moduleInstanceId, Action<JsonObject> update)
    {
        var settings = GetModuleInstanceSettings(moduleInstanceId);
        if (GetModuleSettings(settings.ModuleId).RecordClassId != "module.core.chat")
        {
            throw new InvalidOperationException("Conversation messages are only supported by Conversation module instances.");
        }
        var content = ParseJsonObject(settings.ContentJson);
        update(content);
        using var connection = OpenConnection();
        Execute(connection, "UPDATE module_instances SET content_json = $contentJson WHERE id = $id", ("$contentJson", content.ToJsonString()), ("$id", moduleInstanceId));
    }

    private static void SeedModuleInstancesIfEmpty(SqliteConnection connection)
    {
        if (ScalarLong(connection, "SELECT COUNT(*) FROM module_instances") > 0)
        {
            return;
        }

        Execute(
            connection,
            """
            INSERT INTO module_instances (
              id, shot_id, app_id, module_id, name, notes,
              sort_order, duration_frames, transition_json,
              content_json, behavior_json, animation_json)
            VALUES (
              $id, $shotId, $appId, $moduleId, $name, $notes,
              0, $durationFrames, '{"type":"cut"}',
              $contentJson, $behaviorJson, $animationJson)
            """,
            ("$id", "module_instance_conversation_001"),
            ("$shotId", "shot_001"),
            ("$appId", "app_core_chat"),
            ("$moduleId", "module_core_chat"),
            ("$name", "Conversation"),
            ("$notes", "First concrete Conversation module instance."),
            ("$durationFrames", 240),
            ("$contentJson", DefaultConversationModuleContentJson()),
            ("$behaviorJson", DefaultConversationModuleBehaviorJson()),
            ("$animationJson", DefaultModuleAnimationJson()));
    }

    private static string DefaultConversationModuleContentJson()
    {
        return new JsonObject
        {
            ["schemaVersion"] = 2,
            ["actorId"] = "",
            ["headerSubtitle"] = "online",
            ["bubbleRevealMode"] = "afterWriteOn",
            ["incomingRevealMode"] = "typingIndicator",
            ["textInputVisible"] = true,
            ["keyboardVisible"] = true,
            ["typingIndicatorText"] = "•••",
            ["typingIndicatorSizeToken"] = "theme.typography.sizes.m",
            ["typingIndicatorAnimation"] = "pulsating",
            ["messages"] = new JsonArray(),
        }.ToJsonString();
    }

    private static string DefaultConversationModuleBehaviorJson()
    {
        return new JsonObject
        {
            ["headFrames"] = 0,
            ["tailFrames"] = 12,
        }.ToJsonString();
    }

    private static void NormalizeConversationModuleInstanceBehavior(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT mi.id, mi.content_json, mi.behavior_json
            FROM module_instances mi
            JOIN modules m ON m.id = mi.module_id
            WHERE m.record_class_id = 'module.core.chat'
            """;
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string ContentJson, string BehaviorJson)>();
        while (reader.Read())
        {
            var originalContent = ReadString(reader, 1);
            var originalBehavior = ReadString(reader, 2);
            var content = ParseJsonObject(originalContent);
            var behavior = ParseJsonObject(originalBehavior);
            if (content["header"] is JsonObject header)
            {
                content["actorId"] = header["actorId"]?.DeepClone() ?? "";
                content["headerSubtitle"] = header["subtitle"]?.DeepClone() ?? "online";
                content.Remove("header");
            }
            foreach (var (key, defaultValue) in ConversationRuntimeDefaults())
            {
                content[key] = content[key]?.DeepClone() ?? behavior[key]?.DeepClone() ?? defaultValue.DeepClone();
            }
            foreach (var message in (content["messages"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (message["status"] is not JsonObject status) continue;
                message["statusText"] = status["text"]?.DeepClone() ?? "";
                message["statusState"] = status["deliveryStatus"]?.DeepClone() ?? "none";
                message.Remove("status");
            }
            var timingBehavior = new JsonObject
            {
                ["headFrames"] = behavior["headFrames"]?.DeepClone() ?? 0,
                ["tailFrames"] = behavior["tailFrames"]?.DeepClone() ?? 12,
            };
            var nextContent = content.ToJsonString();
            var nextBehavior = timingBehavior.ToJsonString();
            if (nextContent != originalContent || nextBehavior != originalBehavior)
            {
                updates.Add((reader.GetString(0), nextContent, nextBehavior));
            }
        }
        reader.Close();

        foreach (var update in updates)
        {
            Execute(
                connection,
                "UPDATE module_instances SET content_json = $contentJson, behavior_json = $behaviorJson WHERE id = $id",
                ("$contentJson", update.ContentJson),
                ("$behaviorJson", update.BehaviorJson),
                ("$id", update.Id));
        }
    }

    private static IReadOnlyDictionary<string, JsonNode> ConversationRuntimeDefaults() =>
        new Dictionary<string, JsonNode>(StringComparer.Ordinal)
        {
            ["actorId"] = JsonValue.Create("")!,
            ["headerSubtitle"] = JsonValue.Create("online")!,
            ["bubbleRevealMode"] = JsonValue.Create("afterWriteOn")!,
            ["incomingRevealMode"] = JsonValue.Create("typingIndicator")!,
            ["textInputVisible"] = JsonValue.Create(true)!,
            ["keyboardVisible"] = JsonValue.Create(true)!,
            ["typingIndicatorText"] = JsonValue.Create("•••")!,
            ["typingIndicatorSizeToken"] = JsonValue.Create("theme.typography.sizes.m")!,
            ["typingIndicatorAnimation"] = JsonValue.Create("pulsating")!,
        };

    private static void EnsureConversationBehaviorValue(
        JsonObject behavior,
        string key,
        string value,
        ref bool changed)
    {
        if (behavior[key] is not null) return;
        behavior[key] = value;
        changed = true;
    }

    private static void EnsureConversationBehaviorValue(
        JsonObject behavior,
        string key,
        bool value,
        ref bool changed)
    {
        if (behavior[key] is not null) return;
        behavior[key] = value;
        changed = true;
    }

    private static string DefaultModuleAnimationJson()
    {
        return new JsonObject
        {
            ["schemaVersion"] = 2,
            ["tracks"] = new JsonArray(),
        }.ToJsonString();
    }

    private static List<ModuleInstanceRow> QueryModuleInstanceRows(SqliteConnection connection)
    {
        var rows = new List<ModuleInstanceRow>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT mi.id, mi.shot_id, mi.app_id, mi.module_id, mi.name, mi.notes,
                   mi.sort_order, mi.duration_frames, mi.transition_json, m.name
            FROM module_instances mi
            JOIN modules m ON m.id = mi.module_id
            ORDER BY mi.shot_id, mi.sort_order, mi.name
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ModuleInstanceRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                ReadString(reader, 5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                ReadString(reader, 8),
                reader.GetString(9)));
        }

        return rows;
    }
}
