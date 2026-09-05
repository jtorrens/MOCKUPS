using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class StructuredCollectionDocumentContract
{
    public static JsonArray StoredClone(
        JsonArray items,
        RuntimeInputCollectionDefinition definition,
        string owner)
    {
        RuntimeCollectionDocumentContract.Validate(items, owner);
        var result = new JsonArray();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{owner} item at index {index} must be an object.");
            var itemId = JsonPath.RequiredString(
                item,
                "id",
                $"{owner} item at index {index}");
            var stored = new JsonObject { ["id"] = itemId };
            foreach (var field in definition.Fields.Where((candidate) =>
                         candidate.Source == ComponentInputSource.Runtime))
            {
                var value = item[field.JsonKey]
                    ?? throw new InvalidOperationException(
                        $"{owner} item '{itemId}' requires field '{field.JsonKey}'.");
                stored[field.JsonKey] = field.ValueKind == ValueKind.StructuredCollection
                    ? StoredClone(
                        value as JsonArray
                            ?? throw new InvalidOperationException(
                                $"{owner} item '{itemId}' field '{field.JsonKey}' must be an array."),
                        field.StructuredCollection
                            ?? throw new InvalidOperationException(
                                $"{owner} item '{itemId}' field '{field.JsonKey}' requires a collection contract."),
                        $"{owner} item '{itemId}' field '{field.JsonKey}'")
                    : value.DeepClone();
            }
            if (definition.ComponentItems is { } componentItems)
            {
                stored[componentItems.OverridesJsonKey] = JsonPath.RequiredObject(
                    item,
                    componentItems.OverridesJsonKey,
                    $"{owner} item '{itemId}'").DeepClone();
                stored[componentItems.InputsJsonKey] = JsonPath.RequiredObject(
                    item,
                    componentItems.InputsJsonKey,
                    $"{owner} item '{itemId}'").DeepClone();
            }
            if (!string.IsNullOrWhiteSpace(definition.ItemRuntimeContractJsonKey))
            {
                stored[definition.ItemRuntimeContractJsonKey] = JsonPath.RequiredObject(
                    item,
                    definition.ItemRuntimeContractJsonKey,
                    $"{owner} item '{itemId}'").DeepClone();
            }
            if (definition.FixedComponentBoundary is { } boundary)
            {
                stored[boundary.OverridesJsonKey] = JsonPath.RequiredObject(
                    item,
                    boundary.OverridesJsonKey,
                    $"{owner} item '{itemId}'").DeepClone();
            }
            result.Add(stored);
        }
        Validate(result, definition, owner);
        return result;
    }

    public static void Validate(
        JsonArray items,
        RuntimeInputCollectionDefinition definition,
        string owner)
    {
        RuntimeCollectionDocumentContract.Validate(items, owner);
        if (definition.FixedItemCount > 0
            && items.Count != definition.FixedItemCount)
        {
            throw new InvalidOperationException(
                $"{owner} requires exactly {definition.FixedItemCount} items but contains {items.Count}.");
        }
        var storedFields = definition.Fields
            .Where((field) => field.Source == ComponentInputSource.Runtime)
            .ToList();
        var fieldKeys = storedFields
            .Select((field) => field.JsonKey)
            .ToHashSet(StringComparer.Ordinal);
        if (fieldKeys.Contains("id"))
        {
            throw new InvalidOperationException(
                $"{owner} fields must not redeclare the stable item id.");
        }
        var allowedKeys = fieldKeys.Append("id").ToHashSet(StringComparer.Ordinal);
        if (definition.ComponentItems is { } componentItems)
        {
            if (fieldKeys.Contains(componentItems.OverridesJsonKey)
                || fieldKeys.Contains(componentItems.InputsJsonKey))
            {
                throw new InvalidOperationException(
                    $"{owner} Component item document keys must not overlap field keys.");
            }
            allowedKeys.Add(componentItems.OverridesJsonKey);
            allowedKeys.Add(componentItems.InputsJsonKey);
        }
        if (!string.IsNullOrWhiteSpace(definition.ItemRuntimeContractJsonKey))
        {
            if (fieldKeys.Contains(definition.ItemRuntimeContractJsonKey))
            {
                throw new InvalidOperationException(
                    $"{owner} Runtime owner contract key must not overlap field keys.");
            }
            allowedKeys.Add(definition.ItemRuntimeContractJsonKey);
        }
        if (definition.FixedComponentBoundary is { } boundary)
        {
            var variantFields = definition.Fields.Where((field) =>
                    field.JsonKey.Equals(
                        boundary.VariantReferenceJsonKey,
                        StringComparison.Ordinal)
                    && field.ValueKind == ValueKind.ComponentVariant
                    && field.ComponentType.Equals(
                        boundary.ComponentType,
                        StringComparison.Ordinal))
                .ToList();
            if (variantFields.Count != 1
                || fieldKeys.Contains(boundary.OverridesJsonKey))
            {
                throw new InvalidOperationException(
                    $"{owner} fixed Component boundary requires one Variant field and a distinct Overrides key.");
            }
            allowedKeys.Add(boundary.OverridesJsonKey);
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{owner} item at index {index} must be an object.");
            var itemId = JsonPath.RequiredString(item, "id", $"{owner} item at index {index}");
            var keys = item.Select((entry) => entry.Key).ToHashSet(StringComparer.Ordinal);
            if (!keys.SetEquals(allowedKeys))
            {
                var missing = allowedKeys.Except(keys).Order(StringComparer.Ordinal).ToList();
                var unknown = keys.Except(allowedKeys).Order(StringComparer.Ordinal).ToList();
                throw new InvalidOperationException(
                    $"{owner} item '{itemId}' must use its exact declared collection document"
                    + (missing.Count == 0
                        ? ""
                        : $"; missing: {string.Join(", ", missing)}")
                    + $"{(unknown.Count == 0 ? "" : $"; unknown: {string.Join(", ", unknown)}")}.");
            }
            foreach (var field in storedFields)
            {
                var value = item[field.JsonKey]
                    ?? throw new InvalidOperationException(
                        $"{owner} item '{itemId}' requires field '{field.JsonKey}'.");
                RuntimeInputValueKindContract.ValidateRuntimeValue(
                    field,
                    value,
                    $"{owner} item '{itemId}' field '{field.JsonKey}'");
            }
            if (definition.ComponentItems is { } declaredComponentItems)
            {
                RuntimeComponentCollectionItemDocumentContract.ValidateItem(
                    item,
                    declaredComponentItems.DocumentKeys,
                    $"{owner} item '{itemId}'");
            }
            if (!string.IsNullOrWhiteSpace(definition.ItemRuntimeContractJsonKey))
            {
                _ = JsonPath.RequiredObject(
                    item,
                    definition.ItemRuntimeContractJsonKey,
                    $"{owner} item '{itemId}'");
            }
            if (definition.FixedComponentBoundary is { } fixedBoundary)
            {
                var reference = JsonPath.RequiredString(
                    item,
                    fixedBoundary.VariantReferenceJsonKey,
                    $"{owner} item '{itemId}'");
                if (!VariantReferenceId.TryParse(
                        reference,
                        out var componentClassId,
                        out _)
                    || !componentClassId.Equals(
                        fixedBoundary.ComponentClassId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{owner} item '{itemId}' Component Variant reference '{reference}' does not belong to fixed class '{fixedBoundary.ComponentClassId}'.");
                }
                _ = JsonPath.RequiredObject(
                    item,
                    fixedBoundary.OverridesJsonKey,
                    $"{owner} item '{itemId}'");
            }
        }
    }
}
