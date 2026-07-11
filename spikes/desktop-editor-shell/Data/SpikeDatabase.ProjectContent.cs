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
                INSERT INTO shots (id, episode_id, name, slug, version, notes, sort_order, fps_override, duration_frames, owner_actor_id, canvas_json, metadata_json)
                VALUES ($id, $episodeId, $name, $slug, $version, $notes, $sortOrder, $fpsOverride, $durationFrames, $ownerActorId, $canvasJson, $metadataJson)
                """,
                ("$id", $"shot_{Guid.NewGuid():N}"),
                ("$episodeId", targetEpisodeId),
                ("$name", shot.Name),
                ("$slug", shot.Slug),
                ("$version", shot.Version),
                ("$notes", shot.Notes),
                ("$sortOrder", index),
                ("$fpsOverride", shot.FpsOverride),
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
                INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order, config_json, design_preview_json, metadata_json)
                VALUES ($id, $appId, $recordClassId, $name, $notes, $sortOrder, $configJson, $designPreviewJson, $metadataJson)
                """,
                ("$id", $"module_{Guid.NewGuid():N}"),
                ("$appId", targetAppId),
                ("$recordClassId", module.RecordClassId),
                ("$name", module.Name),
                ("$notes", module.Notes),
                ("$sortOrder", index),
                ("$configJson", module.ConfigJson),
                ("$designPreviewJson", module.DesignPreviewJson),
                ("$metadataJson", module.MetadataJson));
        }
    }

    public ShotSettings GetShotSettings(string shotId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.slug, s.version, s.sort_order, p.default_fps, COALESCE(s.fps_override, p.default_fps), s.fps_override,
                   s.duration_frames, s.owner_actor_id, s.render_preset_id, s.canvas_json, s.metadata_json, e.project_id
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

        return new ShotSettings(
            ReadString(reader, 11),
            ReadString(reader, 0),
            reader.IsDBNull(1) ? 1 : reader.GetInt32(1),
            reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
            reader.IsDBNull(3) ? 25 : reader.GetInt32(3),
            reader.IsDBNull(4) ? 25 : reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(6) ? 240 : reader.GetInt32(6),
            ReadString(reader, 7),
            ReadString(reader, 8),
            ReadString(reader, 9),
            ReadString(reader, 10));
    }

    public void UpdateShotField(string shotId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (fieldId == "shot.fps" && value == "inherited")
        {
            Execute(connection, "UPDATE shots SET fps_override = NULL WHERE id = $id", ("$id", shotId));
            return;
        }

        var column = fieldId switch
        {
            "shot.slug" => "slug",
            "shot.version" => "version",
            "shot.sortOrder" => "sort_order",
            "shot.fps" => "fps_override",
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
        command.CommandText = """
            SELECT a.project_id, m.record_class_id, m.sort_order, m.config_json, m.design_preview_json, m.metadata_json
            FROM modules m
            JOIN apps a ON a.id = m.app_id
            WHERE m.id = $id
            """;
        command.Parameters.AddWithValue("$id", moduleId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing module '{moduleId}'.");
        }

        return new ModuleSettings(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            ReadString(reader, 3),
            ReadString(reader, 4),
            ReadString(reader, 5));
    }

    public void UpdateModuleDesignPreviewJson(string moduleId, string designPreviewJson)
    {
        using var connection = OpenConnection();
        Execute(
            connection,
            "UPDATE modules SET design_preview_json = $json WHERE id = $id",
            ("$json", designPreviewJson),
            ("$id", moduleId));
    }

    public AppSettings GetModuleAppSettings(string moduleId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.project_id, a.bundle_key, a.app_type, a.config_json, a.metadata_json
            FROM modules m
            JOIN apps a ON a.id = m.app_id
            WHERE m.id = $id
            """;
        command.Parameters.AddWithValue("$id", moduleId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing module '{moduleId}'.");
        }

        return new AppSettings(
            reader.GetString(0),
            ReadString(reader, 1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4));
    }

    public void UpdateModuleField(string moduleId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (fieldId == "module.appearanceMode"
            || fieldId.StartsWith("module.conversation.", StringComparison.Ordinal))
        {
            UpdateModuleConfigField(connection, moduleId, fieldId, value);
            return;
        }

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

    public string GetModuleConfigFieldValue(string moduleId, string fieldId)
    {
        var settings = GetModuleSettings(moduleId);
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
        return fieldId switch
        {
            "module.appearanceMode" => JsonString(config, ["appearanceMode"]) is "light" or "dark" ? JsonString(config, ["appearanceMode"]) : "inherit",
            "module.conversation.showHeader" => JsonBoolString(config, ["conversation", "showHeader"], defaultValue: true),
            "module.conversation.useAppWallpaper" => JsonBoolString(config, ["conversation", "useAppWallpaper"], defaultValue: true),
            "module.conversation.headerHeight" => JsonNumberString(config, ["conversation", "headerHeight"], "64"),
            "module.conversation.headerAvatarVariant" => JsonString(config, ["conversation", "headerAvatarVariant"]),
            "module.conversation.showStatusBar" => JsonBoolString(config, ["conversation", "showStatusBar"], defaultValue: true),
            "module.conversation.statusBarVariant" => JsonString(config, ["conversation", "statusBarVariant"]),
            "module.conversation.showNavigationBar" => JsonBoolString(config, ["conversation", "showNavigationBar"], defaultValue: true),
            "module.conversation.navigationBarVariant" => JsonString(config, ["conversation", "navigationBarVariant"]),
            "module.conversation.showTextInputBar" => JsonBoolString(config, ["conversation", "showTextInputBar"], defaultValue: true),
            "module.conversation.textInputBarVariant" => JsonString(config, ["conversation", "textInputBarVariant"]),
            "module.conversation.showKeyboard" => JsonBoolString(config, ["conversation", "showKeyboard"], defaultValue: true),
            "module.conversation.keyboardVariant" => JsonString(config, ["conversation", "keyboardVariant"]),
            "module.conversation.bubbleVariant" => JsonString(config, ["conversation", "bubbleVariant"]),
            "module.conversation.bubbleMaxWidth" => JsonNumberString(config, ["conversation", "bubbleMaxWidth"], "66"),
            "module.conversation.screenGutter" => JsonString(config, ["conversation", "screenGutter"]) is { Length: > 0 } gutter ? gutter : "theme.spacing.l|theme.spacing.l",
            "module.conversation.messageGap" => JsonString(config, ["conversation", "messageGap"]) is { Length: > 0 } gap ? gap : "theme.spacing.m",
            "module.conversation.messageViewportMotion" => JsonPath.Get(config, ["conversation", "messageViewportMotion"])?.ToJsonString()
                ?? (MotionVariantValue.Default with { Bounds = MotionVariantValue.Parent }).ToJsonString(),
            _ => throw new InvalidOperationException($"Unknown module config field '{fieldId}'."),
        };
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
        command.CommandText = "SELECT id, episode_id, name, slug, version, notes, sort_order, fps_override, duration_frames, owner_actor_id, render_preset_id, canvas_json, metadata_json FROM shots ORDER BY sort_order, name";
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
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
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
        command.CommandText = "SELECT id, app_id, record_class_id, name, notes, sort_order, config_json, design_preview_json, metadata_json FROM modules ORDER BY sort_order, name";
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
                ReadString(reader, 6),
                ReadString(reader, 7),
                ReadString(reader, 8)));
        }

        return rows;
    }

    private static void UpdateModuleConfigField(SqliteConnection connection, string moduleId, string fieldId, string value)
    {
        var config = ParseJsonObject(ScalarString(connection, "SELECT config_json FROM modules WHERE id = $id", ("$id", moduleId)) ?? "{}");
        var projectId = ScalarString(
            connection,
            """
            SELECT a.project_id
            FROM modules m
            JOIN apps a ON a.id = m.app_id
            WHERE m.id = $id
            """,
            ("$id", moduleId)) ?? "";

        switch (fieldId)
        {
            case "module.appearanceMode":
                SetJsonValue(config, ["appearanceMode"], JsonValue.Create(value is "light" or "dark" ? value : "inherit")!);
                break;
            case "module.conversation.showHeader":
                SetJsonValue(config, ["conversation", "showHeader"], JsonValue.Create(BoolFromText(value))!);
                break;
            case "module.conversation.useAppWallpaper":
                SetJsonValue(config, ["conversation", "useAppWallpaper"], JsonValue.Create(BoolFromText(value))!);
                break;
            case "module.conversation.headerHeight":
                SetJsonValue(config, ["conversation", "headerHeight"], NumberNode(value));
                break;
            case "module.conversation.headerAvatarVariant":
                SetJsonValue(config, ["conversation", "headerAvatarVariant"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "avatar", value))!);
                break;
            case "module.conversation.showStatusBar":
                SetJsonValue(config, ["conversation", "showStatusBar"], JsonValue.Create(BoolFromText(value))!);
                break;
            case "module.conversation.statusBarVariant":
                SetJsonValue(config, ["conversation", "statusBarVariant"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "status_bar", value))!);
                break;
            case "module.conversation.showNavigationBar":
                SetJsonValue(config, ["conversation", "showNavigationBar"], JsonValue.Create(BoolFromText(value))!);
                break;
            case "module.conversation.navigationBarVariant":
                SetJsonValue(config, ["conversation", "navigationBarVariant"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "navigation_bar", value))!);
                break;
            case "module.conversation.showTextInputBar":
                SetJsonValue(config, ["conversation", "showTextInputBar"], JsonValue.Create(BoolFromText(value))!);
                break;
            case "module.conversation.textInputBarVariant":
                SetJsonValue(config, ["conversation", "textInputBarVariant"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "textInputBar", value))!);
                break;
            case "module.conversation.showKeyboard":
                SetJsonValue(config, ["conversation", "showKeyboard"], JsonValue.Create(BoolFromText(value))!);
                break;
            case "module.conversation.keyboardVariant":
                SetJsonValue(config, ["conversation", "keyboardVariant"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "keyboard", value))!);
                break;
            case "module.conversation.bubbleVariant":
                SetJsonValue(config, ["conversation", "bubbleVariant"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "bubble", value))!);
                break;
            case "module.conversation.bubbleMaxWidth":
                SetJsonValue(config, ["conversation", "bubbleMaxWidth"], NumberNode(value));
                break;
            case "module.conversation.screenGutter":
                SetJsonValue(config, ["conversation", "screenGutter"], JsonValue.Create(value)!);
                break;
            case "module.conversation.messageGap":
                SetJsonValue(config, ["conversation", "messageGap"], JsonValue.Create(value)!);
                break;
            case "module.conversation.messageViewportMotion":
                SetJsonValue(config, ["conversation", "messageViewportMotion"], JsonNode.Parse(MotionVariantValue.Parse(value).ToJsonString())!);
                break;
            default:
                throw new InvalidOperationException($"Unknown module config field '{fieldId}'.");
        }

        Execute(connection, "UPDATE modules SET config_json = $configJson WHERE id = $id", ("$id", moduleId), ("$configJson", config.ToJsonString()));
    }

    private static JsonObject DefaultConversationConfigJson(string projectId)
    {
        return new JsonObject
        {
            ["appearanceMode"] = "inherit",
            ["conversation"] = new JsonObject
            {
                ["showHeader"] = true,
                ["useAppWallpaper"] = true,
                ["headerHeight"] = 64,
                ["headerAvatarVariant"] = SeededComponentPresetReference(projectId, "avatar"),
                ["showStatusBar"] = true,
                ["statusBarVariant"] = SeededComponentPresetReference(projectId, "status_bar"),
                ["showNavigationBar"] = true,
                ["navigationBarVariant"] = SeededComponentPresetReference(projectId, "navigation_bar"),
                ["showTextInputBar"] = true,
                ["textInputBarVariant"] = SeededComponentPresetReference(projectId, "textInputBar"),
                ["showKeyboard"] = true,
                ["keyboardVariant"] = SeededComponentPresetReference(projectId, "keyboard"),
                ["bubbleVariant"] = SeededComponentPresetReference(projectId, "bubble"),
                ["bubbleMaxWidth"] = 66,
                ["screenGutter"] = "theme.spacing.l|theme.spacing.l",
                ["messageGap"] = "theme.spacing.m",
                ["messageViewportMotion"] = JsonNode.Parse((MotionVariantValue.Default with { Bounds = MotionVariantValue.Parent }).ToJsonString()),
            },
        };
    }

    private static string SeededComponentPresetReference(string projectId, string componentType)
    {
        return ComponentPresetNodeId($"component_{projectId}_{componentType}", DefaultComponentPresetId);
    }

    private static JsonObject DefaultConversationDesignPreviewJson()
    {
        return new JsonObject
        {
            ["headerTitle"] = "Alex Q",
            ["headerSubtitle"] = "online",
            ["actorId"] = "",
            ["messages"] = new JsonArray
            {
                ConversationPreviewMessage("message_001", "incoming", "Tenias razon: ya podemos componer desde el modulo.", 0, 30, "duringWriteOn", false, false, false, "none", ""),
                ConversationPreviewMessage("message_002", "outgoing", "Perfecto. El modulo solo elige variantes y datos runtime.", 12, 42, "duringWriteOn", true, true, true, "read", ""),
                ConversationPreviewMessage("message_003", "system", "Siguiente paso: instancias reales.", 12, 0, "duringWriteOn", false, false, false, "none", ""),
            },
            ["conversationFrame"] = 0,
            ["inputs"] = new JsonArray
            {
                new JsonObject { ["id"] = "actor", ["label"] = "Actor", ["jsonKey"] = "actorId", ["kind"] = "recordReference", ["defaultValue"] = "", ["tableId"] = "actors", ["resolvedJsonKey"] = "actor" },
                new JsonObject { ["id"] = "headerTitle", ["label"] = "Header title", ["jsonKey"] = "headerTitle", ["kind"] = "text", ["defaultValue"] = "Alex Q" },
                new JsonObject { ["id"] = "headerSubtitle", ["label"] = "Header subtitle", ["jsonKey"] = "headerSubtitle", ["kind"] = "text", ["defaultValue"] = "online" },
                new JsonObject { ["id"] = "conversationFrame", ["label"] = "Timeline frame", ["jsonKey"] = "conversationFrame", ["kind"] = "number", ["defaultValue"] = "0", ["minimum"] = 0, ["maximum"] = 100000, ["increment"] = 1 },
            },
            ["collections"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "messages",
                    ["label"] = "Messages",
                    ["jsonKey"] = "messages",
                    ["itemLabel"] = "Message",
                    ["sourceCollectionJsonKey"] = "messages",
                    ["fields"] = ConversationPreviewMessageFields(),
                },
            },
            ["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "playConversation",
                    ["label"] = "Play messages",
                    ["playInputId"] = "conversationPlayback",
                    ["durationCollectionJsonKey"] = "messages",
                    ["durationItemNumberKeys"] = new JsonArray { "delayAfterPreviousFrames", "writeOnDurationFrames" },
                    ["timeJsonKey"] = "conversationFrame",
                    ["timeUnit"] = "frames",
                    ["prewarmFrames"] = false,
                },
            },
        };
    }

    private static JsonObject ConversationPreviewMessage(
        string id,
        string direction,
        string text,
        int delayAfterPreviousFrames,
        int writeOnDurationFrames,
        string bubbleRevealMode,
        bool textInputVisible,
        bool keyboardVisible,
        bool statusVisible,
        string statusState,
        string statusText)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["direction"] = direction,
            ["text"] = text,
            ["delayAfterPreviousFrames"] = delayAfterPreviousFrames,
            ["writeOnDurationFrames"] = writeOnDurationFrames,
            ["bubbleRevealMode"] = bubbleRevealMode,
            ["textInputVisible"] = textInputVisible,
            ["keyboardVisible"] = keyboardVisible,
            ["statusVisible"] = statusVisible,
            ["statusState"] = statusState,
            ["statusText"] = statusText,
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
        };
    }

    private static JsonArray ConversationPreviewMessageFields()
    {
        var fields = new JsonArray
        {
            new JsonObject { ["id"] = "direction", ["label"] = "Direction", ["jsonKey"] = "direction", ["kind"] = "option", ["defaultValue"] = "incoming", ["options"] = new JsonArray { new JsonObject { ["value"] = "incoming", ["label"] = "Incoming" }, new JsonObject { ["value"] = "outgoing", ["label"] = "Outgoing" }, new JsonObject { ["value"] = "system", ["label"] = "System" } } },
            new JsonObject { ["id"] = "text", ["label"] = "Text", ["jsonKey"] = "text", ["kind"] = "multilineText", ["defaultValue"] = "" },
            new JsonObject { ["id"] = "delay", ["label"] = "Delay", ["jsonKey"] = "delayAfterPreviousFrames", ["kind"] = "number", ["defaultValue"] = "0", ["minimum"] = 0, ["maximum"] = 100000, ["increment"] = 1 },
            new JsonObject { ["id"] = "writeOn", ["label"] = "Write-on frames", ["jsonKey"] = "writeOnDurationFrames", ["kind"] = "number", ["defaultValue"] = "0", ["minimum"] = 0, ["maximum"] = 100000, ["increment"] = 1 },
            new JsonObject { ["id"] = "bubbleReveal", ["label"] = "Bubble reveal", ["jsonKey"] = "bubbleRevealMode", ["kind"] = "option", ["defaultValue"] = "duringWriteOn", ["options"] = new JsonArray { new JsonObject { ["value"] = "duringWriteOn", ["label"] = "During write-on" }, new JsonObject { ["value"] = "afterWriteOn", ["label"] = "After write-on" } } },
            new JsonObject { ["id"] = "textInput", ["label"] = "Text input visible while writing", ["jsonKey"] = "textInputVisible", ["kind"] = "boolean", ["defaultValue"] = "false" },
            new JsonObject { ["id"] = "keyboard", ["label"] = "Keyboard visible while writing", ["jsonKey"] = "keyboardVisible", ["kind"] = "boolean", ["defaultValue"] = "false" },
            new JsonObject { ["id"] = "statusVisible", ["label"] = "Show delivery status", ["jsonKey"] = "statusVisible", ["kind"] = "boolean", ["defaultValue"] = "false" },
            new JsonObject { ["id"] = "status", ["label"] = "Status", ["jsonKey"] = "statusState", ["kind"] = "option", ["defaultValue"] = "none", ["options"] = new JsonArray { new JsonObject { ["value"] = "none", ["label"] = "None" }, new JsonObject { ["value"] = "sent", ["label"] = "Sent" }, new JsonObject { ["value"] = "delivered", ["label"] = "Delivered" }, new JsonObject { ["value"] = "read", ["label"] = "Read" } } },
            new JsonObject { ["id"] = "statusText", ["label"] = "Status text", ["jsonKey"] = "statusText", ["kind"] = "text", ["defaultValue"] = "" },
            new JsonObject { ["id"] = "mediaType", ["label"] = "Attachment type", ["jsonKey"] = "mediaType", ["kind"] = "option", ["defaultValue"] = "none", ["options"] = new JsonArray { new JsonObject { ["value"] = "none", ["label"] = "None" }, new JsonObject { ["value"] = "image", ["label"] = "Image" }, new JsonObject { ["value"] = "video", ["label"] = "Video" }, new JsonObject { ["value"] = "audio", ["label"] = "Audio" } } },
            new JsonObject { ["id"] = "mediaSource", ["label"] = "Media source", ["jsonKey"] = "mediaSource", ["kind"] = "text", ["valueKind"] = "MediaFilePath", ["defaultValue"] = "", ["enabledWhenItemJsonKey"] = "mediaType", ["enabledWhenItemValues"] = new JsonArray { "image", "video", "audio" } },
            new JsonObject { ["id"] = "viewport", ["label"] = "Media viewport", ["jsonKey"] = "viewportSize", ["kind"] = "integerPair", ["defaultValue"] = "240|160", ["pairFirstLabel"] = "W", ["pairSecondLabel"] = "H" },
            new JsonObject { ["id"] = "mediaScale", ["label"] = "Media scale", ["jsonKey"] = "mediaScale", ["kind"] = "number", ["defaultValue"] = "1", ["minimum"] = 0.01, ["maximum"] = 100, ["increment"] = 0.01 },
            new JsonObject { ["id"] = "mediaOffset", ["label"] = "Media offset", ["jsonKey"] = "mediaOffset", ["kind"] = "integerPair", ["defaultValue"] = "0|0", ["pairFirstLabel"] = "X", ["pairSecondLabel"] = "Y" },
            new JsonObject { ["id"] = "isPlaying", ["label"] = "Playing", ["jsonKey"] = "isPlaying", ["kind"] = "boolean", ["defaultValue"] = "false" },
            new JsonObject { ["id"] = "currentTime", ["label"] = "Current time", ["jsonKey"] = "currentTimeSeconds", ["kind"] = "number", ["defaultValue"] = "0", ["minimum"] = 0, ["maximum"] = 86400, ["increment"] = 0.01 },
            new JsonObject { ["id"] = "duration", ["label"] = "Duration", ["jsonKey"] = "durationSeconds", ["kind"] = "number", ["defaultValue"] = "12", ["minimum"] = 1, ["maximum"] = 86400, ["increment"] = 0.01 },
            new JsonObject { ["id"] = "fullScreen", ["label"] = "Full screen", ["jsonKey"] = "isFullScreen", ["kind"] = "boolean", ["defaultValue"] = "false" },
            new JsonObject { ["id"] = "fullScreenTransition", ["label"] = "Full-screen transition", ["jsonKey"] = "fullScreenTransition", ["kind"] = "boolean", ["defaultValue"] = "false" },
            new JsonObject { ["id"] = "fullframeOrientation", ["label"] = "Fullframe orientation", ["jsonKey"] = "fullframeOrientation", ["kind"] = "option", ["defaultValue"] = "portrait", ["options"] = new JsonArray { new JsonObject { ["value"] = "portrait", ["label"] = "Portrait" }, new JsonObject { ["value"] = "landscape", ["label"] = "Landscape" } } },
            new JsonObject { ["id"] = "controlsElapsed", ["label"] = "Controls elapsed ms", ["jsonKey"] = "controlsElapsedMs", ["kind"] = "number", ["defaultValue"] = "0", ["minimum"] = 0, ["maximum"] = 86400000, ["increment"] = 1 },
        };
        ApplyConversationRuntimeGroups(fields);
        return fields;
    }

    private static void ApplyConversationRuntimeGroups(JsonArray fields)
    {
        SetRuntimeGroup(fields, ["delay", "writeOn", "bubbleReveal", "textInput", "keyboard"], "timing", "Timing", 20);
        SetRuntimeGroup(fields, ["statusVisible", "status", "statusText"], "delivery", "Delivery", 30);
        SetRuntimeGroup(fields, ["mediaType", "mediaSource"], "attachment", "Attachment", 40);
        SetRuntimeGroup(fields, ["viewport", "mediaScale", "mediaOffset"], "attachment", "Attachment", 50, sectionLabel: "Frame");
        SetRuntimeGroup(fields, ["isPlaying", "currentTime", "duration", "controlsElapsed"], "attachment", "Attachment", 60, sectionLabel: "Playback");
        SetRuntimeGroup(fields, ["fullScreen", "fullScreenTransition", "fullframeOrientation"], "attachment", "Attachment", 70, sectionLabel: "Full screen");
    }

    private static void SetRuntimeGroup(
        JsonArray fields,
        string[] ids,
        string groupId,
        string groupLabel,
        int groupOrder,
        string parentGroupId = "",
        string sectionLabel = "")
    {
        foreach (var id in ids)
        {
            var field = fields.OfType<JsonObject>().FirstOrDefault((candidate) => candidate["id"]?.GetValue<string>() == id);
            if (field is null) continue;
            field["uiGroupId"] = groupId;
            field["uiGroupLabel"] = groupLabel;
            field["uiParentGroupId"] = parentGroupId;
            field["uiOrder"] = groupOrder + Array.IndexOf(ids, id);
            field["uiSectionLabel"] = sectionLabel;
        }
    }

    private static string JsonBoolString(JsonObject owner, string[] path, bool defaultValue)
    {
        var node = JsonPath.Get(owner, path);
        return node is JsonValue value && value.TryGetValue<bool>(out var result)
            ? result ? "true" : "false"
            : defaultValue ? "true" : "false";
    }

    private static bool BoolFromText(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

}
