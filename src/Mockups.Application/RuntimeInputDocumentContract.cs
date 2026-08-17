using Mockups.DesktopEditorShell.Common;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class RuntimeInputDocumentContract
{
    public static bool IsRuntimeDefinition(JsonObject definition)
    {
        if (!definition.TryGetPropertyValue("source", out var node))
        {
            return true;
        }

        if (node is not JsonValue value
            || !value.TryGetValue<string>(out var source))
        {
            throw new InvalidOperationException(
                "Runtime Input definition source must be a string when present.");
        }

        return source switch
        {
            "runtime" => true,
            "variant" or "calculated" => false,
            _ => throw new InvalidOperationException(
                $"Runtime Input definition has unknown source '{source}'."),
        };
    }

    public static string CollectionStorageKey(JsonObject collection)
    {
        foreach (var key in new[]
                 {
                     "storageCollectionJsonKey",
                     "sourceCollectionJsonKey",
                     "jsonKey",
                 })
        {
            if (!collection.ContainsKey(key))
            {
                continue;
            }

            return JsonPath.RequiredString(
                collection,
                key,
                "Runtime collection definition");
        }

        throw new InvalidOperationException(
            "Runtime collection definition requires a storage key.");
    }

    public static JsonArray ReconcileProjectedCollection(
        JsonArray? current,
        JsonArray? defaults,
        JsonObject? collectionDefinition = null)
    {
        if (defaults is null)
        {
            throw new InvalidOperationException(
                "Projected runtime collection has no contract defaults.");
        }

        RuntimeCollectionDocumentContract.Validate(
            defaults,
            "Projected runtime collection defaults");
        if (current is not null)
        {
            RuntimeCollectionDocumentContract.Validate(
                current,
                "Projected runtime collection content");
        }

        var currentById =
            new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (current is not null)
        {
            for (var index = 0; index < current.Count; index++)
            {
                var item = current[index] as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Projected runtime collection content item at index {index} must be an object.");
                currentById.Add(
                    JsonPath.RequiredString(
                        item,
                        "id",
                        $"Projected runtime collection content item at index {index}"),
                    item);
            }
        }

        var result = new JsonArray();
        var structureOwnedKeys = StructureOwnedKeys(collectionDefinition);
        for (var index = 0; index < defaults.Count; index++)
        {
            var defaultItem = defaults[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Projected runtime collection default item at index {index} must be an object.");
            var next = defaultItem.DeepClone().AsObject();
            if (collectionDefinition is not null)
            {
                var persistedKeys = DefinitionObjects(
                        collectionDefinition,
                        "fields",
                        "Projected runtime collection",
                        required: true)
                    .Where(IsRuntimeDefinition)
                    .Select((field) => JsonPath.RequiredString(
                        field,
                        "jsonKey",
                        "Projected runtime collection field"))
                    .Append("id")
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var key in next
                             .Select((entry) => entry.Key)
                             .Where((key) => !persistedKeys.Contains(key))
                             .ToList())
                {
                    next.Remove(key);
                }
            }
            var id = JsonPath.RequiredString(
                next,
                "id",
                $"Projected runtime collection default item at index {index}");
            if (currentById.TryGetValue(id, out var currentItem))
            {
                foreach (var key in next
                             .Select((entry) => entry.Key)
                             .Where((key) => !structureOwnedKeys.Contains(key))
                             .ToList())
                {
                    if (currentItem[key] is { } value)
                    {
                        next[key] = value.DeepClone();
                    }
                }
            }

            result.Add(next);
        }

        return result;
    }

    private static IReadOnlySet<string> StructureOwnedKeys(
        JsonObject? collectionDefinition)
    {
        if (collectionDefinition?["structureProjection"] is not JsonObject projection)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var bindings = JsonPath.RequiredObject(
            projection,
            "fieldBindings",
            "Runtime collection structureProjection");
        return bindings
            .Select((entry) => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static JsonObject CreateContentForContract(
        JsonObject current,
        JsonObject contract)
    {
        var next = new JsonObject
        {
            ["schemaVersion"] = current["schemaVersion"]?.DeepClone()
                ?? JsonValue.Create(2),
        };
        foreach (var (key, value) in CreateInputValuesForContract(
                     current,
                     contract,
                     preserveForwarding: false))
        {
            next[key] = value?.DeepClone();
        }
        foreach (var collection in DefinitionObjects(
                     contract,
                     "collections",
                     "Effective Module Runtime contract"))
        {
            var storageKey = CollectionStorageKey(collection);
            next[storageKey] =
                collection.ContainsKey("storageCollectionJsonKey")
                    ? ReconcileProjectedCollection(
                        OptionalCollection(
                            current,
                            storageKey,
                            "Current Module Instance content"),
                        OptionalCollection(
                            contract,
                            JsonPath.RequiredString(
                                collection,
                                "jsonKey",
                                "Runtime collection definition"),
                            "Effective Module Runtime contract"))
                    : (OptionalCollection(
                            current,
                            storageKey,
                            "Current Module Instance content")
                        ?? new JsonArray()).DeepClone();
        }

        return next;
    }

    public static JsonObject CreateInputValuesForContract(
        JsonObject current,
        JsonObject contract,
        bool preserveForwarding = true,
        IReadOnlySet<string>? excludedInputIds = null)
    {
        var next = new JsonObject();
        foreach (var input in DefinitionObjects(
                     contract,
                     "inputs",
                     "Effective Module Runtime contract"))
        {
            if (!IsRuntimeDefinition(input))
            {
                continue;
            }

            var inputId = JsonPath.RequiredString(
                input,
                "id",
                "Runtime Input definition");
            if (excludedInputIds?.Contains(inputId) == true)
            {
                continue;
            }
            var jsonKey = JsonPath.RequiredString(
                input,
                "jsonKey",
                $"Runtime Input '{inputId}'");
            var collection = input["structuredCollection"] as JsonObject;
            var projectedCollection =
                collection?["structureProjection"] is not null;
            if (projectedCollection)
            {
                var value = ReconcileProjectedCollection(
                    OptionalCollection(
                        current,
                        jsonKey,
                        "Current Component Input bindings"),
                    RequiredCollection(
                        contract,
                        jsonKey,
                        "Effective Component Runtime contract"),
                    collection!);
                RuntimeInputValueKindContract.ValidateRuntimeValue(
                    input,
                    value,
                    $"Current Runtime Input '{inputId}'");
                next[jsonKey] = value;
                continue;
            }
            if (current.TryGetPropertyValue(
                    jsonKey,
                    out var currentValue))
            {
                RuntimeInputValueKindContract.ValidateRuntimeValue(
                    input,
                    currentValue,
                    $"Current Runtime Input '{inputId}'");
                next[jsonKey] = currentValue!.DeepClone();
            }
            else
            {
                next[jsonKey] =
                    RuntimeInputValueKindContract.CreateDefaultValue(
                        input,
                        $"Runtime Input '{inputId}'");
            }
        }

        if (preserveForwarding
            && current[RuntimeInputForwardingContract.StorageKey]
                is JsonObject forwarding)
        {
            next[RuntimeInputForwardingContract.StorageKey] =
                forwarding.DeepClone();
        }

        return next;
    }

    public static JsonObject ProjectInputValuesForContract(
        JsonObject current,
        JsonObject contract,
        IReadOnlySet<string>? excludedInputIds = null)
    {
        var inputs = DefinitionObjects(
                contract,
                "inputs",
                "Effective Component Runtime contract")
            .Where(IsRuntimeDefinition)
            .Where((input) => excludedInputIds?.Contains(
                JsonPath.RequiredString(
                    input,
                    "id",
                    "Runtime Input definition")) != true)
            .ToList();
        var collections = DefinitionObjects(
                contract,
                "collections",
                "Effective Component Runtime contract")
            .ToList();
        var expected = inputs
            .Select((input) => JsonPath.RequiredString(
                input,
                "jsonKey",
                "Runtime Input definition"))
            .Concat(collections.Select(CollectionStorageKey))
            .ToHashSet(StringComparer.Ordinal);
        var actual = current
            .Select((entry) => entry.Key)
            .Where((key) => !key.Equals(
                RuntimeInputForwardingContract.StorageKey,
                StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var missing = expected.Except(actual, StringComparer.Ordinal).ToList();
        var unknown = actual.Except(expected, StringComparer.Ordinal).ToList();
        if (missing.Count > 0 || unknown.Count > 0)
        {
            throw new InvalidOperationException(
                "Component Input bindings do not match the selected Variant Runtime contract."
                + (missing.Count > 0
                    ? $" Missing: {string.Join(", ", missing)}."
                    : "")
                + (unknown.Count > 0
                    ? $" Unknown: {string.Join(", ", unknown)}."
                    : ""));
        }
        foreach (var input in inputs)
        {
            var inputId = JsonPath.RequiredString(
                input,
                "id",
                "Runtime Input definition");
            if (excludedInputIds?.Contains(inputId) == true)
            {
                continue;
            }
            var jsonKey = JsonPath.RequiredString(
                input,
                "jsonKey",
                $"Runtime Input '{inputId}'");
            RuntimeInputValueKindContract.ValidateRuntimeValue(
                input,
                current[jsonKey],
                $"Current Runtime Input '{inputId}'");
        }
        var result = CreateInputValuesForContract(
            current,
            contract,
            excludedInputIds: excludedInputIds);
        foreach (var collection in collections)
        {
            var storageKey = CollectionStorageKey(collection);
            var currentCollection = RequiredCollection(
                current,
                storageKey,
                "Current Component Input bindings");
            RuntimeCollectionDocumentContract.Validate(
                currentCollection,
                $"Current Runtime collection '{storageKey}'");
            result[storageKey] = collection.ContainsKey(
                    "storageCollectionJsonKey")
                ? ReconcileProjectedCollection(
                    currentCollection,
                    RequiredCollection(
                        contract,
                        JsonPath.RequiredString(
                            collection,
                            "jsonKey",
                            "Runtime collection definition"),
                        "Effective Component Runtime contract"),
                    collection)
                : currentCollection.DeepClone();
        }
        return result;
    }

    public static JsonObject RemoveOrphanedAnimationTracks(
        JsonObject animation,
        JsonObject contract,
        JsonObject content)
    {
        var topLevelFields = DefinitionObjects(
                contract,
                "inputs",
                "Effective Module Runtime contract")
            .Where(IsRuntimeDefinition)
            .Select((input) => JsonPath.RequiredString(
                input,
                "id",
                "Runtime Input definition"))
            .ToHashSet(StringComparer.Ordinal);
        var targetIds = new HashSet<string>(StringComparer.Ordinal);
        CollectObjectIds(content, targetIds);
        if (animation["tracks"] is JsonArray tracks)
        {
            for (var index = tracks.Count - 1; index >= 0; index--)
            {
                if (tracks[index] is not JsonObject track)
                {
                    continue;
                }

                var targetId =
                    track["targetId"]?.GetValue<string>() ?? "";
                var fieldId =
                    track["fieldId"]?.GetValue<string>() ?? "";
                if ((!string.IsNullOrWhiteSpace(targetId)
                        && !targetIds.Contains(targetId))
                    || (string.IsNullOrWhiteSpace(targetId)
                        && !topLevelFields.Contains(fieldId)))
                {
                    tracks.RemoveAt(index);
                }
            }
        }

        return animation;
    }

    public static IReadOnlyList<JsonObject> DefinitionObjects(
        JsonObject owner,
        string key,
        string context,
        bool required = false)
    {
        var array = DefinitionArray(
            owner,
            key,
            context,
            required);
        if (array is null)
        {
            return [];
        }

        var definitions = new List<JsonObject>(array.Count);
        for (var index = 0; index < array.Count; index++)
        {
            definitions.Add(
                array[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{context} {key}[{index}] must be an object."));
        }

        return definitions;
    }

    public static JsonArray? OptionalCollection(
        JsonObject owner,
        string key,
        string context)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"{context} has an empty runtime collection key.");
        }

        if (owner[key] is null)
        {
            return null;
        }

        return owner[key] as JsonArray
            ?? throw new InvalidOperationException(
                $"{context} '{key}' must be an array.");
    }

    public static JsonArray RequiredCollection(
        JsonObject owner,
        string key,
        string context) =>
        OptionalCollection(owner, key, context)
        ?? throw new InvalidOperationException(
            $"{context} requires runtime collection '{key}'.");

    public static void ValidateCurrentValues(
        JsonObject contract,
        JsonObject content,
        string owner)
    {
        var inputs = DefinitionObjects(
            contract,
            "inputs",
            $"{owner} effective Runtime contract");
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var jsonKey = JsonPath.RequiredString(
                input,
                "jsonKey",
                $"{owner} Runtime Input at index {index}");
            if (IsRuntimeDefinition(input))
            {
                if (!content.TryGetPropertyValue(
                        jsonKey,
                        out var value))
                {
                    throw new InvalidOperationException(
                        $"{owner} requires runtime input '{jsonKey}'.");
                }

                RuntimeInputValueKindContract.ValidateRuntimeValue(
                    input,
                    value,
                    $"{owner} runtime input '{jsonKey}'");
            }
            else if (content.ContainsKey(jsonKey))
            {
                throw new InvalidOperationException(
                    $"{owner} must not persist parent-owned input '{jsonKey}'.");
            }
        }
    }

    public static void ValidateCurrentCollections(
        JsonObject contract,
        JsonObject content,
        string owner)
    {
        var collections = DefinitionObjects(
            contract,
            "collections",
            $"{owner} effective Runtime contract");
        var storageKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < collections.Count; index++)
        {
            var collection = collections[index];
            var storageKey = CollectionStorageKey(collection);
            if (string.IsNullOrWhiteSpace(storageKey)
                || !storageKeys.Add(storageKey))
            {
                throw new InvalidOperationException(
                    $"{owner} has a missing or duplicate Runtime collection storage key '{storageKey}'.");
            }

            var items = RequiredCollection(content, storageKey, owner);
            RuntimeCollectionDocumentContract.Validate(
                items,
                $"{owner} runtime collection '{storageKey}'");
            var componentItems =
                RuntimeComponentCollectionItemDocumentContract
                    .ReadDefinition(
                        collection,
                        $"{owner} runtime collection '{storageKey}'");
            var fields = DefinitionObjects(
                collection,
                "fields",
                $"{owner} runtime collection '{storageKey}'",
                required: true);
            for (var itemIndex = 0;
                 itemIndex < items.Count;
                 itemIndex++)
            {
                var item = items[itemIndex] as JsonObject
                    ?? throw new InvalidOperationException(
                        $"{owner} runtime collection '{storageKey}' item at index {itemIndex} must be an object.");
                var itemId = JsonPath.RequiredString(
                    item,
                    "id",
                    $"{owner} runtime collection '{storageKey}' item at index {itemIndex}");
                if (componentItems is not null)
                {
                    RuntimeComponentCollectionItemDocumentContract
                        .ValidateItem(
                            item,
                            componentItems,
                            $"{owner} runtime collection '{storageKey}' item '{itemId}'");
                }

                for (var fieldIndex = 0;
                     fieldIndex < fields.Count;
                     fieldIndex++)
                {
                    var field = fields[fieldIndex];
                    var fieldKey = JsonPath.RequiredString(
                        field,
                        "jsonKey",
                        $"{owner} runtime collection '{storageKey}' field at index {fieldIndex}");
                    if (IsRuntimeDefinition(field))
                    {
                        if (!item.TryGetPropertyValue(
                                fieldKey,
                                out var value))
                        {
                            throw new InvalidOperationException(
                                $"{owner} runtime collection '{storageKey}' item '{itemId}' requires field '{fieldKey}'.");
                        }

                        RuntimeInputValueKindContract
                            .ValidateRuntimeValue(
                                field,
                                value,
                                $"{owner} runtime collection '{storageKey}' item '{itemId}' field '{fieldKey}'");
                    }
                    else if (item.ContainsKey(fieldKey))
                    {
                        throw new InvalidOperationException(
                            $"{owner} runtime collection '{storageKey}' item '{itemId}' must not persist parent-owned field '{fieldKey}'.");
                    }
                }
            }
        }
    }

    private static JsonArray? DefinitionArray(
        JsonObject owner,
        string key,
        string context,
        bool required)
    {
        if (!owner.TryGetPropertyValue(key, out var node))
        {
            if (!required)
            {
                return null;
            }

            throw new InvalidOperationException(
                $"{context} requires a {key} definition array.");
        }

        return node as JsonArray
            ?? throw new InvalidOperationException(
                $"{context} {key} must be an array when present.");
    }

    private static void CollectObjectIds(
        JsonNode? node,
        ISet<string> ids)
    {
        if (node is JsonObject value)
        {
            if (value["id"]?.GetValue<string>() is { Length: > 0 } id)
            {
                ids.Add(id);
            }

            foreach (var child in value.Select((entry) => entry.Value))
            {
                CollectObjectIds(child, ids);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                CollectObjectIds(child, ids);
            }
        }
    }
}
