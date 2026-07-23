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
        RequireExactKeys(config, ["listItem"], context);
        var listItem = JsonPath.RequiredObject(config, "listItem", context);
        RequireExactKeys(listItem, ["size", "elements", "states"], $"{context}.listItem");
        _ = RuntimeInputValueKindContract.ParseValue(
            ValueKind.IntegerPair,
            JsonPath.RequiredString(listItem, "size", $"{context}.listItem"),
            $"{context}.listItem.size");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var types = new HashSet<string>(StringComparer.Ordinal);
        var elements = JsonPath.RequiredArray(listItem, "elements", $"{context}.listItem");
        for (var index = 0; index < elements.Count; index++)
        {
            var owner = $"{context}.listItem.elements[{index}]";
            var element = elements[index] as JsonObject
                ?? throw new InvalidOperationException($"{owner} must be an object.");
            RequireExactKeys(
                element,
                ["id", "componentType", "componentSlot", "size", "placement"],
                owner);
            var id = JsonPath.RequiredString(element, "id", owner);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"{context}.listItem.elements contains duplicate id '{id}'.");
            }
            var componentType = JsonPath.RequiredString(element, "componentType", owner);
            RequireOneOf(componentType, ["avatar", "label", "iconRow"], $"{owner}.componentType");
            if (!types.Add(componentType))
            {
                throw new InvalidOperationException(
                    $"{context}.listItem.elements may contain at most one '{componentType}'.");
            }
            ComponentVariantSlotDocumentContract.Validate(
                JsonPath.RequiredObject(element, "componentSlot", owner),
                $"{owner}.componentSlot");
            _ = RuntimeInputValueKindContract.ParseValue(
                ValueKind.IntegerPair,
                JsonPath.RequiredString(element, "size", owner),
                $"{owner}.size");
            _ = AlignmentPlacementValue.Parse(
                JsonPath.RequiredObject(element, "placement", owner).ToJsonString());
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
