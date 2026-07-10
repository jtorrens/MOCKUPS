using System;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DeviceImportMapper
{
    public static DeviceImportDraft ToDraft(DeviceCatalogDetails details)
    {
        var width = Math.Max(1, details.RenderWidth);
        var height = Math.Max(1, details.RenderHeight);
        var scale = details.ScaleToPixels > 0 ? details.ScaleToPixels : DeviceMetricRules.GuessScale(width, height, details.OsFamily);
        var metricsJson = DeviceMetricRules.CreateMetricsJson(
            width,
            height,
            scale,
            includeDynamicIsland: false,
            cornerRadius: 0,
            source: details.Source);

        return new DeviceImportDraft(
            details.Name,
            details.Manufacturer,
            details.Model,
            details.OsFamily,
            metricsJson);
    }
}
