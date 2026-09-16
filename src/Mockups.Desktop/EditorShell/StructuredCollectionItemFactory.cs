using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class StructuredCollectionItemFactory
{
    public static JsonObject Create(
        RuntimeInputCollectionDefinition collection,
        Func<ComponentInputDefinition, string> resolveDefault,
        Func<string, JsonObject> componentRuntimeValues,
        Func<JsonObject, RuntimeInputCollectionDefinition, string>? resolveRuntimeOwnerVariant = null,
        int suggestedOrdinal = 0)
    {
        var item = new JsonObject();
        foreach (var field in collection.Fields.Where((field) =>
                     field.Source == ComponentInputSource.Runtime))
        {
            item[field.JsonKey] = DesignPreviewTestValues.ValueNode(
                field,
                resolveDefault(field));
        }
        if (suggestedOrdinal > 0
            && SuggestedNameField(collection) is { } nameField)
        {
            item[nameField.JsonKey] =
                $"{collection.ItemLabel} {suggestedOrdinal}";
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

    public static int NextSuggestedOrdinal(
        RuntimeInputCollectionDefinition collection,
        IEnumerable<JsonObject> items)
    {
        var nameField = SuggestedNameField(collection);
        if (nameField is null) return 0;
        var prefix = $"{collection.ItemLabel} ";
        var highest = 0;
        foreach (var item in items)
        {
            var value = item[nameField.JsonKey]?.GetValue<string>() ?? "";
            if (value.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(value[prefix.Length..], out var ordinal))
            {
                highest = Math.Max(highest, ordinal);
            }
        }
        return highest + 1;
    }

    private static ComponentInputDefinition? SuggestedNameField(
        RuntimeInputCollectionDefinition collection)
    {
        var titleFieldId = collection.ItemPresentation?.TitleFieldId ?? "";
        if (titleFieldId.Length == 0) return null;
        return collection.Fields.SingleOrDefault((field) =>
            field.Id.Equals(titleFieldId, StringComparison.Ordinal)
            && field.Source == ComponentInputSource.Runtime
            && field.ValueKind == ValueKind.StringSingleLine
            && field.DefaultValue.Equals(
                collection.ItemLabel,
                StringComparison.Ordinal));
    }
}
