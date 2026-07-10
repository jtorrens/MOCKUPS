using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
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
