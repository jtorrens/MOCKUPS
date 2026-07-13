using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class RuntimeAnimationFrameOrigin
{
    public static int ScreenFrame(
        JsonObject contract,
        JsonObject runtime,
        string fieldId,
        string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return 0;

        foreach (var collection in (contract["collections"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var field = (collection["fields"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault((candidate) =>
                candidate["id"]?.GetValue<string>() == fieldId
                && candidate["animationFrameOrigin"]?.GetValue<string>() == "targetStart");
            if (field is null) continue;

            var collectionKey = collection["sourceCollectionJsonKey"]?.GetValue<string>()
                ?? collection["jsonKey"]?.GetValue<string>()
                ?? "";
            if (string.IsNullOrWhiteSpace(collectionKey) || runtime[collectionKey] is not JsonArray items) continue;

            var cursor = 0;
            var sequenceKeys = StringArray(collection["animationTargetSequenceNumberKeys"]);
            var startKeys = StringArray(collection["animationTargetStartNumberKeys"]);
            foreach (var item in items.OfType<JsonObject>())
            {
                if (item["id"]?.GetValue<string>() == targetId)
                    return cursor + startKeys.Sum((key) => NonNegativeInt(item[key]));
                cursor += sequenceKeys.Sum((key) => NonNegativeInt(item[key]));
            }
        }

        return 0;
    }

    private static string[] StringArray(JsonNode? value) =>
        (value as JsonArray)?.OfType<JsonValue>().Select((item) => item.GetValue<string>()).ToArray() ?? [];

    private static int NonNegativeInt(JsonNode? value)
    {
        if (value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var integer)) return Math.Max(0, integer);
        if (value is JsonValue decimalValue && decimalValue.TryGetValue<decimal>(out var number)) return Math.Max(0, (int)Math.Ceiling(number));
        return 0;
    }
}
