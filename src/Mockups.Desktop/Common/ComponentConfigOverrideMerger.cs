using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class ComponentConfigOverrideMerger
{
    public static void MergeInto(JsonObject target, JsonObject overrides)
    {
        foreach (var pair in overrides)
        {
            if (pair.Value is JsonObject overrideObject
                && target[pair.Key] is JsonObject targetObject
                && !IsExactComponentVariantSlot(overrideObject))
            {
                MergeInto(targetObject, overrideObject);
                continue;
            }

            target[pair.Key] = pair.Value?.DeepClone();
        }
    }

    private static bool IsExactComponentVariantSlot(JsonObject value)
    {
        if (value.Count != 2
            || value["variantReference"] is not JsonValue referenceValue
            || !referenceValue.TryGetValue<string>(out var reference)
            || value["overrides"] is not JsonObject)
        {
            return false;
        }

        return VariantReferenceId.TryParse(reference, out _, out _);
    }
}
