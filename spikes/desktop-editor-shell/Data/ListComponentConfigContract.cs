using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class ListComponentConfigContract
{
    public const string ComponentType = "list";

    public static void Validate(JsonObject config, string context)
    {
        RequireExactKeys(config, ["boundaryMotion", "list"], context);
        _ = MotionVariantValue.Parse(
            JsonPath.RequiredObject(config, "boundaryMotion", context)
                .ToJsonString());
        var list = JsonPath.RequiredObject(config, "list", context);
        RequireExactKeys(
            list,
            [
                "collectionStackSlot",
                "listItemSlot",
                "sizingMode",
                "startGapToken",
                "endGapToken",
                "itemSizingMode",
                "itemAlignment",
                "itemGapBeforeMode",
                "itemGapBeforeToken",
                "itemGapBeforeWeight",
            ],
            $"{context}.list");
        ComponentVariantSlotDocumentContract.Validate(
            JsonPath.RequiredObject(list, "collectionStackSlot", $"{context}.list"),
            $"{context}.list.collectionStackSlot");
        ComponentVariantSlotDocumentContract.Validate(
            JsonPath.RequiredObject(list, "listItemSlot", $"{context}.list"),
            $"{context}.list.listItemSlot");
        RequireOneOf(
            JsonPath.RequiredString(list, "sizingMode", $"{context}.list"),
            ["content", "fill"],
            $"{context}.list.sizingMode");
        RequireOneOf(
            JsonPath.RequiredString(list, "itemSizingMode", $"{context}.list"),
            ["intrinsic", "largest"],
            $"{context}.list.itemSizingMode");
        RequireOneOf(
            JsonPath.RequiredString(list, "itemAlignment", $"{context}.list"),
            ["start", "center", "end"],
            $"{context}.list.itemAlignment");
        RequireOneOf(
            JsonPath.RequiredString(list, "itemGapBeforeMode", $"{context}.list"),
            ["fixed", "reflow"],
            $"{context}.list.itemGapBeforeMode");
        JsonPath.RequiredString(list, "startGapToken", $"{context}.list");
        JsonPath.RequiredString(list, "endGapToken", $"{context}.list");
        JsonPath.RequiredString(list, "itemGapBeforeToken", $"{context}.list");
        if (JsonPath.RequiredNumber(list, "itemGapBeforeWeight", $"{context}.list") < 0)
        {
            throw new InvalidOperationException(
                $"{context}.list.itemGapBeforeWeight cannot be negative.");
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
