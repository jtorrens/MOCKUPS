using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

public sealed record DevicePreviewMetricValues(
    double CanvasWidth,
    double CanvasHeight,
    double ScreenX,
    double ScreenY,
    double ScreenWidth,
    double ScreenHeight,
    double CornerRadius,
    double CornerRadiusCoefficient,
    double DesignSafeMarginCoefficient,
    double StatusBarHeight,
    double SafeAreaBottom,
    DeviceModuleTransparencyOverride ModuleTransparency);

public static class DeviceMetricRules
{
    public static string CreateMetricsJson(
        int width,
        int height,
        double? cornerRadius = null,
        double? cornerRadiusCoefficient = null,
        double? designSafeMarginCoefficient = null)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Device Canvas dimensions must be positive.");
        }
        if (cornerRadius is < 0)
        {
            throw new InvalidOperationException("Device corner radius must be non-negative.");
        }
        ValidateCoefficient(cornerRadiusCoefficient, "frame.cornerRadiusCoefficient");
        ValidateCoefficient(designSafeMarginCoefficient, "designGuides.safeMarginCoefficient");
        var statusBarHeight = StatusBarHeight(height);
        var bottomInset = BottomInset(height);
        var root = new JsonObject
        {
            ["canvas"] = new JsonObject { ["width"] = width, ["height"] = height },
            ["screen"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["width"] = width, ["height"] = height },
            ["cornerRadius"] = cornerRadius ?? CornerRadius(width),
            ["safeArea"] = new JsonObject { ["bottom"] = bottomInset },
            ["statusBar"] = new JsonObject { ["height"] = statusBarHeight },
            ["moduleTransparency"] = new JsonObject
            {
                ["enabled"] = false,
                ["mode"] = "fixed",
                ["paletteColor"] = "gray_000",
                ["backgroundOpacity"] = 1,
                ["fixedStart"] = Math.Round(height * 0.5),
                ["minimumOpaqueExtent"] = Math.Round(height * 0.5),
                ["gradientHeight"] = Math.Max(1, Math.Round(height * 0.25)),
                ["variableOffset"] = 0,
            },
        };

        if (cornerRadiusCoefficient is > 0 and <= 0.5)
        {
            root["frame"] = new JsonObject
            {
                ["cornerRadiusCoefficient"] = cornerRadiusCoefficient.Value,
            };
        }

        if (designSafeMarginCoefficient is > 0 and <= 0.5)
        {
            root["designGuides"] = new JsonObject
            {
                ["safeMarginCoefficient"] = designSafeMarginCoefficient.Value,
            };
        }

        return root.ToJsonString();
    }

    public static DevicePreviewMetricValues PreviewValues(JsonObject metrics)
    {
        RequireExactKeys(
            metrics,
            ["canvas", "screen", "cornerRadius", "safeArea", "statusBar", "moduleTransparency"],
            ["frame", "designGuides"],
            "Device metrics");
        RequireExactKeys(JsonPath.RequiredObject(metrics, "canvas", "Device metrics"), ["width", "height"], [], "Device metrics.canvas");
        RequireExactKeys(JsonPath.RequiredObject(metrics, "screen", "Device metrics"), ["x", "y", "width", "height"], [], "Device metrics.screen");
        RequireExactKeys(JsonPath.RequiredObject(metrics, "safeArea", "Device metrics"), ["bottom"], [], "Device metrics.safeArea");
        RequireExactKeys(JsonPath.RequiredObject(metrics, "statusBar", "Device metrics"), ["height"], [], "Device metrics.statusBar");
        var moduleTransparency = JsonPath.RequiredObject(
            metrics,
            "moduleTransparency",
            "Device metrics");
        RequireExactKeys(
            moduleTransparency,
            ["enabled", "mode", "paletteColor", "backgroundOpacity", "fixedStart", "minimumOpaqueExtent", "gradientHeight", "variableOffset"],
            [],
            "Device metrics.moduleTransparency");
        if (JsonPath.OptionalObject(metrics, "frame", "Device metrics") is { } frame)
        {
            RequireExactKeys(frame, ["cornerRadiusCoefficient"], [], "Device metrics.frame");
        }
        if (JsonPath.OptionalObject(metrics, "designGuides", "Device metrics") is { } designGuides)
        {
            RequireExactKeys(designGuides, ["safeMarginCoefficient"], [], "Device metrics.designGuides");
        }

        var canvasWidth = RequiredPositiveNumber(metrics, ["canvas", "width"]);
        var canvasHeight = RequiredPositiveNumber(metrics, ["canvas", "height"]);
        var screenX = RequiredNumber(metrics, ["screen", "x"]);
        var screenY = RequiredNumber(metrics, ["screen", "y"]);
        var screenWidth = RequiredPositiveNumber(metrics, ["screen", "width"]);
        var screenHeight = RequiredPositiveNumber(metrics, ["screen", "height"]);
        var cornerRadius = RequiredNonNegativeNumber(metrics, ["cornerRadius"]);
        var cornerRadiusCoefficient = OptionalCoefficient(metrics, ["frame", "cornerRadiusCoefficient"]) ?? 0;
        var designSafeMarginCoefficient = OptionalCoefficient(metrics, ["designGuides", "safeMarginCoefficient"]) ?? 0;
        var statusBarHeight = RequiredNonNegativeNumber(metrics, ["statusBar", "height"]);
        var safeAreaBottom = RequiredNonNegativeNumber(metrics, ["safeArea", "bottom"]);
        var moduleTransparencyEnabled = JsonPath.RequiredBoolean(
            moduleTransparency,
            "enabled",
            "Device metrics.moduleTransparency");
        var moduleTransparencyMode = JsonPath.RequiredString(
            moduleTransparency,
            "mode",
            "Device metrics.moduleTransparency");
        if (moduleTransparencyMode is not ("fixed" or "variable"))
        {
            throw new InvalidOperationException(
                "Device metrics.moduleTransparency.mode must be 'fixed' or 'variable'.");
        }
        var moduleTransparencyPaletteColor = JsonPath.RequiredString(
            moduleTransparency,
            "paletteColor",
            "Device metrics.moduleTransparency");
        var moduleTransparencyBackgroundOpacity = RequiredNumber(
            metrics,
            ["moduleTransparency", "backgroundOpacity"]);
        if (moduleTransparencyBackgroundOpacity is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Device metrics.moduleTransparency.backgroundOpacity must be between 0 and 1.");
        }
        var moduleTransparencyFixedStart = RequiredNonNegativeNumber(
            metrics,
            ["moduleTransparency", "fixedStart"]);
        var moduleTransparencyMinimumOpaqueExtent = RequiredNonNegativeNumber(
            metrics,
            ["moduleTransparency", "minimumOpaqueExtent"]);
        var moduleTransparencyGradientHeight = RequiredPositiveNumber(
            metrics,
            ["moduleTransparency", "gradientHeight"]);
        var moduleTransparencyVariableOffset = RequiredNumber(
            metrics,
            ["moduleTransparency", "variableOffset"]);

        return new DevicePreviewMetricValues(
            canvasWidth,
            canvasHeight,
            screenX,
            screenY,
            screenWidth,
            screenHeight,
            cornerRadiusCoefficient > 0 ? canvasWidth * cornerRadiusCoefficient : cornerRadius,
            cornerRadiusCoefficient,
            designSafeMarginCoefficient,
            statusBarHeight,
            safeAreaBottom,
            new DeviceModuleTransparencyOverride(
                moduleTransparencyEnabled,
                moduleTransparencyMode,
                moduleTransparencyPaletteColor,
                moduleTransparencyBackgroundOpacity,
                moduleTransparencyFixedStart,
                moduleTransparencyMinimumOpaqueExtent,
                moduleTransparencyGradientHeight,
                moduleTransparencyVariableOffset));
    }

    private static void RequireExactKeys(
        JsonObject value,
        IReadOnlyList<string> required,
        IReadOnlyList<string> optional,
        string context)
    {
        var allowed = required.Concat(optional).ToHashSet(StringComparer.Ordinal);
        var missing = required.Where((key) => !value.ContainsKey(key)).ToArray();
        var unknown = value.Select((entry) => entry.Key).Where((key) => !allowed.Contains(key)).ToArray();
        if (missing.Length > 0 || unknown.Length > 0)
        {
            throw new InvalidOperationException(
                $"{context} must contain only its current properties; missing [{string.Join(", ", missing)}], unknown [{string.Join(", ", unknown)}].");
        }
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

        if (node is JsonValue value
            && double.TryParse(
                value.ToJsonString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number)
            && double.IsFinite(number))
        {
            return number;
        }

        throw new InvalidOperationException(
            $"Device metrics path '{PathLabel(path)}' must be numeric.");
    }

    private static double RequiredNonNegativeNumber(JsonObject metrics, IReadOnlyList<string> path)
    {
        var value = RequiredNumber(metrics, path);
        if (value >= 0) return value;

        throw new InvalidOperationException(
            $"Device metrics path '{PathLabel(path)}' must be non-negative.");
    }

    private static string PathLabel(IReadOnlyList<string> path)
    {
        return string.Join(".", path);
    }

    private static double? OptionalNonNegativeNumber(JsonObject metrics, IReadOnlyList<string> path)
    {
        var node = JsonPath.Get(metrics, path);
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value
            && double.TryParse(
                value.ToJsonString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number)
            && double.IsFinite(number)
            && number >= 0)
        {
            return number;
        }

        throw new InvalidOperationException(
            $"Device metrics optional path '{PathLabel(path)}' must be a non-negative JSON number when present.");
    }

    private static double? OptionalCoefficient(JsonObject metrics, IReadOnlyList<string> path)
    {
        var number = OptionalNonNegativeNumber(metrics, path);
        ValidateCoefficient(number, PathLabel(path));
        return number;
    }

    private static void ValidateCoefficient(double? number, string path)
    {
        if (number is null or (>= 0 and <= 0.5)) return;

        throw new InvalidOperationException(
            $"Device metrics optional path '{path}' must be between 0 and 0.5 when present.");
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
