using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DesignPreviewTestValues
{
    public static JsonObject Parse(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    public static string RuntimeJson(string previewJson)
    {
        var preview = Parse(previewJson);
        ApplyCollectionSources(preview);
        var sourcedCollectionKeys = SourcedCollectionKeys(preview);
        if (preview["testValues"] is not JsonObject testValues)
        {
            return preview.ToJsonString();
        }

        foreach (var (key, value) in testValues)
        {
            if (sourcedCollectionKeys.Contains(key))
            {
                continue;
            }
            preview[key] = value?.DeepClone();
        }

        return preview.ToJsonString();
    }

    public static string Value(JsonObject preview, ComponentInputDefinition input)
    {
        var testValues = preview["testValues"] as JsonObject;
        var value = testValues?[input.JsonKey] ?? preview[input.JsonKey];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
            JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var number) => number.ToString(CultureInfo.InvariantCulture),
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
            JsonArray array => array.ToJsonString(),
            _ => input.DefaultValue,
        };
    }

    public static void SetValue(JsonObject preview, ComponentInputDefinition input, string value)
    {
        var testValues = preview["testValues"] as JsonObject ?? new JsonObject();
        preview["testValues"] = testValues;
        testValues[input.JsonKey] = ValueNode(input, value);
    }

    public static IReadOnlyList<JsonObject> CollectionItems(
        JsonObject preview,
        RuntimeInputCollectionDefinition collection)
    {
        if (!string.IsNullOrWhiteSpace(collection.SourceCollectionJsonKey))
        {
            return SourceCollectionItems(preview, collection).ToList();
        }

        var source = preview["testValues"] is JsonObject testValues
                     && testValues[collection.JsonKey] is JsonArray testItems
            ? testItems
            : preview[collection.JsonKey] as JsonArray;
        return source?.OfType<JsonObject>().Select(CloneObject).ToList() ?? [];
    }

    public static string CollectionValue(JsonObject item, ComponentInputDefinition input)
    {
        var value = item[input.JsonKey];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
            JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var number) => number.ToString(CultureInfo.InvariantCulture),
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
            JsonArray array => array.ToJsonString(),
            _ => input.DefaultValue,
        };
    }

    public static void SetCollectionValue(
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        ComponentInputDefinition input,
        string value)
    {
        if (!string.IsNullOrWhiteSpace(collection.SourceCollectionJsonKey))
        {
            SetSourcedCollectionValue(preview, collection, itemIndex, input, value);
            return;
        }

        var items = CollectionItems(preview, collection).Select(CloneObject).ToList();
        if (itemIndex < 0 || itemIndex >= items.Count)
        {
            return;
        }

        items[itemIndex][input.JsonKey] = ValueNode(input, value);
        var testValues = preview["testValues"] as JsonObject ?? new JsonObject();
        preview["testValues"] = testValues;
        testValues[collection.JsonKey] = new JsonArray(items.Select((item) => (JsonNode?)item).ToArray());
    }

    public static void PromoteToDefaults(
        JsonObject preview,
        IReadOnlyList<ComponentInputDefinition> inputs,
        IReadOnlyList<RuntimeInputCollectionDefinition>? collections = null)
    {
        var contract = preview["inputs"] as JsonArray;
        foreach (var input in inputs)
        {
            var value = Value(preview, input);
            preview[input.JsonKey] = ValueNode(input, value);
            if (contract is null)
            {
                continue;
            }

            var definition = contract
                .OfType<JsonObject>()
                .FirstOrDefault((candidate) =>
                    candidate["id"] is JsonValue id
                    && id.TryGetValue<string>(out var text)
                    && text.Equals(input.Id, StringComparison.Ordinal));
            if (definition is not null)
            {
                definition["defaultValue"] = value;
            }
        }

        foreach (var collection in collections ?? [])
        {
            if (!string.IsNullOrWhiteSpace(collection.SourceCollectionJsonKey))
            {
                preview[collection.SourceCollectionJsonKey] = new JsonArray(
                    CollectionItems(preview, collection).Select((item) => (JsonNode?)item).ToArray());
            }
            else if (preview["testValues"] is JsonObject values
                     && values[collection.JsonKey] is JsonArray items)
            {
                preview[collection.JsonKey] = items.DeepClone();
            }
        }

        preview.Remove("testValues");
    }

    private static void ApplyCollectionSources(JsonObject preview)
    {
        var collections = preview["collections"] as JsonArray;
        if (collections is null) return;
        foreach (var collectionNode in collections.OfType<JsonObject>())
        {
            var jsonKey = JsonString(collectionNode, "jsonKey");
            var sourceKey = JsonString(collectionNode, "sourceCollectionJsonKey");
            if (string.IsNullOrWhiteSpace(jsonKey)
                || string.IsNullOrWhiteSpace(sourceKey))
            {
                continue;
            }

            var sourceItems = MergeCollectionSource(
                preview[sourceKey] as JsonArray,
                (preview["testValues"] as JsonObject)?[jsonKey] as JsonArray);
            preview[jsonKey] = new JsonArray(sourceItems.Select((item) => (JsonNode?)item).ToArray());
        }
    }

    private static HashSet<string> SourcedCollectionKeys(JsonObject preview)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var collectionNode in (preview["collections"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var jsonKey = JsonString(collectionNode, "jsonKey");
            var sourceKey = JsonString(collectionNode, "sourceCollectionJsonKey");
            if (!string.IsNullOrWhiteSpace(jsonKey)
                && !string.IsNullOrWhiteSpace(sourceKey))
            {
                keys.Add(jsonKey);
            }
        }
        return keys;
    }

    private static IReadOnlyList<JsonObject> SourceCollectionItems(
        JsonObject preview,
        RuntimeInputCollectionDefinition collection)
    {
        return MergeCollectionSource(
            preview[collection.SourceCollectionJsonKey] as JsonArray,
            (preview["testValues"] as JsonObject)?[collection.JsonKey] as JsonArray);
    }

    private static IReadOnlyList<JsonObject> MergeCollectionSource(JsonArray? source, JsonArray? overrides)
    {
        var overrideById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var item in overrides?.OfType<JsonObject>() ?? [])
        {
            var id = JsonString(item, "id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                overrideById[id] = item;
            }
        }

        var result = new List<JsonObject>();
        foreach (var sourceItem in source?.OfType<JsonObject>() ?? [])
        {
            var item = CloneObject(sourceItem);
            var id = JsonString(item, "id");
            if (!string.IsNullOrWhiteSpace(id)
                && overrideById.TryGetValue(id, out var overrideItem))
            {
                foreach (var (key, value) in overrideItem)
                {
                    item[key] = value?.DeepClone();
                }
            }
            result.Add(item);
        }
        return result;
    }

    private static void SetSourcedCollectionValue(
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        ComponentInputDefinition input,
        string value)
    {
        var sourceItems = SourceCollectionItems(preview, collection).ToList();
        if (itemIndex < 0 || itemIndex >= sourceItems.Count)
        {
            return;
        }

        var id = JsonString(sourceItems[itemIndex], "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = itemIndex.ToString(CultureInfo.InvariantCulture);
        }

        var testValues = preview["testValues"] as JsonObject ?? new JsonObject();
        preview["testValues"] = testValues;
        var overrides = testValues[collection.JsonKey] as JsonArray ?? new JsonArray();
        testValues[collection.JsonKey] = overrides;
        var overrideItem = overrides.OfType<JsonObject>().FirstOrDefault((candidate) => JsonString(candidate, "id") == id);
        if (overrideItem is null)
        {
            overrideItem = new JsonObject { ["id"] = id };
            overrides.Add(overrideItem);
        }
        overrideItem[input.JsonKey] = ValueNode(input, value);
    }

    private static JsonObject CloneObject(JsonObject item)
    {
        return item.DeepClone() as JsonObject ?? new JsonObject();
    }

    private static string JsonString(JsonObject owner, string key)
    {
        return owner[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : "";
    }

    private static JsonNode? ValueNode(ComponentInputDefinition input, string value)
    {
        return input.Kind switch
        {
            ComponentInputKind.Number when double.TryParse(value.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
            ComponentInputKind.Boolean => JsonValue.Create(BooleanText.Parse(value)),
            ComponentInputKind.IconList => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value) ?? new JsonArray(),
            _ => JsonValue.Create(value),
        };
    }
}
