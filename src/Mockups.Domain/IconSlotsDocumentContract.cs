using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

public static class IconSlotsDocumentContract
{
    private static readonly HashSet<string> CurrentKeys = new(StringComparer.Ordinal)
    {
        "id",
        "buttonVariantReference",
        "state",
        "iconToken",
        "text",
        "iconSizeToken",
        "textSizeToken",
        "pushTrigger",
        "pushElapsedMs",
        "buttonOverrides",
    };

    private static readonly HashSet<string> States = new(StringComparer.Ordinal)
    {
        "normal",
        "active",
        "pushed",
        "disabled",
    };

    public static void Validate(JsonArray items, string owner)
    {
        RuntimeCollectionDocumentContract.Validate(items, owner);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index]!.AsObject();
            var context = $"{owner} item at index {index}";
            var keys = item.Select((entry) => entry.Key).ToHashSet(StringComparer.Ordinal);
            if (!keys.SetEquals(CurrentKeys))
            {
                var missing = CurrentKeys.Except(keys).Order().ToList();
                var unknown = keys.Except(CurrentKeys).Order().ToList();
                throw new InvalidOperationException(
                    $"{context} must use the exact current Icon Slots document"
                    + $"{(missing.Count == 0 ? "" : $"; missing: {string.Join(", ", missing)}")}"
                    + $"{(unknown.Count == 0 ? "" : $"; unknown: {string.Join(", ", unknown)}")}.");
            }

            var reference = JsonPath.RequiredString(item, "buttonVariantReference", context);
            if (!VariantReferenceId.TryParse(reference, out _, out _))
            {
                throw new InvalidOperationException(
                    $"{context} Button Variant reference '{reference}' is not a full Variant reference.");
            }

            var state = JsonPath.RequiredString(item, "state", context);
            if (!States.Contains(state))
            {
                throw new InvalidOperationException(
                    $"{context} has unsupported Button state '{state}'.");
            }

            _ = JsonPath.RequiredString(item, "iconToken", context);
            _ = JsonPath.RequiredString(item, "text", context, allowEmpty: true);
            _ = JsonPath.RequiredString(item, "iconSizeToken", context);
            _ = JsonPath.RequiredString(item, "textSizeToken", context);
            _ = JsonPath.RequiredBoolean(item, "pushTrigger", context);
            _ = JsonPath.RequiredNonNegativeNumber(item["pushElapsedMs"], $"{context} pushElapsedMs");
            _ = JsonPath.RequiredObject(item, "buttonOverrides", context);
        }
    }

    public static void ReplaceButtonVariantSlot(
        IReadOnlyList<JsonObject> items,
        string itemId,
        string value,
        string owner)
    {
        var item = items.SingleOrDefault((candidate) =>
                JsonPath.RequiredString(candidate, "id", owner)
                    .Equals(itemId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"{owner} item '{itemId}' no longer exists in the current Icon Row collection.");
        var slotOwner = $"{owner} Button '{itemId}'";
        var slot = ComponentVariantSlotDocumentContract.Parse(value, slotOwner);
        item["buttonVariantReference"] =
            ComponentVariantSlotDocumentContract.VariantReference(slot, slotOwner);
        item["buttonOverrides"] =
            ComponentVariantSlotDocumentContract.Overrides(slot, slotOwner).DeepClone();
    }

    public static void ValidateRuntimeItemsMatchStructure(
        JsonArray structuralItems,
        JsonArray runtimeItems,
        string owner)
    {
        Validate(structuralItems, $"{owner} structural items");
        Validate(runtimeItems, $"{owner} Runtime items");
        var structuralIds = structuralItems
            .Select((item) => JsonPath.RequiredString(
                item!.AsObject(),
                "id",
                $"{owner} structural item"))
            .ToList();
        var runtimeIds = runtimeItems
            .Select((item) => JsonPath.RequiredString(
                item!.AsObject(),
                "id",
                $"{owner} Runtime item"))
            .ToList();
        var missing = structuralIds.Except(runtimeIds, StringComparer.Ordinal)
            .ToList();
        var unknown = runtimeIds.Except(structuralIds, StringComparer.Ordinal)
            .ToList();
        if (missing.Count == 0 && unknown.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{owner} Runtime Button items must match its effective Icon Row structure exactly"
            + (missing.Count == 0 ? "" : $"; missing: {string.Join(", ", missing)}")
            + (unknown.Count == 0 ? "" : $"; unknown: {string.Join(", ", unknown)}"));
    }
}
