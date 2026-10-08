using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ComponentPreviewActionRuntimeValue
{
    public static string RequireTargetValue(JsonObject preview, ComponentPreviewActionDefinition action) =>
        ComponentPreviewActions.Value(preview, action, action.TargetInputId) switch
        {
            JsonValue value when value.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value when value.GetValueKind() == System.Text.Json.JsonValueKind.Number => value.ToJsonString(),
            _ => throw new InvalidOperationException($"Design action '{action.Id}' requires its declared scalar target '{action.TargetInputId}'."),
        };

    public static int DurationFrames(ComponentPreviewActionDefinition action, JsonObject preview, int fps, string themeTokensJson)
    {
        if (action.DurationOwnerTimeline)
        {
            return RuntimeTimeline.DurationFrames(
                preview.ToJsonString(),
                preview.ToJsonString(),
                "{}",
                1,
                themeTokensJson,
                fps);
        }
        if (!string.IsNullOrWhiteSpace(action.DurationStateCollectionJsonKey))
        {
            var durationMs = ComponentPreviewActions.MotionStateTransitionDurationMilliseconds(
                preview,
                action,
                themeTokensJson);
            return durationMs <= 0
                ? 0
                : Math.Max(1, (int)Math.Ceiling(durationMs / 1000.0 * Math.Max(1, fps)));
        }
        if (!string.IsNullOrWhiteSpace(action.DurationThemeToken))
        {
            var themeTokens = JsonPath.ParseRequiredObject(themeTokensJson, "Theme tokens");
            var value = ThemeNumericTokenValue.RequirePositive(
                themeTokens,
                action.DurationThemeToken,
                $"Design Preview action '{action.Id}' duration");
            var seconds = action.TimeUnit switch
            {
                ComponentPreviewActionTimeUnit.Milliseconds => value / 1000.0,
                ComponentPreviewActionTimeUnit.Frames => value / Math.Max(1, fps),
                _ => value,
            };
            return seconds <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(seconds * Math.Max(1, fps)));
        }
        if (!string.IsNullOrWhiteSpace(action.DurationCollectionJsonKey))
        {
            return ComponentPreviewActionRuntimeValue.CollectionDurationFrames(preview, action);
        }
        if (!string.IsNullOrWhiteSpace(action.DurationBehaviorTimingInputId))
        {
            var owner = ComponentPreviewActions.RequiredOwner(preview, action);
            var fields = ComponentPreviewActionRuntimeValue.RequireInputDefinitions(preview, action);
            var definition = fields.FirstOrDefault((field) =>
                field["id"]?.GetValue<string>() == action.DurationBehaviorTimingInputId)
                ?? throw new InvalidOperationException(
                    $"Missing BehaviorTiming action input '{action.DurationBehaviorTimingInputId}'.");
            var themeTokens = JsonPath.ParseRequiredObject(themeTokensJson, "Theme tokens");
            return BehaviorTimingResolver.ResolveFrames(owner, definition, fields, themeTokens);
        }

        if (action.TimeUnit == ComponentPreviewActionTimeUnit.Frames)
        {
            return Math.Max(1, (int)Math.Round(
                ComponentPreviewActionRuntimeValue.RequireDurationInput(preview, action),
                MidpointRounding.AwayFromZero));
        }

        var duration = action.DurationSeconds > 0
            ? action.DurationSeconds
            : ComponentPreviewActionRuntimeValue.RequireDurationInput(preview, action);
        return Math.Max(1, (int)Math.Ceiling(duration * Math.Max(1, fps)));
    }

    public static double DurationSeconds(ComponentPreviewActionDefinition action, JsonObject preview, int fps, string themeTokensJson)
    {
        if (!string.IsNullOrWhiteSpace(action.DurationStateCollectionJsonKey))
            return ComponentPreviewActions.MotionStateTransitionDurationMilliseconds(preview, action, themeTokensJson) / 1000.0;
        if (!string.IsNullOrWhiteSpace(action.DurationThemeToken))
        {
            var value = ThemeNumericTokenValue.RequirePositive(
                JsonPath.ParseRequiredObject(themeTokensJson, "Theme tokens"), action.DurationThemeToken,
                $"Design Preview action '{action.Id}' duration");
            return action.TimeUnit switch
            {
                ComponentPreviewActionTimeUnit.Milliseconds => value / 1000.0,
                ComponentPreviewActionTimeUnit.Frames => value / Math.Max(1, fps),
                _ => value,
            };
        }
        if (action.TimeUnit == ComponentPreviewActionTimeUnit.Frames)
            return DurationFrames(action, preview, fps, themeTokensJson) / (double)Math.Max(1, fps);
        return action.DurationSeconds > 0 ? action.DurationSeconds : RequireDurationInput(preview, action);
    }

    public static double NormalizedTime(
        double value, ComponentPreviewActionDefinition action, JsonObject preview, int fps, string themeTokensJson)
    {
        var seconds = action.TimeUnit switch
        {
            ComponentPreviewActionTimeUnit.Frames => value / Math.Max(1, fps),
            ComponentPreviewActionTimeUnit.Milliseconds => value / 1000.0,
            _ => value,
        };
        var duration = DurationSeconds(action, preview, fps, themeTokensJson);
        var clamped = Math.Clamp(seconds, 0, duration);
        var snapped = Math.Clamp(Math.Round(clamped * fps, MidpointRounding.AwayFromZero) / fps, 0, duration);
        return action.TimeUnit switch
        {
            ComponentPreviewActionTimeUnit.Frames => Math.Min(DurationFrames(action, preview, fps, themeTokensJson), Math.Floor(snapped * fps + 0.0001)),
            ComponentPreviewActionTimeUnit.Milliseconds => snapped * 1000,
            _ => snapped,
        };
    }
    public static double RequireDurationInput(
        JsonObject preview,
        ComponentPreviewActionDefinition action)
    {
        if (string.IsNullOrWhiteSpace(action.DurationInputId))
        {
            throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' has no durationInputId.");
        }

        var durationJsonKey = ComponentPreviewActions.DurationJsonKey(preview, action);
        return JsonPath.RequiredPositiveNumber(
            ComponentPreviewActions.Value(preview, action, durationJsonKey),
            $"Design Preview action '{action.Id}' duration input '{action.DurationInputId}'");
    }

    public static double RequireDurationInput(
        string value,
        ComponentPreviewActionDefinition action)
    {
        if (string.IsNullOrWhiteSpace(action.DurationInputId))
        {
            throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' has no durationInputId.");
        }

        var duration = ParseFiniteSessionNumber(
            value,
            $"Design Preview action '{action.Id}' duration input '{action.DurationInputId}'");
        if (duration <= 0)
        {
            throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' duration input '{action.DurationInputId}' must be positive.");
        }
        return duration;
    }

    public static double RequireTime(JsonObject preview, ComponentPreviewActionDefinition action) =>
        JsonPath.RequiredNonNegativeNumber(
            ComponentPreviewActions.Value(preview, action, action.TimeJsonKey),
            $"Design Preview action '{action.Id}' time input '{action.TimeJsonKey}'");

    public static double TimeOrDefault(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        double absentValue)
    {
        var owner = ComponentPreviewActions.RequiredOwner(preview, action);
        return owner.TryGetPropertyValue(action.TimeJsonKey, out _)
            ? RequireTime(preview, action)
            : absentValue;
    }

    public static double RequireTime(string value, ComponentPreviewActionDefinition action)
    {
        var time = ParseFiniteSessionNumber(
            value,
            $"Design Preview action '{action.Id}' time input '{action.TimeJsonKey}'");
        if (time < 0)
        {
            throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' time input '{action.TimeJsonKey}' must not be negative.");
        }
        return time;
    }

    public static bool RequireBoolean(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string inputId)
    {
        var node = ComponentPreviewActions.Value(preview, action, inputId);
        if (node is JsonValue value && value.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        throw new InvalidOperationException(
            $"Design Preview action '{action.Id}' input '{inputId}' must be a JSON boolean.");
    }

    public static bool BooleanOrDefault(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string inputId,
        bool absentValue)
    {
        var owner = ComponentPreviewActions.RequiredOwner(preview, action);
        return owner.TryGetPropertyValue(inputId, out _)
            ? RequireBoolean(preview, action, inputId)
            : absentValue;
    }

    public static IReadOnlyList<JsonObject> RequireInputDefinitions(
        JsonObject preview,
        ComponentPreviewActionDefinition action)
    {
        var owner = ComponentPreviewActions.RequiredOwner(preview, action);
        var inputs = owner["inputs"] as JsonArray
            ?? throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' requires an inputs definition array.");
        var definitions = inputs.Select((node, index) => node as JsonObject
                ?? throw new InvalidOperationException(
                    $"Design Preview action '{action.Id}' inputs[{index}] must be an object."))
            .ToList();
        RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions(
            definitions,
            $"Design Preview action '{action.Id}' inputs");
        return definitions;
    }

    public static int CollectionDurationFrames(
        JsonObject preview,
        ComponentPreviewActionDefinition action)
    {
        if (string.IsNullOrWhiteSpace(action.DurationCollectionJsonKey))
        {
            throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' has no durationCollectionJsonKey.");
        }

        var owner = ComponentPreviewActions.RequiredOwner(preview, action);
        var items = owner[action.DurationCollectionJsonKey] as JsonArray
            ?? throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' duration collection '{action.DurationCollectionJsonKey}' must be an array.");
        RuntimeCollectionDocumentContract.Validate(
            items,
            $"Design Preview action '{action.Id}' duration collection '{action.DurationCollectionJsonKey}'");

        var total = action.DurationBaseFrames;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Design Preview action '{action.Id}' duration collection item at index {index} must be an object.");
            foreach (var key in action.DurationItemNumberKeys)
            {
                total += JsonPath.RequiredNonNegativeNumber(
                    item[key],
                    $"Design Preview action '{action.Id}' duration collection item '{key}'");
            }
            foreach (var key in action.DurationCollectionMultiplierNumberKeys)
            {
                total += JsonPath.RequiredNonNegativeNumber(
                    owner[key],
                    $"Design Preview action '{action.Id}' duration collection multiplier '{key}'");
            }
        }

        return Math.Max(1, (int)Math.Ceiling(total));
    }

    private static double ParseFiniteSessionNumber(string value, string context)
    {
        if (!double.TryParse(
                value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number)
            || !double.IsFinite(number))
        {
            throw new InvalidOperationException($"{context} must be a finite number.");
        }

        return number;
    }
}
