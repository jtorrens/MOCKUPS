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
    public sealed record ConversationMessage(
        string Id,
        string Type,
        string Direction,
        string ActorId,
        string Text,
        int DelayAfterPreviousFrames,
        int WriteOnDurationFrames,
        string StatusText,
        string DeliveryStatus);

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

    public int GetResolvedModuleInstanceDurationFrames(string moduleInstanceId)
    {
        var settings = GetModuleInstanceSettings(moduleInstanceId);
        var module = GetModuleSettings(settings.ModuleId);
        return module.RecordClassId == "module.core.chat"
            ? ConversationModuleTiming.ResolveDurationFrames(
                settings.ContentJson,
                settings.BehaviorJson,
                settings.AnimationJson)
            : settings.DurationFrames;
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

    public IReadOnlyList<ConversationMessage> GetConversationMessages(string moduleInstanceId)
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
            (message["textReveal"] as JsonObject)?["durationFrames"]?.GetValue<int>() ?? 0,
            (message["status"] as JsonObject)?["text"]?.GetValue<string>() ?? "",
            (message["status"] as JsonObject)?["deliveryStatus"]?.GetValue<string>() ?? "none")).ToList();
    }

    public void AddConversationMessage(string moduleInstanceId)
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
                ["textReveal"] = new JsonObject { ["durationFrames"] = 0 },
                ["status"] = new JsonObject { ["text"] = "", ["deliveryStatus"] = "none" },
            });
        });
    }

    public void UpdateConversationMessage(string moduleInstanceId, string messageId, ConversationMessage next)
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
            message["textReveal"] = new JsonObject { ["durationFrames"] = Math.Max(0, next.WriteOnDurationFrames) };
            message["status"] = new JsonObject { ["text"] = next.StatusText, ["deliveryStatus"] = next.DeliveryStatus };
        });
    }

    public void DeleteConversationMessage(string moduleInstanceId, string messageId)
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
            ["schemaVersion"] = 1,
            ["header"] = new JsonObject
            {
                ["actorId"] = "",
                ["title"] = "Alex Q",
                ["subtitle"] = "online",
            },
            ["messages"] = new JsonArray(),
        }.ToJsonString();
    }

    private static string DefaultConversationModuleBehaviorJson()
    {
        return new JsonObject
        {
            ["headFrames"] = 0,
            ["tailFrames"] = 12,
            ["showHeader"] = true,
            ["showStatusBar"] = true,
            ["showNavigationBar"] = true,
            ["showTextInputBar"] = true,
            ["showKeyboard"] = false,
            ["initialScroll"] = "bottom",
        }.ToJsonString();
    }

    private static string DefaultModuleAnimationJson()
    {
        return new JsonObject
        {
            ["schemaVersion"] = 1,
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
