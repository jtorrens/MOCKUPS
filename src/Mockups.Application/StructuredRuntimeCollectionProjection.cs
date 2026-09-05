using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class StructuredRuntimeCollectionProjection
{
    public static bool Apply(JsonObject preview, JsonObject config)
    {
        var changed = false;
        if (preview["inputs"] is { } inputsNode)
        {
            var inputs = inputsNode as JsonArray
                ?? throw new InvalidOperationException(
                    "Runtime Input definitions must be an array when present.");
            for (var index = 0; index < inputs.Count; index++)
            {
                var input = inputs[index] as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Runtime Input definition at index {index} must be an object.");
                if (input["structuredCollection"] is not JsonObject collection
                    || collection["structureProjection"] is null)
                {
                    continue;
                }

                var owner = $"Runtime Input '{JsonPath.RequiredString(input, "id", "Runtime Input definition")}'";
                if (!JsonPath.RequiredString(input, "valueKind", owner)
                        .Equals(ValueKind.StructuredCollection.ToString(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{owner} structureProjection requires ValueKind StructuredCollection.");
                }
                changed |= ApplyProjection(preview, config, collection, owner);
            }
        }

        if (preview["collections"] is { } collectionsNode)
        {
            var collections = collectionsNode as JsonArray
                ?? throw new InvalidOperationException(
                    "Runtime Input collections must be an array when present.");
            for (var index = 0; index < collections.Count; index++)
            {
                var collection = collections[index] as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Runtime Input collection at index {index} must be an object.");
                if (collection["structureProjection"] is null) continue;
                var owner = $"Runtime Input collection '{JsonPath.RequiredString(collection, "id", "Runtime Input collection")}'";
                if (collection["canEditStructure"]?.GetValue<bool>() != false)
                {
                    throw new InvalidOperationException(
                        $"{owner} structureProjection requires canEditStructure false.");
                }
                changed |= ApplyProjection(preview, config, collection, owner);
            }
        }
        return changed;
    }

    private static bool ApplyProjection(
        JsonObject preview,
        JsonObject config,
        JsonObject collection,
        string owner)
    {
        var jsonKey = JsonPath.RequiredString(collection, "jsonKey", owner);
        var current = preview[jsonKey] as JsonArray
            ?? throw new InvalidOperationException(
                $"{owner} Runtime value '{jsonKey}' must be an array.");
        var next = Project(
            current,
            config,
            collection,
            $"{owner} structured collection");
        if (JsonNode.DeepEquals(current, next)) return false;
        preview[jsonKey] = next;
        return true;
    }

    private static JsonArray Project(
        JsonArray current,
        JsonObject config,
        JsonObject collection,
        string owner)
    {
        var projection = JsonPath.RequiredObject(
            collection,
            "structureProjection",
            owner);
        RequireExactKeys(
            projection,
            [
                "sourceConfigPath",
                "sourceIdJsonKey",
                "runtimeIdJsonKey",
                "fieldBindings",
            ],
            $"{owner}.structureProjection");
        var sourcePath = JsonPath.RequiredString(
            projection,
            "sourceConfigPath",
            $"{owner}.structureProjection");
        var sourceIdJsonKey = JsonPath.RequiredString(
            projection,
            "sourceIdJsonKey",
            $"{owner}.structureProjection");
        var runtimeIdJsonKey = JsonPath.RequiredString(
            projection,
            "runtimeIdJsonKey",
            $"{owner}.structureProjection");
        var bindings = JsonPath.RequiredObject(
            projection,
            "fieldBindings",
            $"{owner}.structureProjection");
        var source = JsonPath.Get(
            config,
            sourcePath.Split('.', StringSplitOptions.RemoveEmptyEntries)) as JsonArray
            ?? throw new InvalidOperationException(
                $"{owner} structureProjection sourceConfigPath '{sourcePath}' must resolve to an array.");
        var fields = JsonPath.ObjectItems(
                JsonPath.RequiredArray(collection, "fields", owner),
                $"{owner}.fields")
            .ToList();
        var fieldsByJsonKey = fields.ToDictionary(
            (field) => JsonPath.RequiredString(field, "jsonKey", $"{owner}.field"),
            StringComparer.Ordinal);
        var typedFieldsByJsonKey = RuntimeInputDefinitionReader.ReadCollections(
                new JsonObject
                {
                    ["collections"] = new JsonArray(collection.DeepClone()),
                },
                new JsonObject(),
                includeHidden: true)
            .Single()
            .Fields
            .ToDictionary((field) => field.JsonKey, StringComparer.Ordinal);
        var sourceKeyByRuntimeKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (runtimeKey, node) in bindings)
        {
            if (!fieldsByJsonKey.ContainsKey(runtimeKey)
                || node is not JsonValue value
                || !value.TryGetValue<string>(out var sourceKey)
                || string.IsNullOrWhiteSpace(sourceKey))
            {
                throw new InvalidOperationException(
                    $"{owner} structureProjection field binding '{runtimeKey}' is invalid.");
            }
            sourceKeyByRuntimeKey.Add(runtimeKey, sourceKey);
        }

        RuntimeCollectionDocumentContract.Validate(current, $"{owner} Runtime values");
        var currentById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        for (var index = 0; index < current.Count; index++)
        {
            var item = current[index]!.AsObject();
            var id = JsonPath.RequiredString(
                item,
                runtimeIdJsonKey,
                $"{owner} Runtime item at index {index}");
            if (!currentById.TryAdd(id, item))
            {
                throw new InvalidOperationException(
                    $"{owner} Runtime item id '{id}' is duplicated.");
            }
        }

        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        var result = new JsonArray();
        for (var index = 0; index < source.Count; index++)
        {
            var sourceItem = source[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{owner} structural item at index {index} must be an object.");
            var id = JsonPath.RequiredString(
                sourceItem,
                sourceIdJsonKey,
                $"{owner} structural item at index {index}");
            if (!sourceIds.Add(id))
            {
                throw new InvalidOperationException(
                    $"{owner} structural item id '{id}' is duplicated.");
            }

            currentById.TryGetValue(id, out var currentItem);
            var projected = new JsonObject
            {
                [runtimeIdJsonKey] = id,
            };
            foreach (var (runtimeKey, definition) in fieldsByJsonKey)
            {
                var typedDefinition = typedFieldsByJsonKey[runtimeKey];
                if (definition["source"] is JsonValue fieldSourceValue
                    && fieldSourceValue.TryGetValue<string>(out var fieldSource)
                    && fieldSource.Equals("calculated", StringComparison.Ordinal))
                {
                    continue;
                }
                JsonNode value;
                // A declared structure binding is authored by the selected
                // Variant (including its local Override).  Only fields without
                // a binding are Runtime-owned and may retain their current
                // value when the structure is reconciled.
                if (sourceKeyByRuntimeKey.TryGetValue(runtimeKey, out var sourceKey)
                    && sourceItem[sourceKey] is { } sourceValue)
                {
                    value = sourceValue.DeepClone();
                }
                else if (currentItem?[runtimeKey] is { } currentValue)
                {
                    value = typedDefinition.ValueKind == ValueKind.StructuredCollection
                        ? StructuredCollectionDocumentContract.StoredClone(
                            currentValue as JsonArray
                                ?? throw new InvalidOperationException(
                                    $"{owner} Runtime item '{id}' field '{runtimeKey}' must be an array."),
                            typedDefinition.StructuredCollection
                                ?? throw new InvalidOperationException(
                                    $"{owner} Runtime item '{id}' field '{runtimeKey}' requires a collection contract."),
                            $"{owner} Runtime item '{id}' field '{runtimeKey}'")
                        : currentValue.DeepClone();
                }
                else
                {
                    value = RuntimeInputValueKindContract.CreateDefaultValue(
                        definition,
                        $"{owner} field '{runtimeKey}'");
                }
                RuntimeInputValueKindContract.ValidateRuntimeValue(
                    definition,
                    value,
                    $"{owner} Runtime item '{id}' field '{runtimeKey}'");
                projected[runtimeKey] = value;
            }
            result.Add(projected);
        }
        return result;
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
}
