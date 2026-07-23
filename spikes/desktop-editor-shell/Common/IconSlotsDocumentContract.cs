using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class IconSlotsDocumentContract
{
    private static readonly HashSet<string> CurrentKeys = new(StringComparer.Ordinal)
    {
        "id",
        "buttonVariantReference",
        "contentMode",
        "state",
        "iconToken",
        "text",
        "iconSizeToken",
        "textSizeToken",
        "pushTrigger",
        "pushElapsedMs",
        "buttonOverrides",
    };

    private static readonly HashSet<string> ContentModes = new(StringComparer.Ordinal)
    {
        "icon",
        "text",
        "iconText",
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

            var contentMode = JsonPath.RequiredString(item, "contentMode", context);
            if (!ContentModes.Contains(contentMode))
            {
                throw new InvalidOperationException(
                    $"{context} has unsupported content mode '{contentMode}'.");
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
}
