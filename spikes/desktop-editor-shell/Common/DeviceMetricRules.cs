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
        var canvasWidth = RequiredPositiveNumber(metrics, ["canvas", "width"]);
        var canvasHeight = RequiredPositiveNumber(metrics, ["canvas", "height"]);
        var screenX = RequiredNumber(metrics, ["screen", "x"]);
        var screenY = RequiredNumber(metrics, ["screen", "y"]);
        var screenWidth = RequiredPositiveNumber(metrics, ["screen", "width"]);
        var screenHeight = RequiredPositiveNumber(metrics, ["screen", "height"]);
        var cornerRadius = RequiredNumber(metrics, ["cornerRadius"]);
        var statusBarHeight = RequiredNumber(metrics, ["statusBar", "height"]);
        var safeAreaBottom = RequiredNumber(metrics, ["safeArea", "bottom"]);
        var scaleToPixels = RequiredPositiveNumber(metrics, ["scaleToPixels"]);

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

    private static double RequiredPositiveNumber(JsonObject metrics, IReadOnlyList<string> path)
    {
        var value = RequiredNumber(metrics, path);
        if (value > 0) return value;

        throw new InvalidOperationException(
            $"Device metrics path '{PathLabel(path)}' must be greater than zero.");
    }

    private static double RequiredNumber(JsonObject metrics, IReadOnlyList<string> path)
    {
        var node = JsonPath.Get(metrics, path)
            ?? throw new InvalidOperationException(
                $"Device metrics path '{PathLabel(path)}' is required.");

        if (node is JsonValue value)
        {
            if (value.TryGetValue<double>(out var number) && double.IsFinite(number))
            {
                return number;
            }

            if (value.TryGetValue<string>(out var text)
                && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && double.IsFinite(parsed))
            {
                return parsed;
            }
        }

        throw new InvalidOperationException(
            $"Device metrics path '{PathLabel(path)}' must be numeric.");
    }

    private static string PathLabel(IReadOnlyList<string> path)
    {
        return string.Join(".", path);
    }

    private static double? FirstPositiveNumber(JsonObject metrics, params IReadOnlyList<string>[] paths)
    {
        foreach (var path in paths)
        {
            var value = OptionalPositiveNumber(metrics, path);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static double? OptionalPositiveNumber(JsonObject metrics, IReadOnlyList<string> path)
    {
        var node = JsonPath.Get(metrics, path);
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<double>(out var number) && double.IsFinite(number) && number > 0)
        {
            return number;
        }

        if (value.TryGetValue<string>(out var text)
            && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return null;
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
