using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private void ReconcileModuleInstanceRuntimePayload(
        SqliteConnection connection,
        string moduleInstanceId)
    {
        var instance = _moduleInstanceRepository.Get(connection, moduleInstanceId);
        var module = _appModuleRepository.GetModule(connection, instance.ModuleId);
        var original = instance.ContentJson;
        var content = ParseJsonObject(original);
        var contract = EffectiveModuleInstanceContract(
            module.Id,
            module.MetadataJson,
            instance.MetadataJson,
            module.DesignPreviewJson);
        foreach (var input in RuntimeDefinitionObjects(
                     contract,
                     "inputs",
                     $"Module Instance '{moduleInstanceId}' effective Runtime contract"))
        {
            var inputId = JsonPath.RequiredString(input, "id", "Runtime Input definition");
            var jsonKey = JsonPath.RequiredString(input, "jsonKey", $"Runtime Input '{inputId}'");
            if (!RuntimeInputDefinition(input))
            {
                content.Remove(jsonKey);
                continue;
            }
            if (!content.TryGetPropertyValue(jsonKey, out var currentValue))
            {
                content[jsonKey] = RuntimeInputValueKindContract.CreateDefaultValue(
                    input,
                    $"Runtime Input '{inputId}'");
                continue;
            }
            RuntimeInputValueKindContract.ValidateRuntimeValue(
                input,
                currentValue,
                $"Module Instance '{moduleInstanceId}' Runtime Input '{inputId}'");
        }

        foreach (var collection in RuntimeDefinitionObjects(
                     contract,
                     "collections",
                     $"Module Instance '{moduleInstanceId}' effective Runtime contract"))
        {
            var storageKey = RuntimeCollectionStorageKey(collection);
            var projected = collection.ContainsKey("storageCollectionJsonKey");
            var items = projected
                ? ReconcileProjectedRuntimeCollection(
                    OptionalRuntimeCollection(content, storageKey, $"Module Instance '{moduleInstanceId}' content_json"),
                    OptionalRuntimeCollection(
                        contract,
                        JsonPath.RequiredString(collection, "jsonKey", "Runtime collection definition"),
                        $"Module Instance '{moduleInstanceId}' effective Runtime contract"))
                : OptionalRuntimeCollection(content, storageKey, $"Module Instance '{moduleInstanceId}' content_json")
                    ?? new JsonArray();
            content[storageKey] = items;
            RuntimeCollectionDocumentContract.Validate(
                items,
                $"Module Instance '{moduleInstanceId}' runtime collection '{storageKey}'");
            var fields = RuntimeDefinitionObjects(
                collection,
                "fields",
                $"Runtime collection '{storageKey}'",
                required: true);
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                var item = items[itemIndex] as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Runtime collection '{storageKey}' item at index {itemIndex} must be an object.");
                foreach (var field in fields)
                {
                    if (!RuntimeInputDefinition(field)) continue;
                    var fieldId = JsonPath.RequiredString(field, "id", $"Runtime collection '{storageKey}' field");
                    var jsonKey = JsonPath.RequiredString(
                        field,
                        "jsonKey",
                        $"Runtime collection '{storageKey}' field '{fieldId}'");
                    if (!item.TryGetPropertyValue(jsonKey, out var currentValue))
                    {
                        item[jsonKey] = RuntimeInputValueKindContract.CreateDefaultValue(
                            field,
                            $"Runtime collection field '{fieldId}'");
                        continue;
                    }
                    RuntimeInputValueKindContract.ValidateRuntimeValue(
                        field,
                        currentValue,
                        $"Runtime collection '{storageKey}' item field '{fieldId}'");
                }
            }
        }

        var next = content.ToJsonString();
        if (next == original) return;
        ValidateModuleInstanceRuntimeContent(connection, moduleInstanceId, content);
        _moduleInstanceRepository.UpdateContent(connection, moduleInstanceId, next);
    }

    private void SynchronizeTimelineDurations(SqliteConnection connection, string? shotId = null)
    {
        var instances = shotId is null
            ? _moduleInstanceRepository.QueryAll(connection)
            : _moduleInstanceRepository.QueryByShot(connection, shotId);
        var modules = _appModuleRepository.QueryModules(connection)
            .ToDictionary((module) => module.Id, StringComparer.Ordinal);
        var updates = new List<(string Id, int Duration)>();
        foreach (var instance in instances)
        {
            if (!modules.TryGetValue(instance.ModuleId, out var module))
            {
                throw new InvalidOperationException($"Missing module '{instance.ModuleId}'.");
            }
            var contract = EffectiveModuleInstanceContract(
                module.Id,
                module.MetadataJson,
                instance.MetadataJson,
                module.DesignPreviewJson);
            if (RuntimeDurationContract.Policy(contract) == RuntimeDurationPolicy.Explicit) continue;
            var duration = RuntimeTimeline.DurationFrames(
                contract.ToJsonString(),
                instance.ContentJson,
                instance.AnimationJson,
                instance.DurationFrames,
                _moduleInstanceThemeContextService.GetTokensJson(connection, instance.Id));
            if (duration != instance.DurationFrames) updates.Add((instance.Id, duration));
        }
        foreach (var update in updates)
        {
            _moduleInstanceRepository.UpdateDuration(connection, update.Id, update.Duration);
        }

        var durationByShot = _moduleInstanceRepository.QueryAll(connection)
            .GroupBy((instance) => instance.ShotId, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => Math.Max(1, group.Sum((instance) => instance.DurationFrames)),
                StringComparer.Ordinal);
        foreach (var shot in _shotRepository.QueryAll(connection))
        {
            var duration = durationByShot.GetValueOrDefault(shot.Id, 1);
            if (duration == shot.DurationFrames) continue;
            _shotRepository.UpdateDuration(connection, shot.Id, duration);
        }
    }

    private static bool RuntimeInputDefinition(JsonObject definition)
    {
        if (!definition.TryGetPropertyValue("source", out var node)) return true;
        if (node is not JsonValue value || !value.TryGetValue<string>(out var source))
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

    private static string RuntimeCollectionStorageKey(JsonObject collection)
    {
        foreach (var key in new[] { "storageCollectionJsonKey", "sourceCollectionJsonKey", "jsonKey" })
        {
            if (!collection.ContainsKey(key)) continue;
            return JsonPath.RequiredString(collection, key, "Runtime collection definition");
        }
        throw new InvalidOperationException("Runtime collection definition requires a storage key.");
    }

    private static JsonArray ReconcileProjectedRuntimeCollection(JsonArray? current, JsonArray? defaults)
    {
        if (defaults is null)
            throw new InvalidOperationException("Projected runtime collection has no contract defaults.");
        RuntimeCollectionDocumentContract.Validate(defaults, "Projected runtime collection defaults");
        if (current is not null)
        {
            RuntimeCollectionDocumentContract.Validate(current, "Projected runtime collection content");
        }
        var currentById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (current is not null)
        {
            for (var index = 0; index < current.Count; index++)
            {
                var item = current[index] as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Projected runtime collection content item at index {index} must be an object.");
                currentById.Add(
                    JsonPath.RequiredString(item, "id", $"Projected runtime collection content item at index {index}"),
                    item);
            }
        }
        var result = new JsonArray();
        for (var index = 0; index < defaults.Count; index++)
        {
            var defaultItem = defaults[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Projected runtime collection default item at index {index} must be an object.");
            var next = defaultItem.DeepClone().AsObject();
            var id = JsonPath.RequiredString(
                next,
                "id",
                $"Projected runtime collection default item at index {index}");
            if (currentById.TryGetValue(id, out var currentItem))
            {
                foreach (var key in next.Select((entry) => entry.Key).ToList())
                {
                    if (currentItem[key] is { } value) next[key] = value.DeepClone();
                }
            }
            result.Add(next);
        }
        return result;
    }

    private JsonArray RequireDeclaredRuntimeCollection(
        string moduleInstanceId,
        string collectionJsonKey,
        JsonObject content)
    {
        if (string.IsNullOrWhiteSpace(collectionJsonKey))
        {
            throw new InvalidOperationException("Runtime collection key cannot be empty.");
        }
        var contract = ModuleInstanceRuntimeContract(moduleInstanceId);
        var matches = RuntimeDefinitionObjects(
                contract,
                "collections",
                $"Module Instance '{moduleInstanceId}' Runtime contract")
            .Where((collection) => RuntimeCollectionStorageKey(collection) == collectionJsonKey)
            .ToList();
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no unique declared runtime collection '{collectionJsonKey}'.");
        }

        var items = RequiredRuntimeCollection(
            content,
            collectionJsonKey,
            $"Module Instance '{moduleInstanceId}' content_json");
        RuntimeCollectionDocumentContract.Validate(
            items,
            $"Module Instance '{moduleInstanceId}' runtime collection '{collectionJsonKey}'");
        return items;
    }

    private JsonObject RequireDeclaredRuntimeInput(
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value)
    {
        if (string.IsNullOrWhiteSpace(jsonKey))
        {
            throw new InvalidOperationException("Runtime input key cannot be empty.");
        }
        var contract = ModuleInstanceRuntimeContract(moduleInstanceId);
        var matches = RuntimeDefinitionObjects(
                contract,
                "inputs",
                $"Module Instance '{moduleInstanceId}' Runtime contract")
            .Where(RuntimeInputDefinition)
            .Where((input) => JsonPath.RequiredString(input, "jsonKey", "Runtime Input definition") == jsonKey)
            .ToList();
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no unique declared runtime input '{jsonKey}'.");
        }
        RuntimeInputValueKindContract.ValidateRuntimeValue(
            matches[0],
            value,
            $"Module Instance '{moduleInstanceId}' runtime input '{jsonKey}'");
        return matches[0];
    }

    private void RequireDeclaredRuntimeCollectionField(
        string moduleInstanceId,
        string collectionJsonKey,
        string fieldJsonKey,
        JsonNode? value)
    {
        var contract = ModuleInstanceRuntimeContract(moduleInstanceId);
        var collectionMatches = RuntimeDefinitionObjects(
                contract,
                "collections",
                $"Module Instance '{moduleInstanceId}' Runtime contract")
            .Where((collection) => RuntimeCollectionStorageKey(collection) == collectionJsonKey)
            .ToList();
        if (collectionMatches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no unique declared runtime collection '{collectionJsonKey}'.");
        }
        var fieldMatches = RuntimeDefinitionObjects(
                collectionMatches[0],
                "fields",
                $"Runtime collection '{collectionJsonKey}'",
                required: true)
            .Where(RuntimeInputDefinition)
            .Where((field) => JsonPath.RequiredString(field, "jsonKey", "Runtime collection field") == fieldJsonKey)
            .ToList();
        if (fieldMatches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Runtime collection '{collectionJsonKey}' has no unique declared runtime field '{fieldJsonKey}'.");
        }
        RuntimeInputValueKindContract.ValidateRuntimeValue(
            fieldMatches[0],
            value,
            $"Module Instance '{moduleInstanceId}' runtime collection '{collectionJsonKey}' field '{fieldJsonKey}'");
    }

    private JsonObject ModuleInstanceRuntimeContract(string moduleInstanceId)
    {
        var instance = _moduleInstanceRepository.Get(moduleInstanceId);
        var module = _appModuleRepository.GetModule(instance.ModuleId);
        return EffectiveModuleInstanceContract(
            module.Id,
            module.MetadataJson,
            instance.MetadataJson,
            module.DesignPreviewJson);
    }

    private static void ValidateCurrentRuntimeValues(
        JsonObject contract,
        JsonObject content,
        string owner)
    {
        var inputs = RuntimeDefinitionObjects(contract, "inputs", $"{owner} effective Runtime contract");
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var jsonKey = JsonPath.RequiredString(input, "jsonKey", $"{owner} Runtime Input at index {index}");
            if (RuntimeInputDefinition(input))
            {
                if (!content.TryGetPropertyValue(jsonKey, out var value))
                {
                    throw new InvalidOperationException($"{owner} requires runtime input '{jsonKey}'.");
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

    private static void ValidateCurrentRuntimeCollections(
        JsonObject contract,
        JsonObject content,
        string owner)
    {
        var collections = RuntimeDefinitionObjects(
            contract,
            "collections",
            $"{owner} effective Runtime contract");
        var storageKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < collections.Count; index++)
        {
            var collection = collections[index];
            var storageKey = RuntimeCollectionStorageKey(collection);
            if (string.IsNullOrWhiteSpace(storageKey) || !storageKeys.Add(storageKey))
            {
                throw new InvalidOperationException(
                    $"{owner} has a missing or duplicate Runtime collection storage key '{storageKey}'.");
            }
            var items = RequiredRuntimeCollection(content, storageKey, owner);
            RuntimeCollectionDocumentContract.Validate(items, $"{owner} runtime collection '{storageKey}'");
            var componentItems = RuntimeComponentCollectionItemDocumentContract.ReadDefinition(
                collection,
                $"{owner} runtime collection '{storageKey}'");
            var fields = RuntimeDefinitionObjects(
                collection,
                "fields",
                $"{owner} runtime collection '{storageKey}'",
                required: true);
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
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
                    RuntimeComponentCollectionItemDocumentContract.ValidateItem(
                        item,
                        componentItems,
                        $"{owner} runtime collection '{storageKey}' item '{itemId}'");
                }
                for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    var field = fields[fieldIndex];
                    var fieldKey = JsonPath.RequiredString(
                        field,
                        "jsonKey",
                        $"{owner} runtime collection '{storageKey}' field at index {fieldIndex}");
                    if (RuntimeInputDefinition(field))
                    {
                        if (!item.TryGetPropertyValue(fieldKey, out var value))
                        {
                            throw new InvalidOperationException(
                                $"{owner} runtime collection '{storageKey}' item '{itemId}' requires field '{fieldKey}'.");
                        }
                        RuntimeInputValueKindContract.ValidateRuntimeValue(
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

    private static IReadOnlyList<JsonObject> RuntimeDefinitionObjects(
        JsonObject owner,
        string key,
        string context,
        bool required = false)
    {
        var array = RuntimeDefinitionArray(owner, key, context, required);
        if (array is null) return [];
        var definitions = new List<JsonObject>(array.Count);
        for (var index = 0; index < array.Count; index++)
        {
            definitions.Add(array[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{context} {key}[{index}] must be an object."));
        }
        return definitions;
    }

    private static JsonArray? RuntimeDefinitionArray(
        JsonObject owner,
        string key,
        string context,
        bool required)
    {
        if (!owner.TryGetPropertyValue(key, out var node))
        {
            if (!required) return null;
            throw new InvalidOperationException($"{context} requires a {key} definition array.");
        }
        return node as JsonArray
            ?? throw new InvalidOperationException(
                $"{context} {key} must be an array when present.");
    }

    private static JsonArray? OptionalRuntimeCollection(JsonObject owner, string key, string context)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"{context} has an empty runtime collection key.");
        }
        if (owner[key] is null) return null;
        return owner[key] as JsonArray
            ?? throw new InvalidOperationException($"{context} '{key}' must be an array.");
    }

    private static JsonArray RequiredRuntimeCollection(JsonObject owner, string key, string context) =>
        OptionalRuntimeCollection(owner, key, context)
        ?? throw new InvalidOperationException($"{context} requires runtime collection '{key}'.");

}
