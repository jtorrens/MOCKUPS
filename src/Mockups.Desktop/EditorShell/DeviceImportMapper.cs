using System;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DeviceImportMapper
{
    public static DeviceImportDraft ToDraft(DeviceCatalogDetails details)
    {
        var width = Math.Max(1, details.RenderWidth);
        var height = Math.Max(1, details.RenderHeight);
        var profile = VisualGuideProfileFor(details);
        var metricsJson = DeviceMetricRules.CreateMetricsJson(
            details.DesignWidth,
            details.DesignHeight,
            width,
            height,
            includeDynamicIsland: false,
            cornerRadius: width * profile.CornerRadiusCoefficient,
            cornerRadiusCoefficient: profile.CornerRadiusCoefficient,
            designSafeMarginCoefficient: profile.DesignSafeMarginCoefficient,
            source: details.Source);

        return new DeviceImportDraft(
            details.Name,
            details.Manufacturer,
            details.Model,
            details.OsFamily,
            metricsJson);
    }

    public static string DescribeVisualGuideProfile(DeviceCatalogDetails details)
    {
        var profile = VisualGuideProfileFor(details);
        return $"{profile.Name} frame {profile.CornerRadiusCoefficient:0.000} · design margin {profile.DesignSafeMarginCoefficient:0.000}";
    }

    private static DeviceVisualGuideProfile VisualGuideProfileFor(DeviceCatalogDetails details)
    {
        var identity = SearchText.Normalize($"{details.Manufacturer} {details.Model}");
        if (identity.Contains("samsung") && identity.Contains("ultra"))
        {
            return new DeviceVisualGuideProfile("Sharp", 0, 0.040);
        }

        if (identity.Contains("iphone x") || identity.Contains("iphone 11") || identity.Contains("curved"))
        {
            return new DeviceVisualGuideProfile("Aggressive curve", 0.052, 0.075);
        }

        if (identity.Contains("apple") || identity.Contains("iphone"))
        {
            return new DeviceVisualGuideProfile("Standard flagship", 0.046, 0.065);
        }

        return new DeviceVisualGuideProfile("Medium curve", 0.035, 0.055);
    }

    private sealed record DeviceVisualGuideProfile(
        string Name,
        double CornerRadiusCoefficient,
        double DesignSafeMarginCoefficient);
}
