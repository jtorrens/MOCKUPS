using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class StructuredCollectionItemIdentity
{
    public static IReadOnlyDictionary<string, string> RebaseNestedItems(
        JsonObject owner,
        RuntimeInputCollectionDefinition collection)
    {
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        Rebase(owner, collection, mappings);
        foreach (var (previous, next) in mappings)
        {
            RuntimeInputForwardingContract.RebaseIds(owner, previous, next);
        }
        ReplaceExactReferences(owner, mappings);
        return mappings;
    }

    public static IReadOnlyList<string> TargetIds(JsonNode root)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Visit(JsonNode? node)
        {
            if (node is JsonObject value)
            {
                if (value["id"] is JsonValue idValue
                    && idValue.TryGetValue<string>(out var id)
                    && !string.IsNullOrWhiteSpace(id)) ids.Add(id);
                foreach (var child in value.Select((entry) => entry.Value)) Visit(child);
            }
            else if (node is JsonArray array)
            {
                foreach (var child in array) Visit(child);
            }
        }
        Visit(root);
        return ids.ToList();
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
                    mappings[previous] = next;
                    RuntimeInputForwardingContract.RebaseIds(item, previous, next);
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
        foreach (var input in ComponentPreviewInputSession.ReadRuntimeInputs(
                     runtimeContract,
                     new JsonObject()))
        {
            if (input.StructuredCollection is { } structuredCollection)
            {
                RebaseCollectionItems(runtimeContract, structuredCollection, mappings);
            }
        }
        foreach (var collection in ComponentPreviewInputSession.ReadRuntimeCollections(
                     runtimeContract,
                     new JsonObject(),
                     includeHidden: true))
        {
            RebaseCollectionItems(runtimeContract, collection, mappings);
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
                mappings[previous] = next;
            }
            Rebase(item, collection, mappings);
        }
    }

    private static void ReplaceExactReferences(
        JsonNode? node,
        IReadOnlyDictionary<string, string> mappings)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select((entry) => entry.Key).ToList())
            {
                if (obj[key] is JsonValue value
                    && value.TryGetValue<string>(out var text)
                    && mappings.TryGetValue(text, out var replacement))
                {
                    obj[key] = replacement;
                    continue;
                }
                ReplaceExactReferences(obj[key], mappings);
            }
            return;
        }
        if (node is not JsonArray array) return;
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is JsonValue value
                && value.TryGetValue<string>(out var text)
                && mappings.TryGetValue(text, out var replacement))
            {
                array[index] = replacement;
                continue;
            }
            ReplaceExactReferences(array[index], mappings);
        }
    }
}
