using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class DeviceSettingsFieldContract
{
    public static IReadOnlyList<string> ScreenOverrideableFieldIds { get; } =
    [
        "device.metrics.moduleTransparency.enabled",
        "device.metrics.moduleTransparency.mode",
        "device.metrics.moduleTransparency.paletteColor",
        "device.metrics.moduleTransparency.backgroundOpacity",
        "device.metrics.moduleTransparency.fixedStart",
        "device.metrics.moduleTransparency.minimumOpaqueExtent",
        "device.metrics.moduleTransparency.gradientHeight",
        "device.metrics.moduleTransparency.variableOffset",
    ];

    public static IReadOnlyList<string> OverrideableFieldIds { get; } =
    [
        "device.manufacturer",
        "device.model",
        "device.osFamily",
        "device.metrics.canvas.size",
        "device.metrics.screen.position",
        "device.metrics.screen.size",
        "device.metrics.cornerRadius",
        "device.metrics.safeArea.bottom",
        "device.metrics.statusBar.height",
        "device.metrics.moduleTransparency.enabled",
        "device.metrics.moduleTransparency.mode",
        "device.metrics.moduleTransparency.paletteColor",
        "device.metrics.moduleTransparency.backgroundOpacity",
        "device.metrics.moduleTransparency.fixedStart",
        "device.metrics.moduleTransparency.minimumOpaqueExtent",
        "device.metrics.moduleTransparency.gradientHeight",
        "device.metrics.moduleTransparency.variableOffset",
    ];

    public static JsonObject ParseOverrides(
        string json,
        string owner)
        => ParseOverrides(
            json,
            owner,
            OverrideableFieldIds);

    public static JsonObject ParseScreenOverrides(
        string json,
        string owner)
        => ParseOverrides(
            json,
            owner,
            ScreenOverrideableFieldIds);

    private static JsonObject ParseOverrides(
        string json,
        string owner,
        IReadOnlyList<string> allowedFieldIds)
    {
        var overrides = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException(
                $"{owner} must be a JSON object.");
        var allowed = allowedFieldIds.ToHashSet(
            StringComparer.Ordinal);
        foreach (var entry in overrides)
        {
            if (!allowed.Contains(entry.Key))
            {
                throw new InvalidOperationException(
                    $"{owner} contains unknown Device field '{entry.Key}'.");
            }
            if (entry.Value is not JsonValue value
                || !value.TryGetValue<string>(out _))
            {
                throw new InvalidOperationException(
                    $"{owner} field '{entry.Key}' must be a JSON string.");
            }
        }
        return overrides;
    }

    public static DeviceSettings ApplyOverrides(
        DeviceSettings inherited,
        string overridesJson,
        string owner)
    {
        var overrides = ParseOverrides(overridesJson, owner);
        var effective = inherited;
        foreach (var fieldId in OverrideableFieldIds)
        {
            if (overrides[fieldId] is JsonValue value)
            {
                effective = ApplyField(
                    effective,
                    fieldId,
                    value.GetValue<string>());
            }
        }
        return effective;
    }

    public static DeviceSettings ApplyScreenOverrides(
        DeviceSettings inherited,
        string overridesJson,
        string owner)
    {
        var overrides = ParseScreenOverrides(overridesJson, owner);
        var effective = inherited;
        foreach (var fieldId in ScreenOverrideableFieldIds)
        {
            if (overrides[fieldId] is JsonValue value)
            {
                effective = ApplyField(
                    effective,
                    fieldId,
                    value.GetValue<string>());
            }
        }
        return effective;
    }

    public static DeviceSettings ApplyField(
        DeviceSettings settings,
        string fieldId,
        string value)
    {
        if (fieldId == "device.manufacturer")
        {
            return settings with { Manufacturer = value };
        }
        if (fieldId == "device.model")
        {
            return settings with { Model = value };
        }
        if (fieldId == "device.osFamily")
        {
            return settings with { OsFamily = value };
        }
        if (!fieldId.StartsWith(
                "device.metrics.",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unknown Device field '{fieldId}'.");
        }

        var metrics = JsonNode.Parse(settings.MetricsJson) as JsonObject
            ?? throw new InvalidOperationException(
                "Device metrics must be a JSON object.");
        switch (fieldId)
        {
            case "device.metrics.canvas.size":
                JsonPath.SetPair(metrics, value,
                    ["canvas", "width"],
                    ["canvas", "height"]);
                break;
            case "device.metrics.screen.position":
                JsonPath.SetPair(metrics, value,
                    ["screen", "x"],
                    ["screen", "y"]);
                break;
            case "device.metrics.screen.size":
                JsonPath.SetPair(metrics, value,
                    ["screen", "width"],
                    ["screen", "height"]);
                break;
            case "device.metrics.cornerRadius":
                JsonPath.Set(metrics, ["cornerRadius"],
                    JsonPath.NumberNode(value));
                metrics.Remove("frame");
                break;
            case "device.metrics.safeArea.bottom":
                JsonPath.Set(metrics, ["safeArea", "bottom"],
                    JsonPath.NumberNode(value));
                break;
            case "device.metrics.statusBar.height":
                JsonPath.Set(metrics, ["statusBar", "height"],
                    JsonPath.NumberNode(value));
                break;
            case "device.metrics.moduleTransparency.enabled":
                JsonPath.Set(metrics,
                    ["moduleTransparency", "enabled"],
                    JsonValue.Create(BooleanText.ParseRequired(
                        value,
                        fieldId))!);
                break;
            case "device.metrics.moduleTransparency.mode":
                JsonPath.Set(metrics,
                    ["moduleTransparency", "mode"],
                    JsonValue.Create(value)!);
                break;
            case "device.metrics.moduleTransparency.paletteColor":
                JsonPath.Set(metrics,
                    ["moduleTransparency", "paletteColor"],
                    JsonValue.Create(value)!);
                break;
            case "device.metrics.moduleTransparency.backgroundOpacity":
                JsonPath.Set(metrics,
                    ["moduleTransparency", "backgroundOpacity"],
                    JsonPath.NumberNode(value));
                break;
            case "device.metrics.moduleTransparency.fixedStart":
                JsonPath.Set(metrics,
                    ["moduleTransparency", "fixedStart"],
                    JsonPath.NumberNode(value));
                break;
            case "device.metrics.moduleTransparency.minimumOpaqueExtent":
                JsonPath.Set(metrics,
                    ["moduleTransparency", "minimumOpaqueExtent"],
                    JsonPath.NumberNode(value));
                break;
            case "device.metrics.moduleTransparency.gradientHeight":
                JsonPath.Set(metrics,
                    ["moduleTransparency", "gradientHeight"],
                    JsonPath.NumberNode(value));
                break;
            case "device.metrics.moduleTransparency.variableOffset":
                JsonPath.Set(metrics,
                    ["moduleTransparency", "variableOffset"],
                    JsonPath.NumberNode(value));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown Device metrics field '{fieldId}'.");
        }
        _ = DeviceMetricRules.PreviewValues(metrics);
        return settings with { MetricsJson = metrics.ToJsonString() };
    }

    public static string FieldValue(
        DeviceSettings settings,
        string fieldId)
    {
        if (fieldId == "device.manufacturer")
        {
            return settings.Manufacturer;
        }
        if (fieldId == "device.model")
        {
            return settings.Model;
        }
        if (fieldId == "device.osFamily")
        {
            return settings.OsFamily;
        }

        var metrics = JsonNode.Parse(settings.MetricsJson) as JsonObject
            ?? throw new InvalidOperationException(
                "Device metrics must be a JSON object.");
        _ = DeviceMetricRules.PreviewValues(metrics);
        const string context = "Device metrics";
        return fieldId switch
        {
            "device.metrics.canvas.size" =>
                JsonPath.RequiredNumberPair(metrics,
                    ["canvas", "width"], ["canvas", "height"], context),
            "device.metrics.screen.position" =>
                JsonPath.RequiredNumberPair(metrics,
                    ["screen", "x"], ["screen", "y"], context),
            "device.metrics.screen.size" =>
                JsonPath.RequiredNumberPair(metrics,
                    ["screen", "width"], ["screen", "height"], context),
            "device.metrics.cornerRadius" =>
                JsonPath.RequiredNumberString(metrics,
                    ["cornerRadius"], context),
            "device.metrics.safeArea.bottom" =>
                JsonPath.RequiredNumberString(metrics,
                    ["safeArea", "bottom"], context),
            "device.metrics.statusBar.height" =>
                JsonPath.RequiredNumberString(metrics,
                    ["statusBar", "height"], context),
            "device.metrics.moduleTransparency.enabled" =>
                BooleanText.Format(JsonPath.RequiredBoolean(
                    JsonPath.RequiredObject(
                        metrics,
                        "moduleTransparency",
                        context),
                    "enabled",
                    context)),
            "device.metrics.moduleTransparency.mode" =>
                JsonPath.RequiredString(
                    JsonPath.RequiredObject(metrics,
                        "moduleTransparency", context),
                    "mode", context),
            "device.metrics.moduleTransparency.paletteColor" =>
                JsonPath.RequiredString(
                    JsonPath.RequiredObject(metrics,
                        "moduleTransparency", context),
                    "paletteColor", context),
            "device.metrics.moduleTransparency.backgroundOpacity" =>
                JsonPath.RequiredNumberString(metrics,
                    ["moduleTransparency", "backgroundOpacity"], context),
            "device.metrics.moduleTransparency.fixedStart" =>
                JsonPath.RequiredNumberString(metrics,
                    ["moduleTransparency", "fixedStart"], context),
            "device.metrics.moduleTransparency.minimumOpaqueExtent" =>
                JsonPath.RequiredNumberString(metrics,
                    ["moduleTransparency", "minimumOpaqueExtent"], context),
            "device.metrics.moduleTransparency.gradientHeight" =>
                JsonPath.RequiredNumberString(metrics,
                    ["moduleTransparency", "gradientHeight"], context),
            "device.metrics.moduleTransparency.variableOffset" =>
                JsonPath.RequiredNumberString(metrics,
                    ["moduleTransparency", "variableOffset"], context),
            _ => throw new InvalidOperationException(
                $"Unknown Device field '{fieldId}'."),
        };
    }

    public static string SetOverride(
        DeviceSettings inherited,
        string overridesJson,
        string fieldId,
        string value,
        string owner)
    {
        var overrides = ParseOverrides(overridesJson, owner);
        if (!OverrideableFieldIds.Contains(
                fieldId,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Device field '{fieldId}' cannot be overridden.");
        }
        if (value == "inherited")
        {
            overrides.Remove(fieldId);
        }
        else
        {
            _ = ApplyField(inherited, fieldId, value);
            overrides[fieldId] = value;
        }
        _ = ApplyOverrides(
            inherited,
            overrides.ToJsonString(),
            owner);
        return overrides.ToJsonString();
    }

    public static DevicePreviewMetrics PreviewMetrics(
        DeviceSettings settings)
    {
        var metrics = JsonNode.Parse(settings.MetricsJson)
            as JsonObject
            ?? throw new InvalidOperationException(
                "Device metrics must be a JSON object.");
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
}
