using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ActorRepository : IActorRepository
{
    private readonly SqliteProjectContext _context;

    public ActorRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public ActorSettings GetSettings(string actorId)
    {
        using var connection = _context.OpenConnection();
        return GetSettings(connection, actorId);
    }

    public void UpdateField(string actorId, string fieldId, string value)
    {
        using var connection = _context.OpenConnection();
        switch (fieldId)
        {
            case "actor.shortName":
                SqliteCommandExecutor.Execute(connection, "UPDATE actors SET short_name = $value WHERE id = $id", ("$id", actorId), ("$value", value));
                return;
            case "actor.defaultDeviceId":
                SqliteCommandExecutor.Execute(connection, "UPDATE actors SET default_device_id = $value WHERE id = $id", ("$id", actorId), ("$value", value));
                return;
            case "actor.defaultThemeId":
                SqliteCommandExecutor.Execute(connection, "UPDATE actors SET default_theme_id = $value WHERE id = $id", ("$id", actorId), ("$value", value));
                return;
        }

        var settings = GetSettings(connection, actorId);
        var metadata = JsonPath.ParseRequiredObject(settings.MetadataJson, $"Actor '{actorId}' metadata_json");
        switch (fieldId)
        {
            case "actor.color.modes":
                JsonPath.SetPair(metadata, value, ["modes", "light", "color"], ["modes", "dark", "color"], asNumber: false);
                break;
            case "actor.avatarTextColor.modes":
                JsonPath.SetPair(metadata, value, ["modes", "light", "avatarTextColor"], ["modes", "dark", "avatarTextColor"], asNumber: false);
                break;
            case "actor.wallpaper.images.light.filePath":
                JsonPath.Set(metadata, ["wallpaper", "images", "light", "filePath"], JsonValue.Create(value)!);
                break;
            case "actor.wallpaper.images.dark.filePath":
                JsonPath.Set(metadata, ["wallpaper", "images", "dark", "filePath"], JsonValue.Create(value)!);
                break;
            case "actor.wallpaper.kind":
                JsonPath.Set(metadata, ["wallpaper", "kind"], JsonValue.Create(value)!);
                break;
            case "actor.wallpaper.opacity":
                JsonPath.Set(metadata, ["wallpaper", "opacity"], JsonPath.NumberNode(value));
                break;
            case "actor.wallpaper.color":
                JsonPath.SetPair(metadata, value, ["modes", "light", "wallpaper", "color"], ["modes", "dark", "wallpaper", "color"], asNumber: false);
                break;
            case "actor.avatar.filePath":
                JsonPath.Set(metadata, ["avatar", "filePath"], JsonValue.Create(value)!);
                break;
            case "actor.avatar.scale":
                JsonPath.Set(metadata, ["avatar", "scale"], JsonPath.NumberNode(value));
                break;
            case "actor.avatar.offset":
                JsonPath.SetPair(metadata, value, ["avatar", "offsetX"], ["avatar", "offsetY"]);
                break;
            case "actor.avatar.useInitials":
                JsonPath.Set(metadata, ["avatar", "useInitials"], JsonValue.Create(BooleanText.Parse(value))!);
                break;
            case "actor.avatar.initialsPadding":
                JsonPath.Set(metadata, ["avatar", "initialsPadding"], JsonPath.NumberNode(value));
                break;
            default:
                throw new InvalidOperationException($"Unknown actor field '{fieldId}'.");
        }

        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE actors SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", actorId),
            ("$metadataJson", metadata.ToJsonString()));
    }

    public IReadOnlyList<ResourceOption> GetOptions(string projectId)
    {
        using var connection = _context.OpenConnection();
        return QueryAll(connection)
            .Where((actor) => actor.ProjectId == projectId)
            .OrderBy((actor) => actor.DisplayName)
            .Select((actor) => new ResourceOption(actor.Id, actor.DisplayName))
            .ToList();
    }

    public IReadOnlyList<ActorRecord> QueryAll(SqliteConnection connection)
    {
        var rows = new List<ActorRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json FROM actors ORDER BY display_name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ActorRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                SqliteCommandExecutor.ReadString(reader, 3),
                SqliteCommandExecutor.ReadString(reader, 4),
                SqliteCommandExecutor.ReadString(reader, 5),
                SqliteCommandExecutor.ReadString(reader, 6)));
        }

        return rows;
    }

    public ActorRecord Create(SqliteConnection connection, string projectId)
    {
        var index = SqliteCommandExecutor.ScalarLong(
            connection,
            "SELECT COUNT(*) FROM actors WHERE project_id = $projectId",
            ("$projectId", projectId)) + 1;
        var id = $"actor_{Guid.NewGuid():N}";
        var displayName = $"Actor {index}";
        var shortName = $"A{index}";
        var metadataJson = DefaultMetadataJson("blue", "gray_010");
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO actors (id, project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json)
            VALUES ($id, $projectId, $displayName, $shortName, '', '', $metadataJson)
            """,
            ("$id", id),
            ("$projectId", projectId),
            ("$displayName", displayName),
            ("$shortName", shortName),
            ("$metadataJson", metadataJson));
        return new ActorRecord(id, projectId, displayName, shortName, "", "", metadataJson);
    }

    public ActorRecord Duplicate(SqliteConnection connection, string sourceId, string copyName)
    {
        var source = QueryAll(connection).SingleOrDefault((actor) => actor.Id == sourceId)
            ?? throw new InvalidOperationException($"Missing actor '{sourceId}'.");
        var copy = source with { Id = $"actor_{Guid.NewGuid():N}", DisplayName = copyName };
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO actors (id, project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json)
            VALUES ($id, $projectId, $displayName, $shortName, $defaultDeviceId, $defaultThemeId, $metadataJson)
            """,
            ("$id", copy.Id),
            ("$projectId", copy.ProjectId),
            ("$displayName", copy.DisplayName),
            ("$shortName", copy.ShortName),
            ("$defaultDeviceId", copy.DefaultDeviceId),
            ("$defaultThemeId", copy.DefaultThemeId),
            ("$metadataJson", copy.MetadataJson));
        return copy;
    }

    public void Delete(SqliteConnection connection, string actorId)
    {
        SqliteCommandExecutor.Execute(connection, "DELETE FROM actors WHERE id = $id", ("$id", actorId));
    }

    public void Rename(SqliteConnection connection, string actorId, string name)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE actors SET display_name = $name WHERE id = $id",
            ("$id", actorId),
            ("$name", name));
    }

    private static ActorSettings GetSettings(SqliteConnection connection, string actorId)
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
            SqliteCommandExecutor.ReadString(reader, 2),
            SqliteCommandExecutor.ReadString(reader, 3),
            SqliteCommandExecutor.ReadString(reader, 4),
            SqliteCommandExecutor.ReadString(reader, 5));
    }

    private static string DefaultMetadataJson(string colorToken, string avatarTextColorToken)
    {
        var root = new JsonObject
        {
            ["modes"] = new JsonObject
            {
                ["light"] = new JsonObject
                {
                    ["color"] = colorToken,
                    ["avatarTextColor"] = avatarTextColorToken,
                    ["wallpaper"] = new JsonObject { ["color"] = "gray_100" },
                },
                ["dark"] = new JsonObject
                {
                    ["color"] = colorToken,
                    ["avatarTextColor"] = avatarTextColorToken,
                    ["wallpaper"] = new JsonObject { ["color"] = "gray_000" },
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
            ["wallpaper"] = new JsonObject
            {
                ["kind"] = "image",
                ["opacity"] = 1,
                ["images"] = new JsonObject
                {
                    ["light"] = new JsonObject { ["filePath"] = "wallpapers/image.16f45e146467c560c19b884f3017a4a2.png" },
                    ["dark"] = new JsonObject { ["filePath"] = "wallpapers/image.16f45e146467c560c19b884f3017a4a2.png" },
                },
            },
        };

        return root.ToJsonString();
    }
}
