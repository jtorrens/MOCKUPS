using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class RuntimeCollectionItemContext
{
    public static JsonObject ResolveOwnerOfKey(
        JsonObject preview,
        IReadOnlyDictionary<string, RuntimeInputCollectionDefinition> collections,
        RuntimeInputCollectionDefinition collection,
        JsonObject item,
        string key,
        string owner)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var currentCollection = collection;
        var currentItem = item;
        var visitedCollections = new HashSet<string>(StringComparer.Ordinal);
        while (!currentItem.ContainsKey(key))
        {
            if (!visitedCollections.Add(currentCollection.JsonKey))
            {
                throw new InvalidOperationException(
                    $"{owner} has a cyclic collection-parent relationship at '{currentCollection.JsonKey}'.");
            }
            if (!string.IsNullOrWhiteSpace(currentCollection.SourceCollectionJsonKey))
            {
                var sourceItems = preview[currentCollection.SourceCollectionJsonKey] as JsonArray
                    ?? throw new InvalidOperationException(
                        $"{owner} structural source '{currentCollection.SourceCollectionJsonKey}' must be an array.");
                RuntimeCollectionDocumentContract.Validate(
                    sourceItems,
                    $"{owner} structural source '{currentCollection.SourceCollectionJsonKey}'");
                var currentId = JsonPath.RequiredString(
                    currentItem,
                    "id",
                    $"{owner} collection item");
                var structuralItem = sourceItems.OfType<JsonObject>()
                    .SingleOrDefault((candidate) => JsonPath.RequiredString(
                        candidate,
                        "id",
                        $"{owner} structural source item")
                        .Equals(currentId, StringComparison.Ordinal));
                if (structuralItem is not null && structuralItem.ContainsKey(key))
                {
                    return structuralItem;
                }
            }
            if (string.IsNullOrWhiteSpace(currentCollection.UiParentCollectionJsonKey)
                || string.IsNullOrWhiteSpace(currentCollection.UiParentItemIdJsonKey))
            {
                throw new InvalidOperationException(
                    $"{owner} key '{key}' is not owned by collection item '{currentCollection.JsonKey}' or a declared parent.");
            }
            if (!collections.TryGetValue(
                    currentCollection.UiParentCollectionJsonKey,
                    out var parentCollection))
            {
                throw new InvalidOperationException(
                    $"{owner} parent collection '{currentCollection.UiParentCollectionJsonKey}' is not declared.");
            }
            var parentItemId = JsonPath.RequiredString(
                currentItem,
                currentCollection.UiParentItemIdJsonKey,
                $"{owner} collection item");
            currentItem = DesignPreviewTestValues.CurrentCollectionItems(preview, parentCollection)
                .SingleOrDefault((candidate) => JsonPath.RequiredString(
                    candidate,
                    "id",
                    $"{owner} parent collection item")
                    .Equals(parentItemId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"{owner} parent item '{parentItemId}' does not exist in collection '{parentCollection.JsonKey}'.");
            currentCollection = parentCollection;
        }
        return currentItem;
    }
}
