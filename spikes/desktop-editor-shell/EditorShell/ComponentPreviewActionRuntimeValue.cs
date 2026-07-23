using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ComponentPreviewActionRuntimeValue
{
    public static double RequireDurationInput(
        JsonObject preview,
        ComponentPreviewActionDefinition action)
    {
        if (string.IsNullOrWhiteSpace(action.DurationInputId))
        {
            throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' has no durationInputId.");
        }

        return JsonPath.RequiredPositiveNumber(
            ComponentPreviewActions.Value(preview, action, action.DurationInputId),
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
