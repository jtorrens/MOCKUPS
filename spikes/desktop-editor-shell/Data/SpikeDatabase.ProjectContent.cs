using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void DuplicateShots(SqliteConnection connection, string sourceEpisodeId, string targetEpisodeId)
    {
        var sourceShots = QueryShotRows(connection)
            .Where((shot) => shot.EpisodeId == sourceEpisodeId)
            .OrderBy((shot) => shot.SortOrder)
            .ToList();

        for (var index = 0; index < sourceShots.Count; index++)
        {
            var shot = sourceShots[index];
            Execute(
                connection,
                """
                INSERT INTO shots (id, episode_id, name, slug, version, notes, sort_order, fps, duration_frames, owner_actor_id, canvas_json, metadata_json)
                VALUES ($id, $episodeId, $name, $slug, $version, $notes, $sortOrder, $fps, $durationFrames, $ownerActorId, $canvasJson, $metadataJson)
                """,
                ("$id", $"shot_{Guid.NewGuid():N}"),
                ("$episodeId", targetEpisodeId),
                ("$name", shot.Name),
                ("$slug", shot.Slug),
                ("$version", shot.Version),
                ("$notes", shot.Notes),
                ("$sortOrder", index),
                ("$fps", shot.Fps),
                ("$durationFrames", shot.DurationFrames),
                ("$ownerActorId", shot.OwnerActorId),
                ("$canvasJson", shot.CanvasJson),
                ("$metadataJson", shot.MetadataJson));
        }
    }

    private static void DuplicateModules(SqliteConnection connection, string sourceAppId, string targetAppId)
    {
        var sourceModules = QueryModuleRows(connection)
            .Where((module) => module.AppId == sourceAppId)
            .OrderBy((module) => module.SortOrder)
            .ToList();

        for (var index = 0; index < sourceModules.Count; index++)
        {
            var module = sourceModules[index];
            Execute(
                connection,
                """
                INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order, metadata_json)
                VALUES ($id, $appId, $recordClassId, $name, $notes, $sortOrder, $metadataJson)
                """,
                ("$id", $"module_{Guid.NewGuid():N}"),
                ("$appId", targetAppId),
                ("$recordClassId", module.RecordClassId),
                ("$name", module.Name),
                ("$notes", module.Notes),
                ("$sortOrder", index),
                ("$metadataJson", module.MetadataJson));
        }
    }

    public ShotSettings GetShotSettings(string shotId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.slug, s.version, s.sort_order, s.fps, s.duration_frames, s.owner_actor_id, s.render_preset_id, s.canvas_json, s.metadata_json, e.project_id
            FROM shots s
            JOIN episodes e ON e.id = s.episode_id
            WHERE s.id = $id
            """;
        command.Parameters.AddWithValue("$id", shotId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing shot '{shotId}'.");
        }

        return new ShotSettings(
            ReadString(reader, 9),
            ReadString(reader, 0),
            reader.IsDBNull(1) ? 1 : reader.GetInt32(1),
            reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
            reader.IsDBNull(3) ? 25 : reader.GetInt32(3),
            reader.IsDBNull(4) ? 240 : reader.GetInt32(4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7),
            ReadString(reader, 8));
    }

    public void UpdateShotField(string shotId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        var column = fieldId switch
        {
            "shot.slug" => "slug",
            "shot.version" => "version",
            "shot.sortOrder" => "sort_order",
            "shot.fps" => "fps",
            "shot.durationFrames" => "duration_frames",
            "shot.ownerActorId" => "owner_actor_id",
            "shot.renderPresetId" => "render_preset_id",
            "shot.canvas" => "canvas_json",
            "shot.metadata" => "metadata_json",
            _ => throw new InvalidOperationException($"Unknown shot field '{fieldId}'."),
        };
        object nextValue = fieldId is "shot.version" or "shot.sortOrder" or "shot.fps" or "shot.durationFrames"
            ? NumericText.Int32(value, 0)
            : value;

        Execute(
            connection,
            $"UPDATE shots SET {column} = $value WHERE id = $id",
            ("$id", shotId),
            ("$value", nextValue));
    }

    public string GetShotRenderName(string shotId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.slug, p.name, e.slug, e.name, s.slug, s.name, s.version
            FROM shots s
            JOIN episodes e ON e.id = s.episode_id
            JOIN projects p ON p.id = e.project_id
            WHERE s.id = $id
            """;
        command.Parameters.AddWithValue("$id", shotId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing shot '{shotId}'.");
        }

        var projectSlug = SlugOrName(ReadString(reader, 0), reader.GetString(1), "project");
        var episodeSlug = SlugOrName(ReadString(reader, 2), reader.GetString(3), "episode");
        var shotSlug = SlugOrName(ReadString(reader, 4), reader.GetString(5), "shot");
        var version = reader.IsDBNull(6) ? 1 : reader.GetInt32(6);
        return $"{projectSlug}_{episodeSlug}_{shotSlug}_v{Math.Max(0, version):00}";
    }

    public string GetShotOwnerDeviceName(string shotId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.name
            FROM shots s
            JOIN actors a ON a.id = s.owner_actor_id
            JOIN devices d ON d.id = a.default_device_id
            WHERE s.id = $id
            """;
        command.Parameters.AddWithValue("$id", shotId);
        return command.ExecuteScalar() as string ?? "No default device";
    }

    public AppSettings GetAppSettings(string appId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, bundle_key, app_type, config_json, metadata_json FROM apps WHERE id = $id";
        command.Parameters.AddWithValue("$id", appId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing app '{appId}'.");
        }

        return new AppSettings(
            reader.GetString(0),
            ReadString(reader, 1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4));
    }

    public ModuleSettings GetModuleSettings(string moduleId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT record_class_id, sort_order, metadata_json FROM modules WHERE id = $id";
        command.Parameters.AddWithValue("$id", moduleId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing module '{moduleId}'.");
        }

        return new ModuleSettings(
            reader.GetString(0),
            reader.GetInt32(1),
            ReadString(reader, 2));
    }

    public void UpdateModuleField(string moduleId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "module.sortOrder":
                Execute(
                    connection,
                    "UPDATE modules SET sort_order = $value WHERE id = $id",
                    ("$id", moduleId),
                    ("$value", NumericText.Int32(value, 0)));
                return;
            case "module.metadata":
                Execute(
                    connection,
                    "UPDATE modules SET metadata_json = $value WHERE id = $id",
                    ("$id", moduleId),
                    ("$value", value));
                return;
            case "module.recordClassId":
                return;
            default:
                throw new InvalidOperationException($"Unknown module field '{fieldId}'.");
        }
    }

    public void UpdateAppField(string appId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (fieldId.StartsWith("app.wallpaper.", StringComparison.Ordinal))
        {
            UpdateAppConfigField(connection, appId, fieldId, value);
            return;
        }

        if (fieldId.StartsWith("app.icon.", StringComparison.Ordinal) || fieldId == "app.note")
        {
            UpdateAppMetadataField(connection, appId, fieldId, value);
            return;
        }

        var column = fieldId switch
        {
            "app.bundleKey" => "bundle_key",
            "app.appType" => "app_type",
            "app.config" => "config_json",
            "app.metadata" => "metadata_json",
            _ => throw new InvalidOperationException($"Unknown app field '{fieldId}'."),
        };

        Execute(
            connection,
            $"UPDATE apps SET {column} = $value WHERE id = $id",
            ("$id", appId),
            ("$value", value));
    }

    public string GetAppConfigFieldValue(string appId, string fieldId)
    {
        var settings = GetAppSettings(appId);
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
        var lightWallpaperColor = JsonString(config, ["modes", "light", "wallpaper", "color"]);
        if (string.IsNullOrWhiteSpace(lightWallpaperColor)) lightWallpaperColor = "gray_100";
        var darkWallpaperColor = JsonString(config, ["modes", "dark", "wallpaper", "color"]);
        if (string.IsNullOrWhiteSpace(darkWallpaperColor)) darkWallpaperColor = "gray_000";
        return fieldId switch
        {
            "app.wallpaper.kind" => JsonString(config, ["wallpaper", "kind"]) is { Length: > 0 } kind ? kind : "solid",
            "app.wallpaper.opacity" => JsonNumberString(config, ["wallpaper", "opacity"], "1"),
            "app.wallpaper.color" => $"{lightWallpaperColor}|{darkWallpaperColor}",
            "app.wallpaper.image.filePath" => JsonString(config, ["wallpaper", "image", "filePath"]),
            _ => throw new InvalidOperationException($"Unknown app config field '{fieldId}'."),
        };
    }

    public string GetAppMetadataFieldValue(string appId, string fieldId)
    {
        var settings = GetAppSettings(appId);
        var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
        return fieldId switch
        {
            "app.note" => JsonString(metadata, ["note"]),
            "app.icon.filePath" => JsonString(metadata, ["icon", "filePath"]),
            "app.icon.scale" => JsonNumberString(metadata, ["icon", "scale"], "1"),
            "app.icon.offset" => $"{JsonNumberString(metadata, ["icon", "offsetX"], "0")}|{JsonNumberString(metadata, ["icon", "offsetY"], "0")}",
            _ => throw new InvalidOperationException($"Unknown app metadata field '{fieldId}'."),
        };
    }

    private static void UpdateAppConfigField(SqliteConnection connection, string appId, string fieldId, string value)
    {
        var config = ParseJsonObject(ScalarString(connection, "SELECT config_json FROM apps WHERE id = $id", ("$id", appId)) ?? "{}");
        switch (fieldId)
        {
            case "app.wallpaper.kind":
                SetJsonValue(config, ["wallpaper", "kind"], JsonValue.Create(value)!);
                break;
            case "app.wallpaper.opacity":
                SetJsonValue(config, ["wallpaper", "opacity"], NumberNode(value));
                break;
            case "app.wallpaper.color":
                SetPair(
                    config,
                    value,
                    ["modes", "light", "wallpaper", "color"],
                    ["modes", "dark", "wallpaper", "color"],
                    asNumber: false);
                break;
            case "app.wallpaper.image.filePath":
                SetJsonValue(config, ["wallpaper", "image", "filePath"], JsonValue.Create(value)!);
                break;
            default:
                throw new InvalidOperationException($"Unknown app config field '{fieldId}'.");
        }

        Execute(connection, "UPDATE apps SET config_json = $configJson WHERE id = $id", ("$id", appId), ("$configJson", config.ToJsonString()));
    }

    private static void UpdateAppMetadataField(SqliteConnection connection, string appId, string fieldId, string value)
    {
        var metadata = ParseJsonObject(ScalarString(connection, "SELECT metadata_json FROM apps WHERE id = $id", ("$id", appId)) ?? "{}");
        switch (fieldId)
        {
            case "app.note":
                SetJsonValue(metadata, ["note"], JsonValue.Create(value)!);
                break;
            case "app.icon.filePath":
                SetJsonValue(metadata, ["icon", "filePath"], JsonValue.Create(value)!);
                break;
            case "app.icon.scale":
                SetJsonValue(metadata, ["icon", "scale"], NumberNode(value));
                break;
            case "app.icon.offset":
                SetPair(metadata, value, ["icon", "offsetX"], ["icon", "offsetY"]);
                break;
            default:
                throw new InvalidOperationException($"Unknown app metadata field '{fieldId}'.");
        }

        Execute(connection, "UPDATE apps SET metadata_json = $metadataJson WHERE id = $id", ("$id", appId), ("$metadataJson", metadata.ToJsonString()));
    }

    private static List<ShotRow> QueryShotRows(SqliteConnection connection)
    {
        var rows = new List<ShotRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, episode_id, name, slug, version, notes, sort_order, fps, duration_frames, owner_actor_id, render_preset_id, canvas_json, metadata_json FROM shots ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ShotRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                reader.GetInt32(4),
                ReadString(reader, 5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                ReadString(reader, 9),
                ReadString(reader, 10),
                ReadString(reader, 11),
                ReadString(reader, 12)));
        }

        return rows;
    }

    private static List<AppRow> QueryAppRows(SqliteConnection connection)
    {
        var rows = new List<AppRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, record_class_id, name, notes, sort_order FROM apps ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new AppRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ReadString(reader, 4),
                reader.GetInt32(5)));
        }

        return rows;
    }

    private static List<ModuleRow> QueryModuleRows(SqliteConnection connection)
    {
        var rows = new List<ModuleRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, app_id, record_class_id, name, notes, sort_order, metadata_json FROM modules ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ModuleRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ReadString(reader, 4),
                reader.GetInt32(5),
                ReadString(reader, 6)));
        }

        return rows;
    }


    private static void EnsureShotColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "shots", "slug", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "shots", "version", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "shots", "owner_actor_id", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "shots", "render_preset_id", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "shots", "canvas_json", "TEXT NOT NULL DEFAULT '{}'");
        Execute(
            connection,
            """
            UPDATE shots
            SET slug = lower(replace(trim(name), ' ', '-'))
            WHERE trim(slug) = ''
            """);
    }

    private static void EnsureAppColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "apps", "bundle_key", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "apps", "app_type", "TEXT NOT NULL DEFAULT 'chat'");
        AddColumnIfMissing(connection, "apps", "config_json", "TEXT NOT NULL DEFAULT '{}'");
        Execute(
            connection,
            """
            UPDATE apps
            SET bundle_key = lower(replace(trim(name), ' ', '-'))
            WHERE trim(bundle_key) = ''
            """);
    }

}
