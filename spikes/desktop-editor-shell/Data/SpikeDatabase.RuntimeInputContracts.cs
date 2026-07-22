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
        foreach (var input in (contract["inputs"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var jsonKey = input["jsonKey"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(jsonKey)) continue;
            if (!RuntimeInputDefinition(input))
            {
                content.Remove(jsonKey);
                continue;
            }
            if (content[jsonKey] is null)
            {
                content[jsonKey] = RuntimeInputValueKindContract.CreateDefaultValue(
                    input,
                    $"Runtime Input '{input["id"]?.GetValue<string>() ?? jsonKey}'");
            }
        }

        foreach (var collection in (contract["collections"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var storageKey = RuntimeCollectionStorageKey(collection);
            if (string.IsNullOrWhiteSpace(storageKey)) continue;
            var projected = collection["storageCollectionJsonKey"] is JsonValue;
            var items = projected
                ? ReconcileProjectedRuntimeCollection(
                    OptionalRuntimeCollection(content, storageKey, $"Module Instance '{moduleInstanceId}' content_json"),
                    OptionalRuntimeCollection(
                        contract,
                        collection["jsonKey"]?.GetValue<string>() ?? "",
                        $"Module Instance '{moduleInstanceId}' effective Runtime contract"))
                : OptionalRuntimeCollection(content, storageKey, $"Module Instance '{moduleInstanceId}' content_json")
                    ?? new JsonArray();
            content[storageKey] = items;
            foreach (var (item, index) in items.OfType<JsonObject>().Select((item, index) => (item, index)))
            {
                item["id"] ??= $"{storageKey}_{index + 1:000}";
                foreach (var field in (collection["fields"] as JsonArray)?.OfType<JsonObject>() ?? [])
                {
                    if (!RuntimeInputDefinition(field)) continue;
                    var jsonKey = field["jsonKey"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrWhiteSpace(jsonKey) || item[jsonKey] is not null) continue;
                    item[jsonKey] = RuntimeInputValueKindContract.CreateDefaultValue(
                        field,
                        $"Runtime collection field '{field["id"]?.GetValue<string>() ?? jsonKey}'");
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
        var source = definition["source"]?.GetValue<string>() ?? "runtime";
        return source == "runtime";
    }

    private static string RuntimeCollectionStorageKey(JsonObject collection) =>
        collection["storageCollectionJsonKey"]?.GetValue<string>()
        ?? collection["sourceCollectionJsonKey"]?.GetValue<string>()
        ?? collection["jsonKey"]?.GetValue<string>()
        ?? "";

    private static JsonArray ReconcileProjectedRuntimeCollection(JsonArray? current, JsonArray? defaults)
    {
        if (defaults is null)
            throw new InvalidOperationException("Projected runtime collection has no contract defaults.");
        RuntimeCollectionDocumentContract.Validate(defaults, "Projected runtime collection defaults");
        if (current is not null)
        {
            RuntimeCollectionDocumentContract.Validate(current, "Projected runtime collection content");
        }
        var currentById = (current ?? [])
            .OfType<JsonObject>()
            .ToDictionary((item) => item["id"]!.GetValue<string>(), StringComparer.Ordinal);
        var result = new JsonArray();
        foreach (var defaultItem in defaults)
        {
            var next = defaultItem!.DeepClone().AsObject();
            var id = next["id"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Projected runtime collection item has no stable id.");
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
        var matches = (contract["collections"] as JsonArray)?.OfType<JsonObject>()
            .Count((collection) => RuntimeCollectionStorageKey(collection) == collectionJsonKey) ?? 0;
        if (matches != 1)
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
        var matches = (contract["inputs"] as JsonArray)?.OfType<JsonObject>()
            .Where(RuntimeInputDefinition)
            .Where((input) => input["jsonKey"]?.GetValue<string>() == jsonKey)
            .ToList() ?? [];
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
        var collectionMatches = (contract["collections"] as JsonArray)?.OfType<JsonObject>()
            .Where((collection) => RuntimeCollectionStorageKey(collection) == collectionJsonKey)
            .ToList() ?? [];
        if (collectionMatches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no unique declared runtime collection '{collectionJsonKey}'.");
        }
        var fieldMatches = (collectionMatches[0]["fields"] as JsonArray)?.OfType<JsonObject>()
            .Where(RuntimeInputDefinition)
            .Where((field) => field["jsonKey"]?.GetValue<string>() == fieldJsonKey)
            .ToList() ?? [];
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
        if (contract["inputs"] is null) return;
        var inputs = contract["inputs"] as JsonArray
            ?? throw new InvalidOperationException($"{owner} effective Runtime contract inputs must be an array.");
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index] as JsonObject
                ?? throw new InvalidOperationException($"{owner} Runtime Input at index {index} must be an object.");
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
        if (contract["collections"] is null) return;
        var collections = contract["collections"] as JsonArray
            ?? throw new InvalidOperationException($"{owner} effective Runtime contract collections must be an array.");
        var storageKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < collections.Count; index++)
        {
            var collection = collections[index] as JsonObject
                ?? throw new InvalidOperationException($"{owner} Runtime collection at index {index} must be an object.");
            var storageKey = RuntimeCollectionStorageKey(collection);
            if (string.IsNullOrWhiteSpace(storageKey) || !storageKeys.Add(storageKey))
            {
                throw new InvalidOperationException(
                    $"{owner} has a missing or duplicate Runtime collection storage key '{storageKey}'.");
            }
            var items = RequiredRuntimeCollection(content, storageKey, owner);
            RuntimeCollectionDocumentContract.Validate(items, $"{owner} runtime collection '{storageKey}'");
            if (collection["fields"] is null) continue;
            var fields = collection["fields"] as JsonArray
                ?? throw new InvalidOperationException(
                    $"{owner} runtime collection '{storageKey}' fields must be an array.");
            foreach (var item in items.OfType<JsonObject>())
            {
                var itemId = item["id"]!.GetValue<string>();
                for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    var field = fields[fieldIndex] as JsonObject
                        ?? throw new InvalidOperationException(
                            $"{owner} runtime collection '{storageKey}' field at index {fieldIndex} must be an object.");
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
