using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public ActorSettings GetActorSettings(string actorId)
    {
        using var connection = OpenConnection();
        return GetActorSettings(connection, actorId);
    }

    private static ActorSettings GetActorSettings(SqliteConnection connection, string actorId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json FROM actors WHERE id = $id";
        command.Parameters.AddWithValue("$id", actorId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing actor '{actorId}'.");
        }

        return new ActorSettings(
            reader.GetString(0),
            reader.GetString(1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4),
            ReadString(reader, 5));
    }

    public void UpdateActorField(string actorId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "actor.shortName":
                Execute(connection, "UPDATE actors SET short_name = $value WHERE id = $id", ("$id", actorId), ("$value", value));
                return;
            case "actor.defaultDeviceId":
                Execute(connection, "UPDATE actors SET default_device_id = $value WHERE id = $id", ("$id", actorId), ("$value", value));
                return;
            case "actor.defaultThemeId":
                Execute(connection, "UPDATE actors SET default_theme_id = $value WHERE id = $id", ("$id", actorId), ("$value", value));
                return;
        }

        var settings = GetActorSettings(connection, actorId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        switch (fieldId)
        {
            case "actor.color.modes":
                SetPair(metadata, value, ["modes", "light", "color"], ["modes", "dark", "color"], asNumber: false);
                break;
            case "actor.avatarTextColor.modes":
                SetPair(metadata, value, ["modes", "light", "avatarTextColor"], ["modes", "dark", "avatarTextColor"], asNumber: false);
                break;
            case "actor.avatar.filePath":
                SetJsonValue(metadata, ["avatar", "filePath"], JsonValue.Create(value)!);
                break;
            case "actor.avatar.scale":
                SetJsonValue(metadata, ["avatar", "scale"], NumberNode(value));
                break;
            case "actor.avatar.offset":
                SetPair(metadata, value, ["avatar", "offsetX"], ["avatar", "offsetY"]);
                break;
            case "actor.avatar.useInitials":
                SetJsonValue(metadata, ["avatar", "useInitials"], JsonValue.Create(StringToBool(value))!);
                break;
            case "actor.avatar.initialsPadding":
                SetJsonValue(metadata, ["avatar", "initialsPadding"], NumberNode(value));
                break;
            default:
                throw new InvalidOperationException($"Unknown actor field '{fieldId}'.");
        }

        Execute(
            connection,
            "UPDATE actors SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", actorId),
            ("$metadataJson", metadata.ToJsonString()));
    }

    public string GetActorFieldValue(string actorId, string fieldId)
    {
        var settings = GetActorSettings(actorId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        return fieldId switch
        {
            "actor.shortName" => settings.ShortName,
            "actor.defaultDeviceId" => settings.DefaultDeviceId,
            "actor.defaultThemeId" => settings.DefaultThemeId,
            "actor.color.modes" => MetricPair(settings.MetadataJson, ["modes", "light", "color"], ["modes", "dark", "color"]),
            "actor.avatarTextColor.modes" => MetricPair(settings.MetadataJson, ["modes", "light", "avatarTextColor"], ["modes", "dark", "avatarTextColor"]),
            "actor.avatar.filePath" => JsonString(metadata, ["avatar", "filePath"]),
            "actor.avatar.scale" => JsonNumberString(metadata, ["avatar", "scale"]),
            "actor.avatar.offset" => MetricPair(settings.MetadataJson, ["avatar", "offsetX"], ["avatar", "offsetY"]),
            "actor.avatar.useInitials" => BoolToString(JsonBool(metadata, ["avatar", "useInitials"])),
            "actor.avatar.initialsPadding" => JsonNumberString(metadata, ["avatar", "initialsPadding"]),
            _ => throw new InvalidOperationException($"Unknown actor field '{fieldId}'."),
        };
    }

    private static void SeedActorsIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM actors WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            foreach (var seed in ActorSeedRows)
            {
                Execute(
                    connection,
                    """
                    INSERT INTO actors (id, project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json)
                    VALUES ($id, $projectId, $displayName, $shortName, '', '', $metadataJson)
                    """,
                    ("$id", seed.Id),
                    ("$projectId", projectId),
                    ("$displayName", seed.DisplayName),
                    ("$shortName", seed.ShortName),
                    ("$metadataJson", seed.MetadataJson));
            }
        }
    }

    private static string DefaultActorMetadataJson(string colorToken, string avatarTextColorToken)
    {
        var root = new JsonObject
        {
            ["modes"] = new JsonObject
            {
                ["light"] = new JsonObject
                {
                    ["color"] = colorToken,
                    ["avatarTextColor"] = avatarTextColorToken,
                },
                ["dark"] = new JsonObject
                {
                    ["color"] = colorToken,
                    ["avatarTextColor"] = avatarTextColorToken,
                },
            },
            ["avatar"] = new JsonObject
            {
                ["useInitials"] = true,
                ["filePath"] = "",
                ["scale"] = 1,
                ["offsetX"] = 0,
                ["offsetY"] = 0,
                ["baseSize"] = 640,
                ["initialsPadding"] = 96,
            },
        };

        return root.ToJsonString();
    }

    private static readonly ActorSeedRow[] ActorSeedRows =
    [
        new("actor_alex", "Alex", "Alex", DefaultActorMetadataJson("pastel_sky", "gray_010")),
        new("actor_sam", "Sam", "Sam", DefaultActorMetadataJson("pastel_mint", "gray_010")),
        new("actor_alex_b", "Alex B", "Alex B", DefaultActorMetadataJson("pastel_yellow", "gray_010")),
    ];

    public IReadOnlyList<FieldOption> GetActorOptions(string projectId)
    {
        using var connection = OpenConnection();
        var options = QueryActorRows(connection)
            .Where((actor) => actor.ProjectId == projectId)
            .OrderBy((actor) => actor.DisplayName)
            .Select((actor) => new FieldOption(actor.Id, actor.DisplayName))
            .ToList();
        options.Insert(0, new FieldOption("", "None"));
        return options;
    }

    private static List<ActorRow> QueryActorRows(SqliteConnection connection)
    {
        var rows = new List<ActorRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json FROM actors ORDER BY display_name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ActorRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                ReadString(reader, 5),
                ReadString(reader, 6)));
        }

        return rows;
    }

}
