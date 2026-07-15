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
            || fieldId.StartsWith("module.conversation.", StringComparison.Ordinal)
            || fieldId.StartsWith("module.lockScreen.", StringComparison.Ordinal))
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
        return ModuleConfigFieldValue(settings.ConfigJson, fieldId);
    }

    private static string ModuleConfigFieldValue(string configJson, string fieldId)
    {
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        return fieldId switch
        {
            "module.appearanceMode" => JsonString(config, ["appearanceMode"]) is "light" or "dark" ? JsonString(config, ["appearanceMode"]) : "inherit",
            "module.conversation.showHeader" => JsonBoolString(config, ["conversation", "showHeader"], defaultValue: true),
            "module.conversation.useAppWallpaper" => JsonBoolString(config, ["conversation", "useAppWallpaper"], defaultValue: true),
            "module.conversation.headerHeight" => JsonNumberString(config, ["conversation", "headerHeight"], "64"),
            "module.conversation.headerAvatarVariant" => JsonString(config, ["conversation", "headerAvatarVariant"]),
            "module.conversation.headerAvatarAlignment" => JsonString(config, ["conversation", "headerAvatarAlignment"]) is { Length: > 0 } alignment ? alignment : "left",
            "module.conversation.headerLeftIconRow.editor" => JsonString(config, ["conversation", "headerLeftIconRowSlot", "presetId"]),
            "module.conversation.headerLeftIconRow.inputs" => JsonPath.Get(config, ["conversation", "headerLeftIconRowInputs"])?.ToJsonString() ?? "{}",
            "module.conversation.headerRightIconRow.editor" => JsonString(config, ["conversation", "headerRightIconRowSlot", "presetId"]),
            "module.conversation.headerRightIconRow.inputs" => JsonPath.Get(config, ["conversation", "headerRightIconRowInputs"])?.ToJsonString() ?? "{}",
            "module.conversation.showStatusBar" => JsonBoolString(config, ["conversation", "showStatusBar"], defaultValue: true),
            "module.conversation.showNavigationBar" => JsonBoolString(config, ["conversation", "showNavigationBar"], defaultValue: true),
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
            "module.lockScreen.statusBarVariant" => JsonString(config, ["lockScreen", "statusBarSlot", "presetId"]),
            "module.lockScreen.navigationBarVariant" => JsonString(config, ["lockScreen", "navigationBarSlot", "presetId"]),
            "module.lockScreen.stackVariant" => JsonString(config, ["lockScreen", "stackSlot", "presetId"]),
            "module.lockScreen.stackInputs" => JsonPath.Get(config, ["lockScreen", "stackInputs"])?.ToJsonString() ?? "{}",
            "module.lockScreen.stackItems" => JsonPath.Get(config, ["lockScreen", "stackInputs", "items"])?.ToJsonString() ?? "[]",
            _ => throw new InvalidOperationException($"Unknown module config field '{fieldId}'."),
        };
    }

    public void UpdateAppField(string appId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (fieldId.StartsWith("app.wallpaper.", StringComparison.Ordinal))
        {
            if (ScalarString(connection, "SELECT app_type FROM apps WHERE id = $id", ("$id", appId)) == "system")
            {
                throw new InvalidOperationException("System apps inherit Actor wallpaper and cannot own wallpaper fields.");
            }
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
        var darkWallpaperColor = JsonString(config, ["modes", "dark", "wallpaper", "color"]);
        return fieldId switch
        {
            "app.wallpaper.kind" => JsonString(config, ["wallpaper", "kind"]),
            "app.wallpaper.opacity" => JsonNumberString(config, ["wallpaper", "opacity"]),
            "app.wallpaper.color" => $"{lightWallpaperColor}|{darkWallpaperColor}",
            "app.wallpaper.images.light.filePath" => JsonString(config, ["wallpaper", "images", "light", "filePath"]),
            "app.wallpaper.images.dark.filePath" => JsonString(config, ["wallpaper", "images", "dark", "filePath"]),
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
            case "app.wallpaper.images.light.filePath":
                SetJsonValue(config, ["wallpaper", "images", "light", "filePath"], JsonValue.Create(value)!);
                break;
            case "app.wallpaper.images.dark.filePath":
                SetJsonValue(config, ["wallpaper", "images", "dark", "filePath"], JsonValue.Create(value)!);
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

        UpdateModuleConfigFieldValue(connection, projectId, config, fieldId, value);
        Execute(connection, "UPDATE modules SET config_json = $configJson WHERE id = $id", ("$id", moduleId), ("$configJson", config.ToJsonString()));
    }

    private static void UpdateModuleConfigFieldValue(
        SqliteConnection connection,
        string projectId,
        JsonObject config,
        string fieldId,
        string value)
    {
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
            case "module.conversation.headerAvatarAlignment":
                SetJsonValue(config, ["conversation", "headerAvatarAlignment"], JsonValue.Create(value is "center" or "right" ? value : "left")!);
                break;
            case "module.conversation.headerLeftIconRow.editor":
                SetJsonValue(config, ["conversation", "headerLeftIconRowSlot", "presetId"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "iconRow", value))!);
                break;
            case "module.conversation.headerLeftIconRow.inputs":
                SetJsonValue(config, ["conversation", "headerLeftIconRowInputs"], JsonNode.Parse(value) as JsonObject ?? new JsonObject());
                break;
            case "module.conversation.headerRightIconRow.editor":
                SetJsonValue(config, ["conversation", "headerRightIconRowSlot", "presetId"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "iconRow", value))!);
                break;
            case "module.conversation.headerRightIconRow.inputs":
                SetJsonValue(config, ["conversation", "headerRightIconRowInputs"], JsonNode.Parse(value) as JsonObject ?? new JsonObject());
                break;
            case "module.conversation.showStatusBar":
                SetJsonValue(config, ["conversation", "showStatusBar"], JsonValue.Create(BoolFromText(value))!);
                break;
            case "module.conversation.showNavigationBar":
                SetJsonValue(config, ["conversation", "showNavigationBar"], JsonValue.Create(BoolFromText(value))!);
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
            case "module.lockScreen.statusBarVariant":
                SetJsonValue(config, ["lockScreen", "statusBarSlot", "presetId"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "status_bar", value))!);
                break;
            case "module.lockScreen.navigationBarVariant":
                SetJsonValue(config, ["lockScreen", "navigationBarSlot", "presetId"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "navigation_bar", value))!);
                break;
            case "module.lockScreen.stackVariant":
                SetJsonValue(config, ["lockScreen", "stackSlot", "presetId"], JsonValue.Create(ValidateComponentPresetReference(connection, projectId, "componentStack", value))!);
                break;
            case "module.lockScreen.stackInputs":
                SetJsonValue(config, ["lockScreen", "stackInputs"], JsonNode.Parse(value) as JsonObject ?? new JsonObject());
                break;
            case "module.lockScreen.stackItems":
                SetJsonValue(config, ["lockScreen", "stackInputs", "items"], JsonNode.Parse(value) as JsonArray ?? new JsonArray());
                break;
            default:
                throw new InvalidOperationException($"Unknown module config field '{fieldId}'.");
        }

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
                ["headerAvatarAlignment"] = "left",
                ["headerLeftIconRowSlot"] = new JsonObject { ["presetId"] = SeededComponentPresetReference(projectId, "iconRow"), ["overrides"] = new JsonObject() },
                ["headerLeftIconRowInputs"] = HeaderIconRowInputs(projectId, []),
                ["headerRightIconRowSlot"] = new JsonObject { ["presetId"] = SeededComponentPresetReference(projectId, "iconRow"), ["overrides"] = new JsonObject() },
                ["headerRightIconRowInputs"] = HeaderIconRowInputs(projectId, ["media_camera"]),
                ["showStatusBar"] = true,
                ["showNavigationBar"] = true,
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

    private static JsonObject HeaderIconRowInputs(string projectId, IReadOnlyList<string> icons)
    {
        var items = new JsonArray();
        for (var index = 0; index < icons.Count; index++)
        {
            items.Add(new JsonObject
            {
                ["id"] = $"button_{index + 1:000}",
                ["buttonPresetId"] = SeededComponentPresetReference(projectId, "button"),
                ["contentMode"] = "icon",
                ["state"] = "normal",
                ["iconToken"] = icons[index],
                ["text"] = "",
                ["iconSizeToken"] = "theme.iconSizes.m",
                ["textSizeToken"] = "theme.typography.sizes.s",
                ["pushTrigger"] = false,
                ["pushElapsedMs"] = 0,
                ["buttonOverrides"] = new JsonObject(),
            });
        }
        return new JsonObject
        {
            ["items"] = items,
            ["gap"] = "theme.spacing.s",
            ["orientation"] = "horizontal",
        };
    }

    private static string SeededComponentPresetReference(string projectId, string componentType)
    {
        return ComponentPresetNodeId($"component_{projectId}_{componentType}", DefaultComponentPresetId);
    }

    private static JsonObject DefaultConversationDesignPreviewJson()
    {
        var preview = new JsonObject
        {
            ["conversationType"] = "individual",
            ["headerSubtitle"] = "online",
            ["actorId"] = "",
            ["bubbleRevealMode"] = "afterWriteOn",
            ["incomingRevealMode"] = "typingIndicator",
            ["textInputVisible"] = true,
            ["keyboardVisible"] = true,
            ["typingIndicatorText"] = "•••",
            ["typingIndicatorSizeToken"] = "theme.typography.sizes.m",
            ["typingIndicatorAnimation"] = "pulsating",
            ["messages"] = new JsonArray
            {
                ConversationPreviewMessage("message_001", "incoming", "Tenias razon: ya podemos componer desde el modulo.", 0, 30, 0, false, "none", ""),
                ConversationPreviewMessage("message_002", "outgoing", "Perfecto. El modulo solo elige variantes y datos runtime.", 12, 42, 12, true, "read", ""),
                ConversationPreviewMessage("message_003", "system", "Siguiente paso: instancias reales.", 12, 0, 0, false, "none", ""),
            },
            ["conversationFrame"] = 0,
            ["timelineFrameJsonKey"] = "conversationFrame",
            ["inputs"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "conversationType",
                    ["label"] = "Chat type",
                    ["jsonKey"] = "conversationType",
                    ["kind"] = "option",
                    ["defaultValue"] = "individual",
                    ["options"] = new JsonArray
                    {
                        new JsonObject { ["value"] = "individual", ["label"] = "Individual" },
                        new JsonObject { ["value"] = "group", ["label"] = "Group" },
                    },
                    ["groupId"] = "participants",
                    ["groupLabel"] = "Participants",
                    ["groupOrder"] = 10,
                },
                new JsonObject { ["id"] = "actor", ["label"] = "Actor", ["jsonKey"] = "actorId", ["kind"] = "recordReference", ["defaultValue"] = "", ["tableId"] = "actors", ["resolvedJsonKey"] = "actor" },
                AnimatableRuntimeField(new JsonObject { ["id"] = "headerSubtitle", ["label"] = "Header subtitle", ["jsonKey"] = "headerSubtitle", ["kind"] = "text", ["defaultValue"] = "online" }, "hold", "writeOn"),
                new JsonObject { ["id"] = "conversationFrame", ["label"] = "Timeline frame", ["jsonKey"] = "conversationFrame", ["kind"] = "number", ["defaultValue"] = "0", ["minimum"] = 0, ["maximum"] = 100000, ["increment"] = 1, ["unit"] = "frames", ["source"] = "calculated" },
                new JsonObject { ["id"] = "bubbleReveal", ["label"] = "Outgoing bubble reveal", ["jsonKey"] = "bubbleRevealMode", ["kind"] = "option", ["defaultValue"] = "afterWriteOn", ["options"] = new JsonArray { new JsonObject { ["value"] = "duringWriteOn", ["label"] = "During write-on" }, new JsonObject { ["value"] = "afterWriteOn", ["label"] = "After write-on" } }, ["groupId"] = "timing", ["groupLabel"] = "Timing", ["groupOrder"] = 20 },
                new JsonObject { ["id"] = "incomingReveal", ["label"] = "Incoming reveal", ["jsonKey"] = "incomingRevealMode", ["kind"] = "option", ["defaultValue"] = "typingIndicator", ["options"] = new JsonArray { new JsonObject { ["value"] = "instant", ["label"] = "Instant" }, new JsonObject { ["value"] = "writeOn", ["label"] = "Write-on" }, new JsonObject { ["value"] = "typingIndicator", ["label"] = "Typing indicator" } }, ["groupId"] = "timing", ["groupLabel"] = "Timing", ["groupOrder"] = 20 },
                new JsonObject { ["id"] = "textInput", ["label"] = "Text input while writing", ["jsonKey"] = "textInputVisible", ["kind"] = "boolean", ["defaultValue"] = "true", ["groupId"] = "timing", ["groupLabel"] = "Timing", ["groupOrder"] = 20 },
                new JsonObject { ["id"] = "keyboard", ["label"] = "Keyboard while writing", ["jsonKey"] = "keyboardVisible", ["kind"] = "boolean", ["defaultValue"] = "true", ["groupId"] = "timing", ["groupLabel"] = "Timing", ["groupOrder"] = 20 },
                new JsonObject { ["id"] = "typingIndicatorText", ["label"] = "Typing indicator text", ["jsonKey"] = "typingIndicatorText", ["kind"] = "text", ["defaultValue"] = "•••", ["groupId"] = "timing", ["groupLabel"] = "Timing", ["groupOrder"] = 20 },
                new JsonObject { ["id"] = "typingIndicatorSize", ["label"] = "Typing indicator size", ["jsonKey"] = "typingIndicatorSizeToken", ["kind"] = "themeToken", ["defaultValue"] = "theme.typography.sizes.m", ["options"] = new JsonArray { new JsonObject { ["value"] = "theme.typography.sizes.xs", ["label"] = "typography.sizes.xs" }, new JsonObject { ["value"] = "theme.typography.sizes.s", ["label"] = "typography.sizes.s" }, new JsonObject { ["value"] = "theme.typography.sizes.m", ["label"] = "typography.sizes.m" }, new JsonObject { ["value"] = "theme.typography.sizes.l", ["label"] = "typography.sizes.l" }, new JsonObject { ["value"] = "theme.typography.sizes.xl", ["label"] = "typography.sizes.xl" } }, ["groupId"] = "timing", ["groupLabel"] = "Timing", ["groupOrder"] = 20 },
                new JsonObject { ["id"] = "typingIndicatorAnimation", ["label"] = "Typing indicator animation", ["jsonKey"] = "typingIndicatorAnimation", ["kind"] = "option", ["defaultValue"] = "pulsating", ["options"] = new JsonArray { new JsonObject { ["value"] = "none", ["label"] = "None" }, new JsonObject { ["value"] = "pulsating", ["label"] = "Pulsating" }, new JsonObject { ["value"] = "wave", ["label"] = "Wave" } }, ["groupId"] = "timing", ["groupLabel"] = "Timing", ["groupOrder"] = 20 },
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
                    ["itemPresentation"] = new JsonObject
                    {
                        ["subtitleFieldIds"] = new JsonArray { "direction", "text" },
                        ["subtitleMaxCharacters"] = 72,
                        ["iconFieldId"] = "mediaType",
                        ["fallbackIcon"] = "message",
                        ["iconValueMap"] = new JsonObject
                        {
                            ["image"] = "image",
                            ["video"] = "video",
                            ["audio"] = "audio",
                        },
                    },
                    ["animationTimeline"] = new JsonObject
                    {
                        ["sequence"] = "serial",
                        ["preDurationFieldIds"] = new JsonArray { "delay" },
                        ["postDurationFieldIds"] = new JsonArray { "postWriteOnHold" },
                    },
                    ["fields"] = ConversationPreviewMessageFields(),
                    ["itemActions"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "playVideo",
                            ["label"] = "Play video",
                            ["playInputId"] = "isPlaying",
                            ["durationInputId"] = "playDurationFrames",
                            ["timeJsonKey"] = "playbackFrame",
                            ["timeUnit"] = "frames",
                            ["prewarmFrames"] = false,
                            ["extendsModuleDuration"] = true,
                            ["durationEnabledInputId"] = "isPlaying",
                            ["visibleWhenItemJsonKey"] = "mediaType",
                            ["visibleWhenItemValues"] = new JsonArray { "video" },
                        },
                        new JsonObject
                        {
                            ["id"] = "playAudio",
                            ["label"] = "Play audio",
                            ["playInputId"] = "isPlaying",
                            ["durationInputId"] = "playDurationFrames",
                            ["timeJsonKey"] = "playbackFrame",
                            ["timeUnit"] = "frames",
                            ["prewarmFrames"] = false,
                            ["extendsModuleDuration"] = true,
                            ["durationEnabledInputId"] = "isPlaying",
                            ["visibleWhenItemJsonKey"] = "mediaType",
                            ["visibleWhenItemValues"] = new JsonArray { "audio" },
                        },
                        new JsonObject
                        {
                            ["id"] = "fullScreen",
                            ["label"] = "Full screen",
                            ["playInputId"] = "fullScreenTransition",
                            ["targetInputId"] = "isFullScreen",
                            ["targetMode"] = "toggle",
                            ["durationSeconds"] = 0.3,
                            ["timeJsonKey"] = "motionElapsedMs",
                            ["timeUnit"] = "milliseconds",
                            ["prewarmFrames"] = false,
                            ["visibleWhenItemJsonKey"] = "mediaType",
                            ["visibleWhenItemValues"] = new JsonArray { "video" },
                        },
                    },
                },
            },
            ["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "playConversation",
                    ["label"] = "Play messages",
                    ["playInputId"] = "conversationPlayback",
                    ["durationOwnerTimeline"] = true,
                    ["durationBaseFrames"] = 1,
                    ["definesModuleDuration"] = true,
                    ["timeJsonKey"] = "conversationFrame",
                    ["timeUnit"] = "frames",
                    ["prewarmFrames"] = false,
                },
            },
        };
        NormalizePreviewActionCompletion(preview);
        return preview;
    }

    private static JsonObject HeaderButtonCollection(string jsonKey, string label) => new()
    {
        ["id"] = jsonKey,
        ["label"] = label,
        ["jsonKey"] = jsonKey,
        ["itemLabel"] = "Button",
        ["sourceCollectionJsonKey"] = jsonKey,
        ["fields"] = IconRowButtonFields(),
        ["itemActions"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "push",
                ["label"] = "Push",
                ["playInputId"] = "pushTrigger",
                ["durationThemeToken"] = "theme.motion.buttonPushedDurationMs",
                ["timeJsonKey"] = "pushElapsedMs",
                ["timeUnit"] = "milliseconds",
                ["prewarmFrames"] = false,
            },
        },
    };

    private static JsonObject ConversationPreviewMessage(
        string id,
        string direction,
        string text,
        int delayAfterPreviousFrames,
        int writeOnDurationFrames,
        int postWriteOnHoldFrames,
        bool statusVisible,
        string statusState,
        string statusText)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["actorId"] = "",
            ["direction"] = direction,
            ["text"] = text,
            ["delayAfterPreviousFrames"] = delayAfterPreviousFrames,
            ["writeOnTiming"] = new JsonObject
            {
                ["mode"] = "fixed",
                ["fixedFrames"] = writeOnDurationFrames,
                ["paceToken"] = "theme.motion.naturalPace.normal",
            },
            ["postWriteOnHoldFrames"] = postWriteOnHoldFrames,
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
            ["playbackMode"] = "once",
            ["playDurationFrames"] = 72,
            ["playbackFrame"] = 0,
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
            new JsonObject { ["id"] = "actor", ["label"] = "Actor", ["jsonKey"] = "actorId", ["kind"] = "recordReference", ["defaultValue"] = "", ["tableId"] = "actors", ["resolvedJsonKey"] = "actor" },
            new JsonObject { ["id"] = "direction", ["label"] = "Direction", ["jsonKey"] = "direction", ["kind"] = "option", ["defaultValue"] = "incoming", ["options"] = new JsonArray { new JsonObject { ["value"] = "incoming", ["label"] = "Incoming" }, new JsonObject { ["value"] = "outgoing", ["label"] = "Outgoing" }, new JsonObject { ["value"] = "system", ["label"] = "System" } } },
            WithAnimationCompletion(
                TargetRelativeAnimatableRuntimeField(new JsonObject { ["id"] = "text", ["label"] = "Text", ["jsonKey"] = "text", ["kind"] = "multilineText", ["defaultValue"] = "" }, "hold", "writeOn"),
                "writeOn"),
            new JsonObject { ["id"] = "delay", ["label"] = "Delay", ["jsonKey"] = "delayAfterPreviousFrames", ["kind"] = "number", ["defaultValue"] = "0", ["minimum"] = 0, ["maximum"] = 100000, ["increment"] = 1, ["unit"] = "frames" },
            new JsonObject
            {
                ["id"] = "writeOn",
                ["label"] = "Write-on timing",
                ["jsonKey"] = "writeOnTiming",
                ["kind"] = "behaviorTiming",
                ["valueKind"] = "BehaviorTiming",
                ["defaultValue"] = "{\"mode\":\"fixed\",\"fixedFrames\":30,\"paceToken\":\"theme.motion.naturalPace.normal\"}",
                ["options"] = new JsonArray
                {
                    new JsonObject { ["value"] = "theme.motion.naturalPace.verySlow", ["label"] = "Very slow" },
                    new JsonObject { ["value"] = "theme.motion.naturalPace.slow", ["label"] = "Slow" },
                    new JsonObject { ["value"] = "theme.motion.naturalPace.normal", ["label"] = "Normal" },
                    new JsonObject { ["value"] = "theme.motion.naturalPace.fast", ["label"] = "Fast" },
                    new JsonObject { ["value"] = "theme.motion.naturalPace.veryFast", ["label"] = "Very fast" },
                },
                ["naturalTiming"] = new JsonObject
                {
                    ["sourceFieldId"] = "text",
                    ["unit"] = "grapheme",
                    ["baseFramesPerUnit"] = 7,
                },
            },
            new JsonObject { ["id"] = "postWriteOnHold", ["label"] = "Post write-on hold", ["jsonKey"] = "postWriteOnHoldFrames", ["kind"] = "number", ["defaultValue"] = "0", ["minimum"] = 0, ["maximum"] = 100000, ["increment"] = 1, ["unit"] = "frames" },
            DoesNotExtendOwnerDuration(TargetDependentAnimatableRuntimeField(new JsonObject { ["id"] = "statusVisible", ["label"] = "Show delivery status", ["jsonKey"] = "statusVisible", ["kind"] = "boolean", ["defaultValue"] = "false" }, "text", "hold")),
            DoesNotExtendOwnerDuration(TargetDependentAnimatableRuntimeField(new JsonObject { ["id"] = "status", ["label"] = "Status", ["jsonKey"] = "statusState", ["kind"] = "option", ["defaultValue"] = "none", ["options"] = new JsonArray { new JsonObject { ["value"] = "none", ["label"] = "None" }, new JsonObject { ["value"] = "sent", ["label"] = "Sent" }, new JsonObject { ["value"] = "delivered", ["label"] = "Delivered" }, new JsonObject { ["value"] = "read", ["label"] = "Read" } } }, "text", "hold")),
            DoesNotExtendOwnerDuration(TargetDependentAnimatableRuntimeField(new JsonObject { ["id"] = "statusText", ["label"] = "Status text", ["jsonKey"] = "statusText", ["kind"] = "text", ["defaultValue"] = "" }, "text", "hold", "writeOn")),
            new JsonObject { ["id"] = "mediaType", ["label"] = "Attachment type", ["jsonKey"] = "mediaType", ["kind"] = "option", ["defaultValue"] = "none", ["options"] = new JsonArray { new JsonObject { ["value"] = "none", ["label"] = "None" }, new JsonObject { ["value"] = "image", ["label"] = "Image" }, new JsonObject { ["value"] = "video", ["label"] = "Video" }, new JsonObject { ["value"] = "audio", ["label"] = "Audio" } } },
            new JsonObject { ["id"] = "mediaSource", ["label"] = "Media source", ["jsonKey"] = "mediaSource", ["kind"] = "text", ["valueKind"] = "MediaFilePath", ["defaultValue"] = "", ["enabledWhenItemJsonKey"] = "mediaType", ["enabledWhenItemValues"] = new JsonArray { "image", "video", "audio" } },
            new JsonObject { ["id"] = "viewport", ["label"] = "Media viewport", ["jsonKey"] = "viewportSize", ["kind"] = "integerPair", ["defaultValue"] = "240|160", ["pairFirstLabel"] = "W", ["pairSecondLabel"] = "H" },
            new JsonObject { ["id"] = "mediaScale", ["label"] = "Media scale", ["jsonKey"] = "mediaScale", ["kind"] = "number", ["defaultValue"] = "1", ["minimum"] = 0.01, ["maximum"] = 100, ["increment"] = 0.01 },
            new JsonObject { ["id"] = "mediaOffset", ["label"] = "Media offset", ["jsonKey"] = "mediaOffset", ["kind"] = "integerPair", ["defaultValue"] = "0|0", ["pairFirstLabel"] = "X", ["pairSecondLabel"] = "Y" },
            TargetDependentAnimatableRuntimeField(new JsonObject { ["id"] = "isPlaying", ["label"] = "Playing", ["jsonKey"] = "isPlaying", ["kind"] = "boolean", ["defaultValue"] = "false" }, "text", "hold"),
            new JsonObject { ["id"] = "currentTime", ["label"] = "Current time", ["jsonKey"] = "currentTimeSeconds", ["kind"] = "number", ["defaultValue"] = "0", ["minimum"] = 0, ["maximum"] = 86400, ["increment"] = 0.01, ["unit"] = "s" },
            new JsonObject { ["id"] = "duration", ["label"] = "Source duration", ["jsonKey"] = "durationSeconds", ["kind"] = "number", ["defaultValue"] = "12", ["minimum"] = 1, ["maximum"] = 86400, ["increment"] = 0.01, ["enabledWhenItemJsonKey"] = "mediaType", ["enabledWhenItemValues"] = new JsonArray { "video", "audio" }, ["unit"] = "s" },
            new JsonObject { ["id"] = "playbackMode", ["label"] = "Playback mode", ["jsonKey"] = "playbackMode", ["kind"] = "option", ["defaultValue"] = "once", ["options"] = new JsonArray { new JsonObject { ["value"] = "once", ["label"] = "Play once" }, new JsonObject { ["value"] = "loop", ["label"] = "Loop" } }, ["enabledWhenItemJsonKey"] = "mediaType", ["enabledWhenItemValues"] = new JsonArray { "video", "audio" } },
            new JsonObject { ["id"] = "playDuration", ["label"] = "Playback duration", ["jsonKey"] = "playDurationFrames", ["kind"] = "number", ["defaultValue"] = "72", ["minimum"] = 1, ["maximum"] = 100000, ["increment"] = 1, ["enabledWhenItemJsonKey"] = "mediaType", ["enabledWhenItemValues"] = new JsonArray { "video", "audio" }, ["unit"] = "frames" },
            TargetDependentAnimatableRuntimeField(new JsonObject { ["id"] = "fullScreen", ["label"] = "Full screen", ["jsonKey"] = "isFullScreen", ["kind"] = "boolean", ["defaultValue"] = "false" }, "text", "hold"),
            new JsonObject { ["id"] = "fullScreenTransition", ["label"] = "Full-screen transition", ["jsonKey"] = "fullScreenTransition", ["kind"] = "boolean", ["defaultValue"] = "false" },
            new JsonObject { ["id"] = "fullframeOrientation", ["label"] = "Fullframe orientation", ["jsonKey"] = "fullframeOrientation", ["kind"] = "option", ["defaultValue"] = "portrait", ["options"] = new JsonArray { new JsonObject { ["value"] = "portrait", ["label"] = "Portrait" }, new JsonObject { ["value"] = "landscape", ["label"] = "Landscape" } } },
            new JsonObject { ["id"] = "controlsElapsed", ["label"] = "Controls elapsed ms", ["jsonKey"] = "controlsElapsedMs", ["kind"] = "number", ["defaultValue"] = "0", ["minimum"] = 0, ["maximum"] = 86400000, ["increment"] = 1, ["unit"] = "ms" },
        };
        ApplyConversationRuntimeGroups(fields);
        return fields;
    }

    private static JsonObject AnimatableRuntimeField(JsonObject field, params string[] interpolations)
    {
        field["animatable"] = true;
        field["animationInterpolations"] = new JsonArray(
            interpolations.Select((value) => (JsonNode?)JsonValue.Create(value)).ToArray());
        field["animationTimeline"] = new JsonObject
        {
            ["origin"] = new JsonObject { ["kind"] = "ownerStart" },
        };
        return field;
    }

    private static JsonObject TargetRelativeAnimatableRuntimeField(JsonObject field, params string[] interpolations)
    {
        AnimatableRuntimeField(field, interpolations);
        return field;
    }

    private static JsonObject TargetDependentAnimatableRuntimeField(
        JsonObject field,
        string sourceFieldId,
        params string[] interpolations)
    {
        TargetRelativeAnimatableRuntimeField(field, interpolations);
        ((JsonObject)field["animationTimeline"]!)["origin"] = new JsonObject
        {
            ["kind"] = "fieldCompletion",
            ["fieldId"] = sourceFieldId,
            ["offsetFrames"] = 0,
        };
        return field;
    }

    private static JsonObject WithAnimationCompletion(JsonObject field, string baseDurationFieldId)
    {
        ((JsonObject)field["animationTimeline"]!)["completion"] = new JsonObject
        {
            ["baseDurationFieldId"] = baseDurationFieldId,
            ["trackOverride"] = "lastEnabledKeyframe",
            ["minimumEnabledKeyframes"] = 2,
        };
        return field;
    }

    private static JsonObject DoesNotExtendOwnerDuration(JsonObject field)
    {
        ((JsonObject)field["animationTimeline"]!)["extendsOwnerDuration"] = false;
        return field;
    }

    private static void ApplyConversationRuntimeGroups(JsonArray fields)
    {
        SetRuntimeGroup(fields, ["actor", "direction", "text"], "message", "Message", 10);
        SetRuntimeGroup(fields, ["delay", "writeOn", "postWriteOnHold"], "timing", "Timing", 20);
        SetRuntimeGroup(fields, ["statusVisible", "status", "statusText"], "delivery", "Delivery", 30);
        SetRuntimeGroup(fields, ["mediaType", "mediaSource"], "attachment", "Attachment", 40);
        SetRuntimeGroup(fields, ["viewport", "mediaScale", "mediaOffset"], "attachment", "Attachment", 50, sectionLabel: "Frame");
        SetRuntimeGroup(fields, ["isPlaying", "currentTime", "duration", "playbackMode", "playDuration", "controlsElapsed"], "attachment", "Attachment", 60, sectionLabel: "Playback");
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
