using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void EnsureModuleInstanceColumns(SqliteConnection connection)
    {
        MigrateScreenInstancesToModuleInstances(connection);
        AddColumnIfMissing(connection, "module_instances", "name", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "module_instances", "notes", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "module_instances", "sort_order", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "module_instances", "duration_frames", "INTEGER NOT NULL DEFAULT 240");
        AddColumnIfMissing(connection, "module_instances", "transition_json", "TEXT NOT NULL DEFAULT '{\"type\":\"cut\"}'");
        AddColumnIfMissing(connection, "module_instances", "content_json", "TEXT NOT NULL DEFAULT '{}'");
        AddColumnIfMissing(connection, "module_instances", "behavior_json", "TEXT NOT NULL DEFAULT '{}'");
        AddColumnIfMissing(connection, "module_instances", "animation_json", "TEXT NOT NULL DEFAULT '{\"schemaVersion\":1,\"tracks\":[]}'");
        AddColumnIfMissing(connection, "module_instances", "metadata_json", "TEXT NOT NULL DEFAULT '{}'");
    }

    private static void MigrateScreenInstancesToModuleInstances(SqliteConnection connection)
    {
        if (!TableExists(connection, "screen_instances"))
        {
            return;
        }

        Execute(connection, """
            INSERT OR IGNORE INTO module_instances (
              id, shot_id, app_id, module_id, name, notes,
              sort_order, duration_frames, transition_json,
              content_json, behavior_json, animation_json, metadata_json)
            SELECT id, shot_id, app_id, module_id, name, notes,
                   layer_order, MAX(1, end_frame - start_frame), '{"type":"cut"}',
                   content_json, behavior_json, animation_json, metadata_json
            FROM screen_instances
            """);
        Execute(connection, "DROP TABLE screen_instances");
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1";
        command.Parameters.AddWithValue("$name", tableName);
        return command.ExecuteScalar() is not null;
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
