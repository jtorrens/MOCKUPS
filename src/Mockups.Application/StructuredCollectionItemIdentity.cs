using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class StructuredCollectionItemIdentity
{
    public static IReadOnlyDictionary<string, string> RebaseNestedItems(
        JsonObject owner,
        RuntimeInputCollectionDefinition collection)
    {
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        RebaseNestedItems(owner, collection, mappings);
        return mappings;
    }

    public static void RebaseNestedItems(
        JsonObject owner,
        RuntimeInputCollectionDefinition collection,
        Dictionary<string, string> mappings)
    {
        var previousMappings = mappings.Keys.ToHashSet(StringComparer.Ordinal);
        Rebase(owner, collection, mappings);
        foreach (var (previous, next) in mappings.Where((entry) =>
                     !previousMappings.Contains(entry.Key)))
        {
            RuntimeInputForwardingContract.RebaseIds(owner, previous, next);
        }
    }

    public static IReadOnlySet<string> TargetIds(
        JsonObject item,
        RuntimeInputCollectionDefinition collection)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        CollectItemTargets(item, collection, ids);
        return ids;
    }

    public static void ValidateUniqueTargetIds(
        JsonArray items,
        RuntimeInputCollectionDefinition collection,
        string owner)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is JsonObject item)
            {
                ValidateItemTargets(item, collection, $"{owner}[{index}]", paths);
            }
        }
    }

    private static void CollectItemTargets(
        JsonObject item,
        RuntimeInputCollectionDefinition collection,
        HashSet<string> ids)
    {
        if (item["id"] is JsonValue idValue
            && idValue.TryGetValue<string>(out var id)
            && !string.IsNullOrWhiteSpace(id))
        {
            ids.Add(id);
        }
        foreach (var field in collection.Fields.Where((field) =>
                     field.StructuredCollection is not null))
        {
            var nested = field.StructuredCollection!;
            foreach (var child in (item[field.JsonKey] as JsonArray)
                         ?.OfType<JsonObject>() ?? [])
            {
                CollectItemTargets(child, nested, ids);
            }
        }
        if (!string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey)
            && item[collection.ItemRuntimeContractJsonKey] is JsonObject runtimeContract)
        {
            CollectRuntimeContractTargets(runtimeContract, ids);
        }
        if (collection.ComponentItems is { } componentItems
            && item[componentItems.InputsJsonKey] is JsonObject componentRuntimeContract)
        {
            CollectRuntimeContractTargets(componentRuntimeContract, ids);
        }
    }

    private static void ValidateItemTargets(
        JsonObject item,
        RuntimeInputCollectionDefinition collection,
        string path,
        Dictionary<string, string> paths)
    {
        var id = JsonPath.RequiredString(item, "id", path);
        if (!paths.TryAdd(id, path))
        {
            throw new InvalidOperationException(
                $"Structured collection '{collection.Id}' reuses stable id '{id}' at '{path}' and '{paths[id]}'.");
        }
        foreach (var field in collection.Fields.Where((field) => field.StructuredCollection is not null))
        {
            var nested = field.StructuredCollection!;
            var children = item[field.JsonKey] as JsonArray
                ?? throw new InvalidOperationException(
                    $"{path} requires nested collection '{field.JsonKey}'.");
            for (var index = 0; index < children.Count; index++)
            {
                if (children[index] is JsonObject child)
                {
                    ValidateItemTargets(child, nested, $"{path}.{field.JsonKey}[{index}]", paths);
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey)
            && item[collection.ItemRuntimeContractJsonKey] is JsonObject runtimeContract)
        {
            ValidateRuntimeContractTargets(runtimeContract, $"{path}.{collection.ItemRuntimeContractJsonKey}", paths);
        }
        if (collection.ComponentItems is { } componentItems
            && item[componentItems.InputsJsonKey] is JsonObject componentRuntimeContract)
        {
            ValidateRuntimeContractTargets(componentRuntimeContract, $"{path}.{componentItems.InputsJsonKey}", paths);
        }
    }

    private static void ValidateRuntimeContractTargets(
        JsonObject runtimeContract,
        string path,
        Dictionary<string, string> paths)
    {
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(
                     runtimeContract,
                     new JsonObject()))
        {
            if (input.StructuredCollection is { } nested
                && nested.CanEditStructure
                && runtimeContract[nested.JsonKey] is JsonArray nestedItems)
            {
                ValidateCollectionTargets(nestedItems, nested, $"{path}.{nested.JsonKey}", paths);
            }
        }
        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(
                     runtimeContract,
                     new JsonObject(),
                     includeHidden: true).Where((collection) => collection.CanEditStructure))
        {
            if (runtimeContract[collection.JsonKey] is not JsonArray items)
            {
                continue;
            }
            ValidateCollectionTargets(items, collection, $"{path}.{collection.JsonKey}", paths);
        }
    }

    private static void ValidateCollectionTargets(
        JsonArray items,
        RuntimeInputCollectionDefinition collection,
        string path,
        Dictionary<string, string> paths)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is JsonObject item)
            {
                ValidateItemTargets(item, collection, $"{path}[{index}]", paths);
            }
        }
    }

    private static void CollectRuntimeContractTargets(
        JsonObject runtimeContract,
        HashSet<string> ids)
    {
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(
                     runtimeContract,
                     new JsonObject()))
        {
            if (input.StructuredCollection is { CanEditStructure: true } nested)
            {
                foreach (var item in (runtimeContract[nested.JsonKey] as JsonArray)
                             ?.OfType<JsonObject>() ?? [])
                {
                    CollectItemTargets(item, nested, ids);
                }
            }
        }
        foreach (var nested in RuntimeInputDefinitionReader.ReadCollections(
                     runtimeContract,
                     new JsonObject(),
                     includeHidden: true).Where((collection) => collection.CanEditStructure))
        {
            foreach (var item in (runtimeContract[nested.JsonKey] as JsonArray)
                         ?.OfType<JsonObject>() ?? [])
            {
                CollectItemTargets(item, nested, ids);
            }
        }
    }

    private static void Rebase(
        JsonObject owner,
        RuntimeInputCollectionDefinition collection,
        Dictionary<string, string> mappings)
    {
        foreach (var field in collection.Fields.Where((field) => field.StructuredCollection is not null))
        {
            var nested = field.StructuredCollection!;
            foreach (var item in (owner[field.JsonKey] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                var previous = item["id"]?.GetValue<string>() ?? "";
                var next = $"{nested.Id}_{Guid.NewGuid():N}";
                item["id"] = next;
                if (!string.IsNullOrWhiteSpace(previous))
                {
                    AddMapping(mappings, previous, next, nested.Id);
                }
                Rebase(item, nested, mappings);
            }
        }
        if (!string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey)
            && owner[collection.ItemRuntimeContractJsonKey] is JsonObject runtimeContract)
        {
            RebaseRuntimeContract(runtimeContract, mappings);
        }
        if (collection.ComponentItems is { } componentItems
            && owner[componentItems.InputsJsonKey] is JsonObject componentRuntimeContract)
        {
            RebaseRuntimeContract(componentRuntimeContract, mappings);
        }
    }

    private static void RebaseRuntimeContract(
        JsonObject runtimeContract,
        Dictionary<string, string> mappings)
    {
        var inputCollections = RuntimeInputDefinitionReader.ReadInputs(
                runtimeContract,
                new JsonObject())
            .Where((input) => input.StructuredCollection is not null)
            .Select((input) => input.StructuredCollection!)
            .ToList();
        var declaredCollections = RuntimeInputDefinitionReader.ReadCollections(
                runtimeContract,
                new JsonObject(),
                includeHidden: true)
            .ToList();
        foreach (var structuredCollection in inputCollections
                     .Where((collection) => collection.CanEditStructure))
        {
            RebaseCollectionItems(runtimeContract, structuredCollection, mappings);
        }
        foreach (var collection in declaredCollections.Where((collection) =>
                     collection.CanEditStructure))
        {
            RebaseCollectionItems(runtimeContract, collection, mappings);
        }
        foreach (var collection in inputCollections.Concat(declaredCollections))
        {
            RebaseParentItemIds(runtimeContract, collection, mappings);
        }
    }

    private static void RebaseParentItemIds(
        JsonObject runtimeContract,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyDictionary<string, string> mappings)
    {
        if (string.IsNullOrWhiteSpace(collection.UiParentCollectionJsonKey)
            || string.IsNullOrWhiteSpace(collection.UiParentItemIdJsonKey))
        {
            return;
        }
        foreach (var item in (runtimeContract[collection.JsonKey] as JsonArray)
                     ?.OfType<JsonObject>() ?? [])
        {
            var previous = item[collection.UiParentItemIdJsonKey]?.GetValue<string>() ?? "";
            if (mappings.TryGetValue(previous, out var next))
            {
                item[collection.UiParentItemIdJsonKey] = next;
            }
        }
    }

    private static void RebaseCollectionItems(
        JsonObject owner,
        RuntimeInputCollectionDefinition collection,
        Dictionary<string, string> mappings)
    {
        foreach (var item in (owner[collection.JsonKey] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var previous = item["id"]?.GetValue<string>() ?? "";
            var next = $"{collection.Id}_{Guid.NewGuid():N}";
            item["id"] = next;
            if (!string.IsNullOrWhiteSpace(previous))
            {
                AddMapping(mappings, previous, next, collection.Id);
            }
            Rebase(item, collection, mappings);
        }
    }

    private static void AddMapping(
        Dictionary<string, string> mappings,
        string previous,
        string next,
        string collectionId)
    {
        if (!mappings.TryAdd(previous, next))
        {
            throw new InvalidOperationException(
                $"Structured collection '{collectionId}' contains duplicate stable id '{previous}'.");
        }
    }

}
