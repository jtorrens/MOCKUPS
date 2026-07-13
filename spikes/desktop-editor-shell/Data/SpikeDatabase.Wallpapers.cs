using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private const string DefaultActorWallpaperPath = "wallpapers/image.16f45e146467c560c19b884f3017a4a2.png";

    private static void NormalizeAppWallpaperContracts(SqliteConnection connection)
    {
        NormalizeApplicationWallpapers(connection);
        NormalizeActorWallpapers(connection);
    }

    private static void NormalizeApplicationWallpapers(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, bundle_key, app_type, config_json FROM apps";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string ConfigJson)>();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var bundleKey = ReadString(reader, 1);
            var appType = ReadString(reader, 2);
            var original = ReadString(reader, 3);
            var config = ParseJsonObject(original);

            if (appType == "system")
            {
                config.Remove("wallpaper");
                RemoveModeWallpaper(config, "light");
                RemoveModeWallpaper(config, "dark");
            }
            else if (config["wallpaper"] is JsonObject wallpaper)
            {
                var retiredPath = JsonString(wallpaper, ["image", "filePath"]);
                var lightPath = JsonString(wallpaper, ["images", "light", "filePath"]);
                var darkPath = JsonString(wallpaper, ["images", "dark", "filePath"]);
                if (string.IsNullOrWhiteSpace(lightPath)) lightPath = retiredPath;
                if (string.IsNullOrWhiteSpace(darkPath))
                {
                    darkPath = bundleKey == "chat" && retiredPath == "wallpapers/fondo_chat.jpg"
                        ? "wallpapers/fondo_chat_dark.jpg"
                        : retiredPath;
                }

                wallpaper.Remove("image");
                wallpaper["kind"] = JsonString(wallpaper, ["kind"]) is { Length: > 0 } kind ? kind : "image";
                wallpaper["opacity"] ??= 1;
                wallpaper["images"] = WallpaperImages(lightPath, darkPath);
                EnsureModeWallpaperColor(config, "light", "gray_100");
                EnsureModeWallpaperColor(config, "dark", "gray_000");
            }

            var next = config.ToJsonString();
            if (next != original) updates.Add((id, next));
        }
        reader.Close();
        foreach (var update in updates)
        {
            Execute(connection, "UPDATE apps SET config_json = $configJson WHERE id = $id",
                ("$id", update.Id), ("$configJson", update.ConfigJson));
        }
    }

    private static void NormalizeActorWallpapers(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, metadata_json FROM actors";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string MetadataJson)>();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var original = ReadString(reader, 1);
            var metadata = ParseJsonObject(original);
            var wallpaper = metadata["wallpaper"] as JsonObject ?? new JsonObject();
            metadata["wallpaper"] = wallpaper;

            var retiredPath = JsonString(wallpaper, ["filePath"]);
            var lightPath = JsonString(wallpaper, ["images", "light", "filePath"]);
            var darkPath = JsonString(wallpaper, ["images", "dark", "filePath"]);
            if (string.IsNullOrWhiteSpace(lightPath)) lightPath = string.IsNullOrWhiteSpace(retiredPath) ? DefaultActorWallpaperPath : retiredPath;
            if (string.IsNullOrWhiteSpace(darkPath)) darkPath = string.IsNullOrWhiteSpace(retiredPath) ? DefaultActorWallpaperPath : retiredPath;

            wallpaper.Remove("filePath");
            wallpaper["kind"] = "image";
            wallpaper["opacity"] ??= 1;
            wallpaper["images"] = WallpaperImages(lightPath, darkPath);
            EnsureModeWallpaperColor(metadata, "light", "gray_100");
            EnsureModeWallpaperColor(metadata, "dark", "gray_000");

            var next = metadata.ToJsonString();
            if (next != original) updates.Add((id, next));
        }
        reader.Close();
        foreach (var update in updates)
        {
            Execute(connection, "UPDATE actors SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", update.Id), ("$metadataJson", update.MetadataJson));
        }
    }

    private static JsonObject WallpaperImages(string lightPath, string darkPath) => new()
    {
        ["light"] = new JsonObject { ["filePath"] = lightPath },
        ["dark"] = new JsonObject { ["filePath"] = darkPath },
    };

    private static void EnsureModeWallpaperColor(JsonObject config, string mode, string color)
    {
        if (string.IsNullOrWhiteSpace(JsonString(config, ["modes", mode, "wallpaper", "color"])))
        {
            SetJsonValue(config, ["modes", mode, "wallpaper", "color"], JsonValue.Create(color)!);
        }
    }

    private static void RemoveModeWallpaper(JsonObject config, string mode)
    {
        if (config["modes"] is JsonObject modes && modes[mode] is JsonObject modeObject)
        {
            modeObject.Remove("wallpaper");
        }
    }
}
