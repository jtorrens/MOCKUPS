using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DesignPreviewTestValues
{
    internal sealed record Difference(string Id, string Label);

    public static IReadOnlyList<Difference> Differences(
        JsonObject effectivePreview,
        JsonObject persistedPreview,
        IReadOnlyList<ComponentInputDefinition> inputs,
        IReadOnlyList<RuntimeInputCollectionDefinition> collections)
    {
        var differences = inputs
            .Where((input) => !string.Equals(Value(effectivePreview, input), Value(persistedPreview, input), StringComparison.Ordinal))
            .Select((input) => new Difference(input.Id, input.Label))
            .ToList();

        foreach (var collection in collections)
        {
            var current = CollectionItems(effectivePreview, collection);
            var baseline = CollectionItems(persistedPreview, collection);
            if (!JsonNode.DeepEquals(
                    new JsonArray(current.Select((item) => (JsonNode?)item.DeepClone()).ToArray()),
                    new JsonArray(baseline.Select((item) => (JsonNode?)item.DeepClone()).ToArray())))
            {
                differences.Add(new Difference(collection.JsonKey, collection.Label));
            }
        }

        return differences;
    }
    public static JsonObject Parse(string json)
    {
        return JsonPath.ParseRequiredObject(json, "Design Preview JSON");
    }

    public static string RuntimeJson(string previewJson)
    {
        var preview = Parse(previewJson);
        ApplyCollectionSources(preview);
        var sourcedCollectionKeys = SourcedCollectionKeys(preview);
        if (TestValues(preview) is not { } testValues)
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
        var testValues = TestValues(preview);
        if (testValues is not null
            && testValues.TryGetPropertyValue(input.JsonKey, out var testValue))
        {
            return CurrentValueText(
                testValue,
                input,
                $"Design Test Value '{input.JsonKey}'");
        }
        if (preview.TryGetPropertyValue(input.JsonKey, out var currentValue))
        {
            return CurrentValueText(
                currentValue,
                input,
                $"Design Preview Runtime value '{input.JsonKey}'");
        }
        return input.DefaultValue;
    }

    public static void SetValue(JsonObject preview, ComponentInputDefinition input, string value)
    {
        var testValues = TestValuesForWrite(preview);
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

        var testValues = TestValues(preview);
        var source = testValues is not null
                     && testValues.TryGetPropertyValue(collection.JsonKey, out var testNode)
            ? testNode as JsonArray
              ?? throw new InvalidOperationException(
                  $"Design Test Values collection '{collection.JsonKey}' must be an array.")
            : OptionalArray(preview, collection.JsonKey, "Design Preview collection");
        return CloneItems(source, $"Design Preview collection '{collection.JsonKey}'");
    }

    public static string CollectionValue(JsonObject item, ComponentInputDefinition input)
    {
        if (!item.TryGetPropertyValue(input.JsonKey, out var value))
        {
            return input.DefaultValue;
        }
        return CurrentValueText(
            value,
            input,
            $"Runtime collection value '{input.JsonKey}'");
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
        var testValues = TestValuesForWrite(preview);
        testValues[collection.JsonKey] = new JsonArray(items.Select((item) => (JsonNode?)item).ToArray());
    }

    public static void PromoteToDefaults(
        JsonObject preview,
        IReadOnlyList<ComponentInputDefinition> inputs,
        IReadOnlyList<RuntimeInputCollectionDefinition>? collections = null)
    {
        var contract = OptionalArray(preview, "inputs", "Design Preview Runtime inputs");
        foreach (var input in inputs)
        {
            var value = Value(preview, input);
            preview[input.JsonKey] = ValueNode(input, value);
            if (contract is null)
            {
                continue;
            }

            var definition = ObjectItems(contract, "Design Preview Runtime inputs")
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
            else if (TestValues(preview) is { } values
                     && values.TryGetPropertyValue(collection.JsonKey, out var itemsNode))
            {
                var items = itemsNode as JsonArray
                    ?? throw new InvalidOperationException(
                        $"Design Test Values collection '{collection.JsonKey}' must be an array.");
                preview[collection.JsonKey] = items.DeepClone();
            }
        }

        preview.Remove("testValues");
    }

    private static void ApplyCollectionSources(JsonObject preview)
    {
        var collections = OptionalArray(preview, "collections", "Design Preview Runtime collections");
        if (collections is null) return;
        foreach (var collectionNode in ObjectItems(collections, "Design Preview Runtime collections"))
        {
            var jsonKey = JsonString(collectionNode, "jsonKey");
            var sourceKey = JsonString(collectionNode, "sourceCollectionJsonKey");
            if (string.IsNullOrWhiteSpace(jsonKey))
            {
                throw new InvalidOperationException("Runtime collection definition requires a non-empty jsonKey.");
            }
            if (string.IsNullOrWhiteSpace(sourceKey)) continue;

            var sourceItems = MergeCollectionSource(
                RequiredArray(preview, sourceKey, "Design Preview source collection"),
                OptionalArray(TestValues(preview), jsonKey, "Design Test Values collection"),
                jsonKey);
            preview[jsonKey] = new JsonArray(sourceItems.Select((item) => (JsonNode?)item).ToArray());
        }
    }

    private static HashSet<string> SourcedCollectionKeys(JsonObject preview)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var collections = OptionalArray(preview, "collections", "Design Preview Runtime collections");
        foreach (var collectionNode in collections is null
                     ? []
                     : ObjectItems(collections, "Design Preview Runtime collections"))
        {
            var jsonKey = JsonString(collectionNode, "jsonKey");
            var sourceKey = JsonString(collectionNode, "sourceCollectionJsonKey");
            if (string.IsNullOrWhiteSpace(jsonKey))
            {
                throw new InvalidOperationException("Runtime collection definition requires a non-empty jsonKey.");
            }
            if (!string.IsNullOrWhiteSpace(sourceKey)) keys.Add(jsonKey);
        }
        return keys;
    }

    private static IReadOnlyList<JsonObject> SourceCollectionItems(
        JsonObject preview,
        RuntimeInputCollectionDefinition collection)
    {
        return MergeCollectionSource(
            RequiredArray(preview, collection.SourceCollectionJsonKey, "Design Preview source collection"),
            OptionalArray(TestValues(preview), collection.JsonKey, "Design Test Values collection"),
            collection.JsonKey);
    }

    private static IReadOnlyList<JsonObject> MergeCollectionSource(
        JsonArray source,
        JsonArray? overrides,
        string collectionJsonKey)
    {
        RuntimeCollectionDocumentContract.Validate(
            source,
            $"Design Preview source collection '{collectionJsonKey}'");
        if (overrides is not null)
        {
            RuntimeCollectionDocumentContract.Validate(
                overrides,
                $"Design Test Values collection '{collectionJsonKey}'");
        }
        var overrideById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var item in overrides is null
                     ? []
                     : ObjectItems(overrides, $"Design Test Values collection '{collectionJsonKey}'"))
        {
            var id = JsonString(item, "id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                overrideById[id] = item;
            }
        }

        var result = new List<JsonObject>();
        foreach (var sourceItem in ObjectItems(
                     source,
                     $"Design Preview source collection '{collectionJsonKey}'"))
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
            throw new InvalidOperationException(
                $"Design Preview source collection '{collection.JsonKey}' item requires a stable id.");
        }

        var testValues = TestValuesForWrite(preview);
        var overrides = ArrayForWrite(
            testValues,
            collection.JsonKey,
            "Design Test Values collection");
        RuntimeCollectionDocumentContract.Validate(
            overrides,
            $"Design Test Values collection '{collection.JsonKey}'");
        var overrideItem = ObjectItems(overrides, $"Design Test Values collection '{collection.JsonKey}'")
            .FirstOrDefault((candidate) => JsonString(candidate, "id") == id);
        if (overrideItem is null)
        {
            overrideItem = new JsonObject { ["id"] = id };
            overrides.Add(overrideItem);
        }
        overrideItem[input.JsonKey] = ValueNode(input, value);
    }

    private static JsonObject CloneObject(JsonObject item)
    {
        return item.DeepClone().AsObject();
    }

    private static IReadOnlyList<JsonObject> CloneItems(JsonArray? items, string owner)
    {
        if (items is null) return [];
        RuntimeCollectionDocumentContract.Validate(items, owner);
        return ObjectItems(items, owner).Select(CloneObject).ToList();
    }

    private static JsonObject? TestValues(JsonObject preview)
    {
        if (!preview.TryGetPropertyValue("testValues", out var node)) return null;
        return node as JsonObject
            ?? throw new InvalidOperationException("Design Test Values must be an object when present.");
    }

    private static JsonObject TestValuesForWrite(JsonObject preview)
    {
        if (TestValues(preview) is { } existing) return existing;
        var created = new JsonObject();
        preview["testValues"] = created;
        return created;
    }

    private static JsonArray? OptionalArray(JsonObject? owner, string key, string context)
    {
        if (owner is null || !owner.TryGetPropertyValue(key, out var node)) return null;
        return node as JsonArray
            ?? throw new InvalidOperationException($"{context} '{key}' must be an array when present.");
    }

    private static JsonArray RequiredArray(JsonObject owner, string key, string context)
    {
        return OptionalArray(owner, key, context)
            ?? throw new InvalidOperationException($"{context} '{key}' is required.");
    }

    private static JsonArray ArrayForWrite(JsonObject owner, string key, string context)
    {
        if (OptionalArray(owner, key, context) is { } existing) return existing;
        var created = new JsonArray();
        owner[key] = created;
        return created;
    }

    private static IEnumerable<JsonObject> ObjectItems(JsonArray items, string context)
    {
        for (var index = 0; index < items.Count; index++)
        {
            yield return items[index] as JsonObject
                ?? throw new InvalidOperationException($"{context} item at index {index} must be an object.");
        }
    }

    private static string JsonString(JsonObject owner, string key)
    {
        return owner[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : "";
    }

    internal static JsonNode? ValueNode(ComponentInputDefinition input, string value)
    {
        return RuntimeInputValueKindContract.ParseValue(
            input.ValueKind,
            value,
            $"Runtime Input '{input.Id}' value");
    }

    private static string CurrentValueText(
        JsonNode? value,
        ComponentInputDefinition input,
        string owner)
    {
        if (value is null)
        {
            throw new InvalidOperationException($"{owner} cannot be null.");
        }
        return RuntimeInputValueKindContract.CurrentStorageText(input.ValueKind, value, owner);
    }
}
