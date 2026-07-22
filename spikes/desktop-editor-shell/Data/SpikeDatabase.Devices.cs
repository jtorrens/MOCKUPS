using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public DevicePreviewMetrics GetDevicePreviewMetrics(string deviceId)
    {
        var settings = GetDeviceSettings(deviceId);
        var metrics = ParseJsonObject(settings.MetricsJson);
        var values = DeviceMetricRules.PreviewValues(metrics);

        return new DevicePreviewMetrics(
            settings.Name,
            values.CanvasWidth,
            values.CanvasHeight,
            values.ScreenX,
            values.ScreenY,
            values.ScreenWidth,
            values.ScreenHeight,
            values.CornerRadius,
            values.CornerRadiusCoefficient,
            values.DesignSafeMarginCoefficient,
            values.StatusBarHeight,
            values.SafeAreaBottom,
            values.ScaleToPixels);
    }

    public DeviceSettings GetDeviceSettings(string deviceId)
    {
        return _deviceRepository.GetSettings(deviceId);
    }

    public void UpdateDeviceField(string deviceId, string fieldId, string value)
    {
        _deviceRepository.UpdateField(deviceId, fieldId, value);
    }

    public string GetDeviceMetricFieldValue(string deviceId, string fieldId)
    {
        var settings = GetDeviceSettings(deviceId);
        var metrics = ParseJsonObject(settings.MetricsJson);
        var context = $"Device '{deviceId}' metrics_json";
        return fieldId switch
        {
            "device.metrics.designSpace.size" => JsonPath.RequiredNumberPair(metrics, ["designSpace", "width"], ["designSpace", "height"], context),
            "device.metrics.renderSize" => JsonPath.RequiredNumberPair(metrics, ["renderSize", "width"], ["renderSize", "height"], context),
            "device.metrics.canvas.size" => JsonPath.RequiredNumberPair(metrics, ["canvas", "width"], ["canvas", "height"], context),
            "device.metrics.screen.position" => JsonPath.RequiredNumberPair(metrics, ["screen", "x"], ["screen", "y"], context),
            "device.metrics.screen.size" => JsonPath.RequiredNumberPair(metrics, ["screen", "width"], ["screen", "height"], context),
            "device.metrics.viewport.position" => JsonPath.RequiredNumberPair(metrics, ["viewport", "x"], ["viewport", "y"], context),
            "device.metrics.viewport.size" => JsonPath.RequiredNumberPair(metrics, ["viewport", "width"], ["viewport", "height"], context),
            "device.metrics.safeArea.vertical" => JsonPath.RequiredNumberPair(metrics, ["safeArea", "top"], ["safeArea", "bottom"], context),
            "device.metrics.safeArea.horizontal" => JsonPath.RequiredNumberPair(metrics, ["safeArea", "left"], ["safeArea", "right"], context),
            "device.metrics.statusBar.position" => JsonPath.RequiredNumberPair(metrics, ["statusBar", "x"], ["statusBar", "y"], context),
            "device.metrics.statusBar.size" => JsonPath.RequiredNumberPair(metrics, ["statusBar", "width"], ["statusBar", "height"], context),
            "device.metrics.dynamicIsland.position" => OptionalDynamicIslandPair(metrics, "x", "y", context),
            "device.metrics.dynamicIsland.size" => OptionalDynamicIslandPair(metrics, "width", "height", context),
            "device.metrics.scaleToPixels" => JsonPath.RequiredNumberString(metrics, ["scaleToPixels"], context),
            "device.metrics.pixelRatio" => JsonPath.RequiredNumberString(metrics, ["pixelRatio"], context),
            "device.metrics.defaultScreenScale" => JsonPath.RequiredNumberString(metrics, ["defaultScreenScale"], context),
            "device.metrics.cornerRadius" => JsonPath.RequiredNumberString(metrics, ["cornerRadius"], context),
            _ => throw new InvalidOperationException($"Unknown device metrics field '{fieldId}'."),
        };
    }

    private static string OptionalDynamicIslandPair(
        System.Text.Json.Nodes.JsonObject metrics,
        string firstKey,
        string secondKey,
        string context)
    {
        if (!metrics.TryGetPropertyValue("dynamicIsland", out var dynamicIslandNode))
        {
            return "0|0";
        }

        if (dynamicIslandNode is not System.Text.Json.Nodes.JsonObject)
        {
            throw new InvalidOperationException($"{context} optional path 'dynamicIsland' must contain an object when present.");
        }

        return JsonPath.RequiredNumberPair(
            metrics,
            ["dynamicIsland", firstKey],
            ["dynamicIsland", secondKey],
            context);
    }

    public IReadOnlyList<FieldOption> GetDeviceOptions(string projectId)
    {
        return _deviceRepository.GetOptions(projectId)
            .Select((option) => new FieldOption(option.Value, option.Label))
            .ToList();
    }

    private IReadOnlyList<DeviceRecord> QueryDeviceRows(SqliteConnection connection)
    {
        return _deviceRepository.QueryAll(connection);
    }
}
