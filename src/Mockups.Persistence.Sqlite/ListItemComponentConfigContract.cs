using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class ListItemComponentConfigContract
{
    public const string ComponentType = "listItem";

    public static void Validate(JsonObject config, string context)
    {
        RequireExactKeys(config, ["boundaryMotion", "listItem"], context);
        _ = MotionVariantValue.Parse(
            JsonPath.RequiredObject(config, "boundaryMotion", context)
                .ToJsonString());
        var listItem = JsonPath.RequiredObject(config, "listItem", context);
        RequireExactKeys(
            listItem,
            ["contentSetCount", "padding", "gapToken", "components", "states"],
            $"{context}.listItem");
        var contentSetCount = JsonPath.RequiredNumber(
            listItem,
            "contentSetCount",
            $"{context}.listItem");
        if (contentSetCount < 1 || contentSetCount != Math.Truncate(contentSetCount))
        {
            throw new InvalidOperationException(
                $"{context}.listItem.contentSetCount must be a positive integer.");
        }
        _ = RuntimeInputValueKindContract.ParseValue(
            ValueKind.ThemeTokenPair,
            JsonPath.RequiredString(listItem, "padding", $"{context}.listItem"),
            $"{context}.listItem.padding");
        _ = JsonPath.RequiredString(listItem, "gapToken", $"{context}.listItem");

        var components = JsonPath.RequiredObject(
            listItem,
            "components",
            $"{context}.listItem");
        RequireExactKeys(
            components,
            ["avatar", "label", "iconRow"],
            $"{context}.listItem.components");
        var orders = new HashSet<int>();
        foreach (var componentType in new[] { "avatar", "label", "iconRow" })
        {
            var owner = $"{context}.listItem.components.{componentType}";
            var component = JsonPath.RequiredObject(components, componentType, owner);
            RequireExactKeys(
                component,
                ["visible", "order", "componentSlot", "sizeMode", "fixedSize", "verticalAlignment"],
                owner);
            _ = JsonPath.RequiredBoolean(component, "visible", owner);
            var order = JsonPath.RequiredNumber(component, "order", owner);
            if (order < 1 || order != Math.Truncate(order))
            {
                throw new InvalidOperationException($"{owner}.order must be a positive integer.");
            }
            if (!orders.Add((int)order))
            {
                throw new InvalidOperationException(
                    $"{context}.listItem component order '{order}' is duplicated.");
            }
            ComponentVariantSlotDocumentContract.Validate(
                JsonPath.RequiredObject(component, "componentSlot", owner),
                $"{owner}.componentSlot");
            var sizeMode = JsonPath.RequiredString(component, "sizeMode", owner);
            RequireOneOf(
                sizeMode,
                componentType switch
                {
                    "avatar" => ["auto", "fixed"],
                    "label" => ["fill", "fixed"],
                    _ => ["content", "fixed"],
                },
                $"{owner}.sizeMode");
            if (componentType == "avatar")
            {
                if (JsonPath.RequiredNumber(component, "fixedSize", owner) <= 0)
                {
                    throw new InvalidOperationException($"{owner}.fixedSize must be positive.");
                }
            }
            else
            {
                _ = RuntimeInputValueKindContract.ParseValue(
                    ValueKind.IntegerPair,
                    JsonPath.RequiredString(component, "fixedSize", owner),
                    $"{owner}.fixedSize");
            }
            RequireOneOf(
                JsonPath.RequiredString(component, "verticalAlignment", owner),
                ["start", "center", "end"],
                $"{owner}.verticalAlignment");
        }

        var states = JsonPath.RequiredObject(listItem, "states", $"{context}.listItem");
        RequireExactKeys(states, ["normal", "pressed", "inactive"], $"{context}.listItem.states");
        foreach (var state in new[] { "normal", "pressed", "inactive" })
        {
            var owner = $"{context}.listItem.states.{state}";
            var value = JsonPath.RequiredObject(states, state, $"{context}.listItem.states");
            RequireExactKeys(value, ["surfaceSlot", "elementsOpacity"], owner);
            ComponentVariantSlotDocumentContract.Validate(
                JsonPath.RequiredObject(value, "surfaceSlot", owner),
                $"{owner}.surfaceSlot");
            var opacity = JsonPath.RequiredNumber(value, "elementsOpacity", owner);
            if (opacity < 0 || opacity > 1)
            {
                throw new InvalidOperationException(
                    $"{owner}.elementsOpacity must be between 0 and 1.");
            }
        }
    }

    private static void RequireExactKeys(
        JsonObject value,
        IReadOnlyList<string> expected,
        string owner)
    {
        var missing = expected.Where((key) => !value.ContainsKey(key)).ToList();
        var unknown = value.Select((pair) => pair.Key)
            .Where((key) => !expected.Contains(key, StringComparer.Ordinal))
            .ToList();
        if (missing.Count == 0 && unknown.Count == 0) return;
        throw new InvalidOperationException(
            $"{owner} has an invalid shape."
            + (missing.Count > 0 ? $" Missing: {string.Join(", ", missing)}." : "")
            + (unknown.Count > 0 ? $" Unknown: {string.Join(", ", unknown)}." : ""));
    }

    private static void RequireOneOf(
        string value,
        IReadOnlyList<string> options,
        string path)
    {
        if (!options.Contains(value, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"{path} has unsupported value '{value}'.");
        }
    }
}
