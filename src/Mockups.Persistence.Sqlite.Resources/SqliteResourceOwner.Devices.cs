using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteResourceOwner
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
            values.ModuleTransparency);
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
        _ = DeviceMetricRules.PreviewValues(metrics);
        var context = $"Device '{deviceId}' metrics_json";
        return fieldId switch
        {
            "device.metrics.canvas.size" => JsonPath.RequiredNumberPair(metrics, ["canvas", "width"], ["canvas", "height"], context),
            "device.metrics.screen.position" => JsonPath.RequiredNumberPair(metrics, ["screen", "x"], ["screen", "y"], context),
            "device.metrics.screen.size" => JsonPath.RequiredNumberPair(metrics, ["screen", "width"], ["screen", "height"], context),
            "device.metrics.cornerRadius" => JsonPath.RequiredNumberString(metrics, ["cornerRadius"], context),
            "device.metrics.safeArea.bottom" => JsonPath.RequiredNumberString(metrics, ["safeArea", "bottom"], context),
            "device.metrics.statusBar.height" => JsonPath.RequiredNumberString(metrics, ["statusBar", "height"], context),
            "device.metrics.moduleTransparency.enabled" => BooleanText.Format(
                JsonPath.RequiredBoolean(
                    JsonPath.RequiredObject(metrics, "moduleTransparency", context),
                    "enabled",
                    context)),
            "device.metrics.moduleTransparency.mode" => JsonPath.RequiredString(
                JsonPath.RequiredObject(metrics, "moduleTransparency", context),
                "mode",
                context),
            "device.metrics.moduleTransparency.paletteColor" => JsonPath.RequiredString(
                JsonPath.RequiredObject(metrics, "moduleTransparency", context),
                "paletteColor",
                context),
            "device.metrics.moduleTransparency.opacity" => JsonPath.RequiredNumberString(metrics, ["moduleTransparency", "opacity"], context),
            "device.metrics.moduleTransparency.fixedStart" => JsonPath.RequiredNumberString(metrics, ["moduleTransparency", "fixedStart"], context),
            "device.metrics.moduleTransparency.gradientHeight" => JsonPath.RequiredNumberString(metrics, ["moduleTransparency", "gradientHeight"], context),
            "device.metrics.moduleTransparency.variableOffset" => JsonPath.RequiredNumberString(metrics, ["moduleTransparency", "variableOffset"], context),
            _ => throw new InvalidOperationException($"Unknown device metrics field '{fieldId}'."),
        };
    }

    public IReadOnlyList<FieldOption> GetDeviceOptions(string projectId)
    {
        return _deviceRepository.GetOptions(projectId)
            .Select((option) => new FieldOption(option.Value, option.Label))
            .ToList();
    }

    internal IReadOnlyList<DeviceRecord> QueryDeviceRows(SqliteConnection connection)
    {
        return _deviceRepository.QueryAll(connection);
    }
}
