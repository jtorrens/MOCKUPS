using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

public static class ComponentConfigOverrideMerger
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

    /// <summary>
    /// Removes the fields supplied by an overlay from an already effective
    /// document. This is the inverse needed when an embedded editor exposes
    /// the inherited value separately from its owner-local override.
    /// </summary>
    public static void RemoveOverlay(JsonObject target, JsonObject overlay)
    {
        foreach (var pair in overlay)
        {
            if (pair.Value is JsonObject overlayObject
                && target[pair.Key] is JsonObject targetObject
                && !IsExactComponentVariantSlot(overlayObject))
            {
                RemoveOverlay(targetObject, overlayObject);
                if (targetObject.Count == 0)
                {
                    target.Remove(pair.Key);
                }
                continue;
            }

            target.Remove(pair.Key);
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
