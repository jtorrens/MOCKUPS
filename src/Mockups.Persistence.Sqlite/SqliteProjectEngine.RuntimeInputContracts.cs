using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    private void ReconcileModuleInstanceRuntimePayload(
        SqliteConnection connection,
        string moduleInstanceId)
    {
        var instance = _productionOwner.ModuleInstanceRepository.Get(connection, moduleInstanceId);
        var module = _designOwner.AppModuleRepository.GetModule(connection, instance.ModuleId);
        var original = instance.ContentJson;
        var content = ParseJsonObject(original);
        var contract = _productionOwner.ResolveModuleInstanceContract(
            module.Id,
            instance.MetadataJson);
        foreach (var input in RuntimeInputDocumentContract.DefinitionObjects(
                     contract,
                     "inputs",
                     $"Module Instance '{moduleInstanceId}' effective Runtime contract"))
        {
            var inputId = JsonPath.RequiredString(input, "id", "Runtime Input definition");
            var jsonKey = JsonPath.RequiredString(input, "jsonKey", $"Runtime Input '{inputId}'");
            if (!RuntimeInputDocumentContract.IsRuntimeDefinition(input))
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

        foreach (var collection in RuntimeInputDocumentContract.DefinitionObjects(
                     contract,
                     "collections",
                     $"Module Instance '{moduleInstanceId}' effective Runtime contract"))
        {
            var storageKey = RuntimeInputDocumentContract.CollectionStorageKey(collection);
            var projected = collection.ContainsKey("storageCollectionJsonKey");
            var items = projected
                ? RuntimeInputDocumentContract.ReconcileProjectedCollection(
                    RuntimeInputDocumentContract.OptionalCollection(content, storageKey, $"Module Instance '{moduleInstanceId}' content_json"),
                    RuntimeInputDocumentContract.OptionalCollection(
                        contract,
                        JsonPath.RequiredString(collection, "jsonKey", "Runtime collection definition"),
                        $"Module Instance '{moduleInstanceId}' effective Runtime contract"))
                : RuntimeInputDocumentContract.OptionalCollection(content, storageKey, $"Module Instance '{moduleInstanceId}' content_json")
                    ?? new JsonArray();
            content[storageKey] = items;
            RuntimeCollectionDocumentContract.Validate(
                items,
                $"Module Instance '{moduleInstanceId}' runtime collection '{storageKey}'");
            var fields = RuntimeInputDocumentContract.DefinitionObjects(
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
                    if (!RuntimeInputDocumentContract.IsRuntimeDefinition(field)) continue;
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
        _productionOwner.ModuleInstanceRepository.UpdateContent(connection, moduleInstanceId, next);
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
        var matches = RuntimeInputDocumentContract.DefinitionObjects(
                contract,
                "collections",
                $"Module Instance '{moduleInstanceId}' Runtime contract")
            .Where((collection) => RuntimeInputDocumentContract.CollectionStorageKey(collection) == collectionJsonKey)
            .ToList();
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no unique declared runtime collection '{collectionJsonKey}'.");
        }

        var items = RuntimeInputDocumentContract.RequiredCollection(
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
        var matches = RuntimeInputDocumentContract.DefinitionObjects(
                contract,
                "inputs",
                $"Module Instance '{moduleInstanceId}' Runtime contract")
            .Where(RuntimeInputDocumentContract.IsRuntimeDefinition)
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
        var collectionMatches = RuntimeInputDocumentContract.DefinitionObjects(
                contract,
                "collections",
                $"Module Instance '{moduleInstanceId}' Runtime contract")
            .Where((collection) => RuntimeInputDocumentContract.CollectionStorageKey(collection) == collectionJsonKey)
            .ToList();
        if (collectionMatches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no unique declared runtime collection '{collectionJsonKey}'.");
        }
        var fieldMatches = RuntimeInputDocumentContract.DefinitionObjects(
                collectionMatches[0],
                "fields",
                $"Runtime collection '{collectionJsonKey}'",
                required: true)
            .Where(RuntimeInputDocumentContract.IsRuntimeDefinition)
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
        var instance = _productionOwner.ModuleInstanceRepository.Get(moduleInstanceId);
        var module = _designOwner.AppModuleRepository.GetModule(instance.ModuleId);
        return _productionOwner.ResolveModuleInstanceContract(
            module.Id,
            instance.MetadataJson);
    }

}
