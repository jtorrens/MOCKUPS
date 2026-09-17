using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class SurfaceStackComponentConfigContract
{
    public const string ComponentType = "surfaceStack";

    public static void Validate(JsonObject config, string context)
    {
        RequireExactKeys(config, ["surfaceStack"], context);
        var stack = JsonPath.RequiredObject(config, "surfaceStack", context);
        var owner = $"{context}.surfaceStack";
        RequireExactKeys(
            stack,
            ["surfaceSlot", "padding", "startGapToken", "endGapToken", "items"],
            owner);
        ComponentVariantSlotDocumentContract.Validate(
            JsonPath.RequiredObject(stack, "surfaceSlot", owner),
            $"{owner}.surfaceSlot");
        _ = RuntimeInputValueKindContract.ParseValue(
            ValueKind.ThemeTokenPair,
            JsonPath.RequiredString(stack, "padding", owner),
            $"{owner}.padding");
        JsonPath.RequiredString(stack, "startGapToken", owner);
        JsonPath.RequiredString(stack, "endGapToken", owner);
        ValidateSlots(JsonPath.RequiredArray(stack, "items", owner), $"{owner}.items");
    }

    private static void ValidateSlots(JsonArray slots, string owner)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < slots.Count; index++)
        {
            var slotOwner = $"{owner}[{index}]";
            var slot = slots[index] as JsonObject
                ?? throw new InvalidOperationException($"{slotOwner} must be an object.");
            RequireExactKeys(
                slot,
                ["id", "name", "sizeMode", "alternatives", "gapBeforeMode", "gapBeforeToken", "gapBeforeWeight"],
                slotOwner);
            var id = JsonPath.RequiredString(slot, "id", slotOwner);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"{owner} slot id '{id}' is duplicated.");
            }
            JsonPath.RequiredString(slot, "name", slotOwner);
            RequireOneOf(JsonPath.RequiredString(slot, "sizeMode", slotOwner), ["content", "fill"], $"{slotOwner}.sizeMode");
            RequireOneOf(JsonPath.RequiredString(slot, "gapBeforeMode", slotOwner), ["fixed", "reflow"], $"{slotOwner}.gapBeforeMode");
            JsonPath.RequiredString(slot, "gapBeforeToken", slotOwner);
            if (JsonPath.RequiredNumber(slot, "gapBeforeWeight", slotOwner) < 0)
            {
                throw new InvalidOperationException($"{slotOwner}.gapBeforeWeight cannot be negative.");
            }
            ValidateAlternatives(
                JsonPath.RequiredArray(slot, "alternatives", slotOwner),
                $"{slotOwner}.alternatives");
        }
    }

    private static void ValidateAlternatives(JsonArray alternatives, string owner)
    {
        if (alternatives.Count == 0)
        {
            throw new InvalidOperationException($"{owner} requires at least one state.");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < alternatives.Count; index++)
        {
            var stateOwner = $"{owner}[{index}]";
            var state = alternatives[index] as JsonObject
                ?? throw new InvalidOperationException($"{stateOwner} must be an object.");
            RequireExactKeys(
                state,
                ["id", "name", "componentSlot", "behavior", "placement", "enterMotion", "exitMotion"],
                stateOwner);
            var id = JsonPath.RequiredString(state, "id", stateOwner);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"{owner} state id '{id}' is duplicated.");
            }
            JsonPath.RequiredString(state, "name", stateOwner);
            if (state["componentSlot"] is JsonObject componentSlot)
            {
                ComponentVariantSlotDocumentContract.Validate(componentSlot, $"{stateOwner}.componentSlot");
            }
            else if (state["componentSlot"] is not null)
            {
                throw new InvalidOperationException($"{stateOwner}.componentSlot must be null or a Component Variant Slot.");
            }
            var behavior = JsonPath.RequiredString(state, "behavior", stateOwner);
            RequireOneOf(behavior, ["replace", "overlay"], $"{stateOwner}.behavior");
            if (index == 0 && !behavior.Equals("replace", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{stateOwner}.behavior must be 'replace' for the initial state.");
            }
            _ = AlignmentPlacementValue.Parse(JsonPath.RequiredObject(state, "placement", stateOwner).ToJsonString());
            _ = MotionVariantValue.Parse(JsonPath.RequiredObject(state, "enterMotion", stateOwner).ToJsonString());
            _ = MotionVariantValue.Parse(JsonPath.RequiredObject(state, "exitMotion", stateOwner).ToJsonString());
        }
    }

    private static void RequireExactKeys(JsonObject value, IReadOnlyList<string> expected, string owner)
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

    private static void RequireOneOf(string value, IReadOnlyList<string> options, string path)
    {
        if (!options.Contains(value, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"{path} has unsupported value '{value}'.");
        }
    }
}
