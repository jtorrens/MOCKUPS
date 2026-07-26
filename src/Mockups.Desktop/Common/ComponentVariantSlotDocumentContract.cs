using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class ComponentVariantSlotDocumentContract
{
    private static readonly HashSet<string> CurrentKeys = new(StringComparer.Ordinal)
    {
        "variantReference",
        "overrides",
    };

    public static JsonObject Parse(string json, string owner)
    {
        var slot = JsonPath.ParseRequiredObject(json, owner);
        Validate(slot, owner);
        return slot;
    }

    public static void Validate(JsonObject slot, string owner)
    {
        var keys = slot.Select((entry) => entry.Key).ToHashSet(StringComparer.Ordinal);
        if (!keys.SetEquals(CurrentKeys))
        {
            var missing = CurrentKeys.Except(keys).Order().ToList();
            var unknown = keys.Except(CurrentKeys).Order().ToList();
            throw new InvalidOperationException(
                $"{owner} must use the exact current Component Variant Slot document"
                + $"{(missing.Count == 0 ? "" : $"; missing: {string.Join(", ", missing)}")}"
                + $"{(unknown.Count == 0 ? "" : $"; unknown: {string.Join(", ", unknown)}")}.");
        }

        var reference = JsonPath.RequiredString(slot, "variantReference", owner);
        if (!VariantReferenceId.TryParse(reference, out _, out _))
        {
            throw new InvalidOperationException(
                $"{owner} Component Variant reference '{reference}' is not a full Variant reference.");
        }
        _ = JsonPath.RequiredObject(slot, "overrides", owner);
    }

    public static string VariantReference(JsonObject slot, string owner)
    {
        Validate(slot, owner);
        return JsonPath.RequiredString(slot, "variantReference", owner);
    }

    public static JsonObject Overrides(JsonObject slot, string owner)
    {
        Validate(slot, owner);
        return JsonPath.RequiredObject(slot, "overrides", owner);
    }

    public static JsonObject Create(string variantReference, JsonObject overrides, string owner)
    {
        var slot = new JsonObject
        {
            ["variantReference"] = variantReference,
            ["overrides"] = overrides.DeepClone(),
        };
        Validate(slot, owner);
        return slot;
    }
}
