using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record DevicePreviewMetricValues(
    double CanvasWidth,
    double CanvasHeight,
    double ScreenX,
    double ScreenY,
    double ScreenWidth,
    double ScreenHeight,
    double CornerRadius,
    double StatusBarHeight,
    double SafeAreaBottom,
    double ScaleToPixels);

internal static class DeviceMetricRules
{
    public static string CreateMetricsJson(
        int width,
        int height,
        double scale,
        bool includeDynamicIsland,
        double? cornerRadius = null,
        string? source = null)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        scale = scale > 0 ? scale : GuessScale(width, height, "");

        var statusBarHeight = StatusBarHeight(height);
        var bottomInset = BottomInset(height);
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
            ["cornerRadius"] = cornerRadius ?? CornerRadius(width),
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

        if (!string.IsNullOrWhiteSpace(source))
        {
            root["source"] = source;
        }

        return root.ToJsonString();
    }

    public static DevicePreviewMetricValues PreviewValues(JsonObject metrics)
    {
        var canvasWidth = JsonPath.NumberAt(metrics, ["canvas", "width"], JsonPath.NumberAt(metrics, ["renderSize", "width"], 1080));
        var canvasHeight = JsonPath.NumberAt(metrics, ["canvas", "height"], JsonPath.NumberAt(metrics, ["renderSize", "height"], 1920));
        var screenX = JsonPath.NumberAt(metrics, ["screen", "x"], 0);
        var screenY = JsonPath.NumberAt(metrics, ["screen", "y"], 0);
        var screenWidth = JsonPath.NumberAt(metrics, ["screen", "width"], canvasWidth);
        var screenHeight = JsonPath.NumberAt(metrics, ["screen", "height"], canvasHeight);
        var cornerRadius = JsonPath.NumberAt(metrics, ["cornerRadius"], 0);
        var statusBarHeight = JsonPath.NumberAt(metrics, ["statusBar", "height"], JsonPath.NumberAt(metrics, ["safeArea", "top"], 0));
        var safeAreaBottom = JsonPath.NumberAt(metrics, ["safeArea", "bottom"], 0);
        var scaleToPixels = ResolveScaleToPixels(metrics, canvasWidth);

        return new DevicePreviewMetricValues(
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

    public static double GuessScale(int width, int height, string osFamily)
    {
        if (osFamily.Equals("ios", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(width, height) >= 2200 ? 3 : 2;
        }

        return Math.Max(width, height) >= 2400 ? 3 : 2;
    }

    public static double GuessScaleFromText(IReadOnlyList<string> values, string osFamily, int width, int height)
    {
        var ppiText = values.FirstOrDefault((value) => value.Contains("ppi", StringComparison.OrdinalIgnoreCase));
        if (ppiText is not null)
        {
            var match = Regex.Match(ppiText, @"(?<ppi>\d{3,4})\s*ppi", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups["ppi"].Value, out var ppi))
            {
                if (ppi >= 430) return 3;
                if (ppi >= 300) return 2;
            }
        }

        return GuessScale(width, height, osFamily);
    }

    private static double ResolveScaleToPixels(JsonObject metrics, double canvasWidth)
    {
        var scaleToPixels = JsonPath.NumberAt(metrics, ["scaleToPixels"], 0);
        if (scaleToPixels > 0) return scaleToPixels;

        var renderWidth = JsonPath.NumberAt(metrics, ["renderSize", "width"], canvasWidth);
        var designWidth = JsonPath.NumberAt(metrics, ["designSpace", "width"], 0);
        return designWidth > 0 ? renderWidth / designWidth : JsonPath.NumberAt(metrics, ["pixelRatio"], 1);
    }

    private static int StatusBarHeight(int height)
    {
        return (int)Math.Round(height * 0.063);
    }

    private static int BottomInset(int height)
    {
        return (int)Math.Round(height * 0.0365);
    }

    private static int CornerRadius(int width)
    {
        return (int)Math.Round(width * 0.128);
    }
}
