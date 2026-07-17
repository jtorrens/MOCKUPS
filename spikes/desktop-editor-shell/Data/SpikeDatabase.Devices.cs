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
