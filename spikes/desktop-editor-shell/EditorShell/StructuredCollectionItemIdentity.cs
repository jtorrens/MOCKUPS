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
    }
}
