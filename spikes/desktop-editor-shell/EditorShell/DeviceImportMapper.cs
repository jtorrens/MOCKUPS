using System;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DeviceImportMapper
{
    public static DeviceImportDraft ToDraft(DeviceCatalogDetails details)
    {
        var width = Math.Max(1, details.RenderWidth);
        var height = Math.Max(1, details.RenderHeight);
        var metricsJson = DeviceMetricRules.CreateMetricsJson(
            details.DesignWidth,
            details.DesignHeight,
            width,
            height,
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
