using Microsoft.Data.Sqlite;
using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public DevicePreviewMetrics GetDevicePreviewMetrics(string deviceId)
    {
        var settings = GetDeviceSettings(deviceId);
        var metrics = ParseJsonObject(settings.MetricsJson);
        var canvasWidth = JsonNumberDouble(metrics, ["canvas", "width"], JsonNumberDouble(metrics, ["renderSize", "width"], 1080));
        var canvasHeight = JsonNumberDouble(metrics, ["canvas", "height"], JsonNumberDouble(metrics, ["renderSize", "height"], 1920));
        var screenX = JsonNumberDouble(metrics, ["screen", "x"], 0);
        var screenY = JsonNumberDouble(metrics, ["screen", "y"], 0);
        var screenWidth = JsonNumberDouble(metrics, ["screen", "width"], canvasWidth);
        var screenHeight = JsonNumberDouble(metrics, ["screen", "height"], canvasHeight);
        var cornerRadius = JsonNumberDouble(metrics, ["cornerRadius"], 0);
        var statusBarHeight = JsonNumberDouble(metrics, ["statusBar", "height"], JsonNumberDouble(metrics, ["safeArea", "top"], 0));
        var safeAreaBottom = JsonNumberDouble(metrics, ["safeArea", "bottom"], 0);
        var scaleToPixels = JsonNumberDouble(metrics, ["scaleToPixels"], 0);
        if (scaleToPixels <= 0)
        {
            var renderWidth = JsonNumberDouble(metrics, ["renderSize", "width"], canvasWidth);
            var designWidth = JsonNumberDouble(metrics, ["designSpace", "width"], 0);
            scaleToPixels = designWidth > 0 ? renderWidth / designWidth : JsonNumberDouble(metrics, ["pixelRatio"], 1);
        }

        return new DevicePreviewMetrics(
            settings.Name,
            canvasWidth,
            canvasHeight,
            screenX,
            screenY,
            screenWidth,
            screenHeight,
            cornerRadius,
            statusBarHeight,
            safeAreaBottom,
            scaleToPixels);
    }

    public DeviceSettings GetDeviceSettings(string deviceId)
    {
        using var connection = OpenConnection();
        return GetDeviceSettings(connection, deviceId);
    }

    private static DeviceSettings GetDeviceSettings(SqliteConnection connection, string deviceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, manufacturer, model, os_family, metrics_json FROM devices WHERE id = $id";
        command.Parameters.AddWithValue("$id", deviceId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing device '{deviceId}'.");
        }

        return new DeviceSettings(
            reader.GetString(0),
            ReadString(reader, 1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            reader.GetString(4));
    }

    public void UpdateDeviceField(string deviceId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "device.manufacturer":
                Execute(connection, "UPDATE devices SET manufacturer = $value WHERE id = $id", ("$id", deviceId), ("$value", value));
                return;
            case "device.model":
                Execute(connection, "UPDATE devices SET model = $value WHERE id = $id", ("$id", deviceId), ("$value", value));
                return;
            case "device.osFamily":
                Execute(connection, "UPDATE devices SET os_family = $value WHERE id = $id", ("$id", deviceId), ("$value", value));
                return;
        }

        if (!fieldId.StartsWith("device.metrics.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unknown device field '{fieldId}'.");
        }

        var settings = GetDeviceSettings(connection, deviceId);
        var metrics = ParseJsonObject(settings.MetricsJson);
        switch (fieldId)
        {
            case "device.metrics.designSpace.size":
                SetPair(metrics, value, ["designSpace", "width"], ["designSpace", "height"]);
                break;
            case "device.metrics.renderSize":
                SetPair(metrics, value, ["renderSize", "width"], ["renderSize", "height"]);
                break;
            case "device.metrics.canvas.size":
                SetPair(metrics, value, ["canvas", "width"], ["canvas", "height"]);
                break;
            case "device.metrics.screen.position":
                SetPair(metrics, value, ["screen", "x"], ["screen", "y"]);
                break;
            case "device.metrics.screen.size":
                SetPair(metrics, value, ["screen", "width"], ["screen", "height"]);
                break;
            case "device.metrics.viewport.position":
                SetPair(metrics, value, ["viewport", "x"], ["viewport", "y"]);
                break;
            case "device.metrics.viewport.size":
                SetPair(metrics, value, ["viewport", "width"], ["viewport", "height"]);
                break;
            case "device.metrics.safeArea.vertical":
                SetPair(metrics, value, ["safeArea", "top"], ["safeArea", "bottom"]);
                break;
            case "device.metrics.safeArea.horizontal":
                SetPair(metrics, value, ["safeArea", "left"], ["safeArea", "right"]);
                break;
            case "device.metrics.statusBar.position":
                SetPair(metrics, value, ["statusBar", "x"], ["statusBar", "y"]);
                break;
            case "device.metrics.statusBar.size":
                SetPair(metrics, value, ["statusBar", "width"], ["statusBar", "height"]);
                break;
            case "device.metrics.dynamicIsland.position":
                SetPair(metrics, value, ["dynamicIsland", "x"], ["dynamicIsland", "y"]);
                break;
            case "device.metrics.dynamicIsland.size":
                SetPair(metrics, value, ["dynamicIsland", "width"], ["dynamicIsland", "height"]);
                break;
            case "device.metrics.scaleToPixels":
                SetJsonValue(metrics, ["scaleToPixels"], NumberNode(value));
                break;
            case "device.metrics.pixelRatio":
                SetJsonValue(metrics, ["pixelRatio"], NumberNode(value));
                break;
            case "device.metrics.defaultScreenScale":
                SetJsonValue(metrics, ["defaultScreenScale"], NumberNode(value));
                break;
            case "device.metrics.cornerRadius":
                SetJsonValue(metrics, ["cornerRadius"], NumberNode(value));
                break;
            default:
                throw new InvalidOperationException($"Unknown device metrics field '{fieldId}'.");
        }

        Execute(
            connection,
            "UPDATE devices SET metrics_json = $metricsJson WHERE id = $id",
            ("$id", deviceId),
            ("$metricsJson", metrics.ToJsonString()));
    }

    public string GetDeviceMetricFieldValue(string deviceId, string fieldId)
    {
        var settings = GetDeviceSettings(deviceId);
        return fieldId switch
        {
            "device.metrics.designSpace.size" => MetricPair(settings.MetricsJson, ["designSpace", "width"], ["designSpace", "height"]),
            "device.metrics.renderSize" => MetricPair(settings.MetricsJson, ["renderSize", "width"], ["renderSize", "height"]),
            "device.metrics.canvas.size" => MetricPair(settings.MetricsJson, ["canvas", "width"], ["canvas", "height"]),
            "device.metrics.screen.position" => MetricPair(settings.MetricsJson, ["screen", "x"], ["screen", "y"]),
            "device.metrics.screen.size" => MetricPair(settings.MetricsJson, ["screen", "width"], ["screen", "height"]),
            "device.metrics.viewport.position" => MetricPair(settings.MetricsJson, ["viewport", "x"], ["viewport", "y"]),
            "device.metrics.viewport.size" => MetricPair(settings.MetricsJson, ["viewport", "width"], ["viewport", "height"]),
            "device.metrics.safeArea.vertical" => MetricPair(settings.MetricsJson, ["safeArea", "top"], ["safeArea", "bottom"]),
            "device.metrics.safeArea.horizontal" => MetricPair(settings.MetricsJson, ["safeArea", "left"], ["safeArea", "right"]),
            "device.metrics.statusBar.position" => MetricPair(settings.MetricsJson, ["statusBar", "x"], ["statusBar", "y"]),
            "device.metrics.statusBar.size" => MetricPair(settings.MetricsJson, ["statusBar", "width"], ["statusBar", "height"]),
            "device.metrics.dynamicIsland.position" => MetricPair(settings.MetricsJson, ["dynamicIsland", "x"], ["dynamicIsland", "y"]),
            "device.metrics.dynamicIsland.size" => MetricPair(settings.MetricsJson, ["dynamicIsland", "width"], ["dynamicIsland", "height"]),
            "device.metrics.scaleToPixels" => JsonNumberString(ParseJsonObject(settings.MetricsJson), ["scaleToPixels"]),
            "device.metrics.pixelRatio" => JsonNumberString(ParseJsonObject(settings.MetricsJson), ["pixelRatio"]),
            "device.metrics.defaultScreenScale" => JsonNumberString(ParseJsonObject(settings.MetricsJson), ["defaultScreenScale"]),
            "device.metrics.cornerRadius" => JsonNumberString(ParseJsonObject(settings.MetricsJson), ["cornerRadius"]),
            _ => throw new InvalidOperationException($"Unknown device metrics field '{fieldId}'."),
        };
    }

    private static string DefaultDeviceMetricsJson(int width, int height, double scale)
    {
        return DeviceMetricsJson(width, height, scale, includeDynamicIsland: false);
    }

    private static string DeviceMetricsJson(int width, int height, double scale, bool includeDynamicIsland)
    {
        var statusBarHeight = (int)Math.Round(height * 0.063);
        var bottomInset = (int)Math.Round(height * 0.0365);
        var cornerRadius = (int)Math.Round(width * 0.128);
        var root = new JsonObject
        {
            ["designSpace"] = new JsonObject
            {
                ["width"] = (int)Math.Round(width / scale),
                ["height"] = (int)Math.Round(height / scale),
                ["unit"] = "logical",
            },
            ["renderSize"] = new JsonObject { ["width"] = width, ["height"] = height },
            ["scaleToPixels"] = scale,
            ["canvas"] = new JsonObject { ["width"] = width, ["height"] = height },
            ["screen"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["width"] = width, ["height"] = height },
            ["viewport"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["width"] = width, ["height"] = height },
            ["safeArea"] = new JsonObject { ["top"] = statusBarHeight, ["right"] = 0, ["bottom"] = bottomInset, ["left"] = 0 },
            ["statusBar"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["width"] = width, ["height"] = statusBarHeight },
            ["cornerRadius"] = cornerRadius,
            ["pixelRatio"] = scale,
            ["defaultScreenScale"] = 1,
        };

        if (includeDynamicIsland)
        {
            root["dynamicIsland"] = new JsonObject
            {
                ["x"] = 462,
                ["y"] = 33,
                ["width"] = 366,
                ["height"] = 111,
            };
        }

        return root.ToJsonString();
    }

    private static readonly DeviceSeedRow[] DeviceSeedRows =
    [
        new("device_iphone_15_pro", "iPhone 15 Pro", "Apple", "iPhone 15 Pro", "ios", DeviceMetricsJson(1179, 2556, 3, includeDynamicIsland: false)),
        new("device_iphone_generic", "iPhone 15 Pro Max", "Apple", "iPhone 15 Pro Max", "ios", DeviceMetricsJson(1290, 2796, 3, includeDynamicIsland: true)),
        new("device_iphone_14_pro", "iPhone 14 Pro", "Apple", "iPhone 14 Pro", "ios", DeviceMetricsJson(1179, 2556, 3, includeDynamicIsland: false)),
        new("device_samsung_galaxy_s24", "Samsung Galaxy S24", "Samsung", "Galaxy S24", "android", DeviceMetricsJson(1080, 2340, 3, includeDynamicIsland: false)),
        new("device_samsung_galaxy_s24_ultra", "Samsung Galaxy S24 Ultra", "Samsung", "Galaxy S24 Ultra", "android", DeviceMetricsJson(1440, 3120, 3, includeDynamicIsland: false)),
        new("device_google_pixel_8_pro", "Google Pixel 8 Pro", "Google", "Pixel 8 Pro", "android", DeviceMetricsJson(1344, 2992, 3, includeDynamicIsland: false)),
    ];
}
