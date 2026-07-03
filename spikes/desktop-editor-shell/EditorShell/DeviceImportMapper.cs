using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DeviceImportMapper
{
    public static DeviceImportDraft ToDraft(DeviceCatalogDetails details)
    {
        var width = Math.Max(1, details.RenderWidth);
        var height = Math.Max(1, details.RenderHeight);
        var scale = details.ScaleToPixels > 0 ? details.ScaleToPixels : GuessScale(width, height, details.OsFamily);
        var statusBarHeight = (int)Math.Round(height * 0.063);
        var bottomInset = (int)Math.Round(height * 0.0365);

        var metrics = new JsonObject
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
            ["cornerRadius"] = 0,
            ["pixelRatio"] = scale,
            ["defaultScreenScale"] = 1,
            ["source"] = details.Source,
        };

        return new DeviceImportDraft(
            details.Name,
            details.Manufacturer,
            details.Model,
            details.OsFamily,
            metrics.ToJsonString());
    }

    private static double GuessScale(int width, int height, string osFamily)
    {
        if (osFamily.Equals("ios", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(width, height) >= 2200 ? 3 : 2;
        }

        return Math.Max(width, height) >= 2400 ? 3 : 2;
    }
}
