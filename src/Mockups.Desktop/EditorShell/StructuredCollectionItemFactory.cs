using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class StructuredCollectionItemFactory
{
    public static JsonObject Create(
        RuntimeInputCollectionDefinition collection,
        Func<ComponentInputDefinition, string> resolveDefault,
        Func<string, JsonObject> componentRuntimeValues,
        Func<JsonObject, RuntimeInputCollectionDefinition, string>? resolveRuntimeOwnerVariant = null)
    {
        var item = new JsonObject();
        foreach (var field in collection.Fields.Where((field) =>
                     field.Source == ComponentInputSource.Runtime))
        {
            item[field.JsonKey] = DesignPreviewTestValues.ValueNode(
                field,
                resolveDefault(field));
        }

        if (collection.ComponentItems is { } componentItems)
        {
            var variantField = collection.Fields.Single((field) =>
                field.JsonKey.Equals(
                    componentItems.VariantReferenceJsonKey,
                    StringComparison.Ordinal));
            var reference = DesignPreviewTestValues.CollectionValue(item, variantField);
            item[componentItems.OverridesJsonKey] = new JsonObject();
            item[componentItems.InputsJsonKey] = string.IsNullOrWhiteSpace(reference)
                ? new JsonObject()
                : componentRuntimeValues(reference).DeepClone();
        }

        if (collection.FixedComponentBoundary is { } fixedBoundary)
        {
            item[fixedBoundary.OverridesJsonKey] = new JsonObject();
        }

        if (!string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey))
        {
            var reference = resolveRuntimeOwnerVariant?.Invoke(item, collection)
                ?? throw new InvalidOperationException(
                    $"Structured collection '{collection.Id}' requires an explicit Runtime owner Variant resolver.");
            item[collection.ItemRuntimeContractJsonKey] =
                componentRuntimeValues(reference).DeepClone();
        }

        return item;
    }
}
