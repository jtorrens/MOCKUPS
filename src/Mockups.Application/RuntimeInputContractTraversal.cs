using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record RuntimeInputPathPart(string? Name, int? ArrayIndex)
{
    public static RuntimeInputPathPart Property(string name) => new(name, null);
    public static RuntimeInputPathPart Index(int index) => new(null, index);
}

public sealed record RuntimeInputContractLocation(
    JsonObject Runtime, JsonObject Config, IReadOnlyList<RuntimeInputPathPart> Path);

// Walk only declared value boundaries. Source snapshots and definition metadata
// can contain complete Design contracts, but are not Production Runtime owners.
public static class RuntimeInputContractTraversal
{
    public static IEnumerable<RuntimeInputContractLocation> Contracts(
        JsonObject runtime, JsonObject config, IReadOnlyList<RuntimeInputPathPart> path)
    {
        yield return new(runtime, config, path);
        foreach (var field in RuntimeInputDefinitionReader.ReadInputs(runtime, config, includeHidden: true))
        {
            if (!CollectionFieldAvailability.IsEnabled(runtime, field)) continue;
            foreach (var child in StructuredValue(runtime[field.JsonKey], field,
                         [.. path, RuntimeInputPathPart.Property(field.JsonKey)]))
                yield return child;
        }
        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(runtime, config, includeHidden: true))
        {
            if (runtime[collection.JsonKey] is not JsonArray items) continue;
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index] is not JsonObject item) continue;
                foreach (var child in ItemContracts(item, collection,
                             [.. path, RuntimeInputPathPart.Property(collection.StorageJsonKey), RuntimeInputPathPart.Index(index)]))
                    yield return child;
            }
        }
    }

    public static IEnumerable<RuntimeInputContractLocation> ItemContracts(
        JsonObject item, RuntimeInputCollectionDefinition collection, IReadOnlyList<RuntimeInputPathPart> path)
    {
        var runtimeKey = !string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey)
            ? collection.ItemRuntimeContractJsonKey : collection.ComponentItems?.InputsJsonKey ?? "";
        if (runtimeKey.Length > 0 && item[runtimeKey] is JsonObject runtime)
            foreach (var child in Contracts(runtime, new JsonObject(),
                         [.. path, RuntimeInputPathPart.Property(runtimeKey)]))
                yield return child;
        foreach (var field in collection.Fields)
        {
            if (!CollectionFieldAvailability.IsEnabled(item, field)) continue;
            foreach (var child in StructuredValue(item[field.JsonKey], field,
                         [.. path, RuntimeInputPathPart.Property(field.JsonKey)]))
                yield return child;
        }
    }

    private static IEnumerable<RuntimeInputContractLocation> StructuredValue(
        JsonNode? value, ComponentInputDefinition field, IReadOnlyList<RuntimeInputPathPart> path)
    {
        if (field.ValueKind != ValueKind.StructuredCollection
            || field.StructuredCollection is null || value is not JsonArray items) yield break;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is not JsonObject item) continue;
            foreach (var child in ItemContracts(item, field.StructuredCollection,
                         [.. path, RuntimeInputPathPart.Index(index)]))
                yield return child;
        }
    }
}
