using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    public AppSettings GetAppSettings(string appId)
    {
        var record = _appModuleRepository.GetApp(appId);

        return new AppSettings(
            record.ProjectId,
            record.BundleKey,
            record.AppType,
            record.ConfigJson,
            record.MetadataJson);
    }

    public void UpdateAppField(
        string appId,
        string fieldId,
        string value)
    {
        using var connection = OpenConnection();
        if (fieldId.StartsWith(
                "app.wallpaper.",
                StringComparison.Ordinal))
        {
            if (_appModuleRepository.GetApp(connection, appId).AppType
                == "system")
            {
                throw new InvalidOperationException(
                    "System apps inherit Actor wallpaper and cannot own wallpaper fields.");
            }
            UpdateAppConfigField(connection, appId, fieldId, value);
            return;
        }

        if (fieldId.StartsWith("app.icon.", StringComparison.Ordinal)
            || fieldId == "app.note")
        {
            UpdateAppMetadataField(connection, appId, fieldId, value);
            return;
        }

        _appModuleRepository.UpdateAppDirectField(
            connection,
            appId,
            fieldId,
            value);
    }

    public string GetAppConfigFieldValue(string appId, string fieldId)
    {
        var settings = GetAppSettings(appId);
        var config = ParseJsonObject(settings.ConfigJson);
        var context = $"App '{appId}' config_json";
        var lightWallpaperColor = JsonString(
            config,
            ["modes", "light", "wallpaper", "color"]);
        var darkWallpaperColor = JsonString(
            config,
            ["modes", "dark", "wallpaper", "color"]);
        return fieldId switch
        {
            "app.wallpaper.kind" =>
                JsonString(config, ["wallpaper", "kind"]),
            "app.wallpaper.opacity" =>
                JsonPath.RequiredNumberString(
                    config,
                    ["wallpaper", "opacity"],
                    context),
            "app.wallpaper.color" =>
                $"{lightWallpaperColor}|{darkWallpaperColor}",
            "app.wallpaper.images.light.filePath" =>
                JsonString(
                    config,
                    ["wallpaper", "images", "light", "filePath"]),
            "app.wallpaper.images.dark.filePath" =>
                JsonString(
                    config,
                    ["wallpaper", "images", "dark", "filePath"]),
            _ => throw new InvalidOperationException(
                $"Unknown app config field '{fieldId}'."),
        };
    }

    public string GetAppMetadataFieldValue(string appId, string fieldId)
    {
        var settings = GetAppSettings(appId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        var context = $"App '{appId}' metadata_json";
        return fieldId switch
        {
            "app.note" => JsonString(metadata, ["note"]),
            "app.icon.filePath" =>
                JsonString(metadata, ["icon", "filePath"]),
            "app.icon.scale" =>
                JsonPath.RequiredNumberString(
                    metadata,
                    ["icon", "scale"],
                    context),
            "app.icon.offset" =>
                JsonPath.RequiredNumberPair(
                    metadata,
                    ["icon", "offsetX"],
                    ["icon", "offsetY"],
                    context),
            _ => throw new InvalidOperationException(
                $"Unknown app metadata field '{fieldId}'."),
        };
    }

    private void UpdateAppConfigField(
        SqliteConnection connection,
        string appId,
        string fieldId,
        string value)
    {
        var config = ParseJsonObject(
            _appModuleRepository.GetApp(connection, appId).ConfigJson);
        switch (fieldId)
        {
            case "app.wallpaper.kind":
                SetJsonValue(
                    config,
                    ["wallpaper", "kind"],
                    JsonValue.Create(value)!);
                break;
            case "app.wallpaper.opacity":
                SetJsonValue(
                    config,
                    ["wallpaper", "opacity"],
                    NumberNode(value));
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
                SetJsonValue(
                    config,
                    ["wallpaper", "images", "light", "filePath"],
                    JsonValue.Create(value)!);
                break;
            case "app.wallpaper.images.dark.filePath":
                SetJsonValue(
                    config,
                    ["wallpaper", "images", "dark", "filePath"],
                    JsonValue.Create(value)!);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown app config field '{fieldId}'.");
        }

        _appModuleRepository.UpdateAppConfig(
            connection,
            appId,
            config.ToJsonString());
    }

    private void UpdateAppMetadataField(
        SqliteConnection connection,
        string appId,
        string fieldId,
        string value)
    {
        var metadata = ParseJsonObject(
            _appModuleRepository.GetApp(connection, appId).MetadataJson);
        switch (fieldId)
        {
            case "app.note":
                SetJsonValue(
                    metadata,
                    ["note"],
                    JsonValue.Create(value)!);
                break;
            case "app.icon.filePath":
                SetJsonValue(
                    metadata,
                    ["icon", "filePath"],
                    JsonValue.Create(value)!);
                break;
            case "app.icon.scale":
                SetJsonValue(
                    metadata,
                    ["icon", "scale"],
                    NumberNode(value));
                break;
            case "app.icon.offset":
                SetPair(
                    metadata,
                    value,
                    ["icon", "offsetX"],
                    ["icon", "offsetY"]);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown app metadata field '{fieldId}'.");
        }

        _appModuleRepository.UpdateAppMetadata(
            connection,
            appId,
            metadata.ToJsonString());
    }
}
