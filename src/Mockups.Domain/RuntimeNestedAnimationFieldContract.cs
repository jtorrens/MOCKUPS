using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

public sealed record RuntimeNestedAnimationField(
    string FieldId,
    JsonObject Definition,
    JsonObject Values);

public static class RuntimeNestedAnimationFieldContract
{
    public static IReadOnlyList<RuntimeNestedAnimationField> CollectionItemFields(
        JsonObject collection,
        JsonObject item)
    {
        var result = new List<RuntimeNestedAnimationField>();
        AddFields(result, Fields(collection), item, "");
        AddItemRuntimeContractFields(result, collection, item, "");
        return result;
    }

    public static string Join(params string[] segments) =>
        string.Join('.', segments.Where((segment) => !string.IsNullOrWhiteSpace(segment)));

    private static void AddFields(
        ICollection<RuntimeNestedAnimationField> result,
        IReadOnlyList<JsonObject> fields,
        JsonObject values,
        string prefix)
    {
        foreach (var field in fields)
        {
            var id = RequiredString(field, "id", "Runtime animation field");
            var fieldId = Join(prefix, id);
            result.Add(new RuntimeNestedAnimationField(
                fieldId,
                WithId(field, fieldId),
                values));

            if (field["structuredCollection"] is not JsonObject nestedCollection)
            {
                continue;
            }
            var jsonKey = RequiredString(
                field,
                "jsonKey",
                $"Runtime structured animation field '{fieldId}'");
            if (values[jsonKey] is not JsonArray nestedItems) continue;

            foreach (var nestedItem in ObjectItems(
                         nestedItems,
                         $"Runtime structured collection '{fieldId}'"))
            {
                var nestedItemId = RequiredString(
                    nestedItem,
                    "id",
                    $"Runtime structured collection '{fieldId}' item");
                var nestedPrefix = Join(fieldId, nestedItemId);
                AddFields(result, Fields(nestedCollection), nestedItem, nestedPrefix);
                AddItemRuntimeContractFields(
                    result,
                    nestedCollection,
                    nestedItem,
                    nestedPrefix);
            }
        }
    }

    private static void AddItemRuntimeContractFields(
        ICollection<RuntimeNestedAnimationField> result,
        JsonObject collection,
        JsonObject item,
        string prefix)
    {
        var runtimeKey = OptionalString(collection, "itemRuntimeContractJsonKey");
        if (runtimeKey.Length == 0 && collection["componentItems"] is JsonObject componentItems)
        {
            runtimeKey = OptionalString(componentItems, "inputsJsonKey");
        }
        if (runtimeKey.Length == 0) return;

        var runtime = item[runtimeKey] as JsonObject
            ?? throw new InvalidOperationException(
                $"Runtime collection item '{OptionalString(item, "id")}' requires object '{runtimeKey}'.");
        AddFields(result, Inputs(runtime), runtime, prefix);
    }

    private static IReadOnlyList<JsonObject> Fields(JsonObject collection) =>
        OptionalObjectArray(collection, "fields", "Runtime collection fields");

    private static IReadOnlyList<JsonObject> Inputs(JsonObject runtime) =>
        OptionalObjectArray(runtime, "inputs", "Embedded Runtime inputs");

    private static IReadOnlyList<JsonObject> OptionalObjectArray(
        JsonObject owner,
        string key,
        string path)
    {
        if (!owner.TryGetPropertyValue(key, out var node)) return [];
        return node is JsonArray array
            ? ObjectItems(array, path)
            : throw new InvalidOperationException($"{path} must be an array when present.");
    }

    private static IReadOnlyList<JsonObject> ObjectItems(JsonArray? array, string owner)
    {
        if (array is null) return [];
        return array.Select((node, index) => node as JsonObject
                ?? throw new InvalidOperationException($"{owner} item at index {index} must be an object."))
            .ToArray();
    }

    private static JsonObject WithId(JsonObject definition, string fieldId)
    {
        var result = definition.DeepClone().AsObject();
        result["id"] = fieldId;
        return result;
    }

    private static string RequiredString(JsonObject owner, string key, string path) =>
        owner[key]?.GetValue<string>() is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{path} requires non-empty '{key}'.");

    private static string OptionalString(JsonObject? owner, string key = "") =>
        owner is not null
        && key.Length > 0
        && owner[key] is JsonValue value
        && value.TryGetValue<string>(out var text)
            ? text
            : "";
}
