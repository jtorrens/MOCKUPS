using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record StructuredCollectionOwnerSegment(string CollectionJsonKey, string ItemId);

public sealed record StructuredCollectionAddress(
    string RootStorageJsonKey,
    IReadOnlyList<StructuredCollectionOwnerSegment> Owners,
    string CollectionJsonKey)
{
    public static StructuredCollectionAddress Root(string collectionJsonKey) =>
        new(collectionJsonKey, [], collectionJsonKey);
}

public abstract record StructuredCollectionMutation(StructuredCollectionAddress Address);

public sealed record AddStructuredCollectionItem(
    StructuredCollectionAddress Address,
    JsonObject Prototype,
    string? BeforeItemId = null) : StructuredCollectionMutation(Address);

public sealed record DuplicateStructuredCollectionItem(
    StructuredCollectionAddress Address,
    string SourceItemId,
    string? BeforeItemId = null) : StructuredCollectionMutation(Address);

public sealed record MoveStructuredCollectionItem(
    StructuredCollectionAddress Address,
    string ItemId,
    string? BeforeItemId = null) : StructuredCollectionMutation(Address);

public sealed record DeleteStructuredCollectionItem(
    StructuredCollectionAddress Address,
    string ItemId) : StructuredCollectionMutation(Address);

public sealed record StructuredCollectionIdentityChange(
    IReadOnlyDictionary<string, string> RebasedItemIds,
    IReadOnlySet<string> RemovedAnimationTargetIds);

public sealed record StructuredCollectionMutationResult(
    JsonObject Content,
    JsonObject Animation,
    JsonArray Collection,
    JsonObject? Item,
    string? SelectedItemId,
    StructuredCollectionIdentityChange IdentityChange);

public static class StructuredCollectionMutationEngine
{
    public static JsonObject UpdateValues(
        JsonObject content,
        RuntimeInputCollectionDefinition rootDefinition,
        StructuredCollectionAddress address,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "A structured collection update requires at least one explicit field.");
        }

        var nextContent = content.DeepClone().AsObject();
        var (collection, definition) = Resolve(nextContent, rootDefinition, address);
        var itemIndex = RequiredIndex(collection, itemId, definition.Id);
        var item = collection[itemIndex] as JsonObject
            ?? throw new InvalidOperationException(
                $"Structured collection '{definition.Id}' item '{itemId}' must be an object.");
        foreach (var (fieldJsonKey, value) in values)
        {
            if (string.IsNullOrWhiteSpace(fieldJsonKey))
            {
                throw new InvalidOperationException(
                    "Structured collection field keys cannot be empty.");
            }

            var matches = definition.Fields
                .Where((field) =>
                    field.Source == ComponentInputSource.Runtime
                    && field.JsonKey.Equals(fieldJsonKey, StringComparison.Ordinal))
                .ToList();
            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Structured collection '{definition.Id}' has no unique declared runtime field '{fieldJsonKey}'.");
            }
            item[fieldJsonKey] = value?.DeepClone();
        }

        var rootCollection = nextContent[address.RootStorageJsonKey] as JsonArray
            ?? throw new InvalidOperationException(
                $"Structured collection update requires root array '{address.RootStorageJsonKey}'.");
        StructuredCollectionDocumentContract.Validate(
            rootCollection,
            rootDefinition,
            $"Structured collection update '{rootDefinition.Id}'");
        StructuredCollectionItemIdentity.ValidateUniqueTargetIds(
            rootCollection,
            rootDefinition,
            $"Structured collection update '{rootDefinition.Id}'");
        return nextContent;
    }

    public static StructuredCollectionMutationResult Apply(
        JsonObject content,
        JsonObject animation,
        RuntimeInputCollectionDefinition rootDefinition,
        StructuredCollectionMutation mutation)
    {
        var nextContent = content.DeepClone().AsObject();
        var nextAnimation = animation.DeepClone().AsObject();
        var (collection, definition) = Resolve(nextContent, rootDefinition, mutation.Address);
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        var removed = new HashSet<string>(StringComparer.Ordinal);
        JsonObject? selectedItem;
        string? selectedItemId;
        switch (mutation)
        {
            case AddStructuredCollectionItem add:
                selectedItem = Add(collection, definition, add, mappings);
                selectedItemId = RequiredId(selectedItem, definition.Id);
                break;
            case DuplicateStructuredCollectionItem duplicate:
                selectedItem = Duplicate(collection, definition, duplicate, nextAnimation, mappings);
                selectedItemId = RequiredId(selectedItem, definition.Id);
                break;
            case MoveStructuredCollectionItem move:
                Move(collection, definition, move);
                selectedItem = collection[IndexOf(collection, move.ItemId)] as JsonObject;
                selectedItemId = move.ItemId;
                break;
            case DeleteStructuredCollectionItem delete:
                Delete(collection, definition, delete, nextAnimation, removed);
                selectedItem = null;
                selectedItemId = null;
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported structured collection mutation '{mutation.GetType().Name}'.");
        }
        StructuredCollectionDocumentContract.Validate(
            collection,
            definition,
            $"Structured collection mutation '{definition.Id}'");
        var rootCollection = nextContent[mutation.Address.RootStorageJsonKey] as JsonArray
            ?? throw new InvalidOperationException(
                $"Structured collection mutation requires root array '{mutation.Address.RootStorageJsonKey}'.");
        StructuredCollectionItemIdentity.ValidateUniqueTargetIds(
            rootCollection,
            rootDefinition,
            $"Structured collection mutation '{rootDefinition.Id}'");

        return new StructuredCollectionMutationResult(
            nextContent,
            nextAnimation,
            collection.DeepClone().AsArray(),
            selectedItem?.DeepClone().AsObject(),
            selectedItemId,
            new StructuredCollectionIdentityChange(mappings, removed));
    }

    public static StructuredCollectionMutation Snapshot(StructuredCollectionMutation mutation) =>
        WithAddress(mutation, Snapshot(mutation.Address));

    public static StructuredCollectionMutation WithAddress(
        StructuredCollectionMutation mutation,
        StructuredCollectionAddress address) =>
        mutation switch
        {
            AddStructuredCollectionItem add => add with
            {
                Address = address,
                Prototype = add.Prototype.DeepClone().AsObject(),
            },
            DuplicateStructuredCollectionItem duplicate => duplicate with
            {
                Address = address,
            },
            MoveStructuredCollectionItem move => move with
            {
                Address = address,
            },
            DeleteStructuredCollectionItem delete => delete with
            {
                Address = address,
            },
            _ => throw new InvalidOperationException(
                $"Unsupported structured collection mutation '{mutation.GetType().Name}'."),
        };

    private static StructuredCollectionAddress Snapshot(StructuredCollectionAddress address) =>
        address with { Owners = address.Owners.ToList() };

    private static (JsonArray Collection, RuntimeInputCollectionDefinition Definition) Resolve(
        JsonObject content,
        RuntimeInputCollectionDefinition rootDefinition,
        StructuredCollectionAddress address)
    {
        if (string.IsNullOrWhiteSpace(address.RootStorageJsonKey)
            || string.IsNullOrWhiteSpace(address.CollectionJsonKey))
        {
            throw new InvalidOperationException(
                "A structured collection address requires root and target collection keys.");
        }

        var definition = rootDefinition;
        var collectionKey = address.RootStorageJsonKey;
        var collection = content[collectionKey] as JsonArray
            ?? throw new InvalidOperationException(
                $"Structured collection mutation requires array '{collectionKey}'.");
        for (var depth = 0; depth < address.Owners.Count; depth++)
        {
            var ownerSegment = address.Owners[depth];
            if (!ownerSegment.CollectionJsonKey.Equals(collectionKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Structured collection owner path expected '{collectionKey}' but received '{ownerSegment.CollectionJsonKey}'.");
            }
            var ownerIndex = RequiredIndex(collection, ownerSegment.ItemId, collectionKey);
            var owner = collection[ownerIndex] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Structured collection '{collectionKey}' item '{ownerSegment.ItemId}' must be an object.");
            var nextKey = depth + 1 < address.Owners.Count
                ? address.Owners[depth + 1].CollectionJsonKey
                : address.CollectionJsonKey;
            definition = NestedDefinition(definition, nextKey);
            collection = owner[nextKey] as JsonArray
                ?? throw new InvalidOperationException(
                    $"Structured collection item '{ownerSegment.ItemId}' requires nested array '{nextKey}'.");
            collectionKey = nextKey;
        }

        if (!collectionKey.Equals(address.CollectionJsonKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Structured collection address resolved '{collectionKey}' instead of '{address.CollectionJsonKey}'.");
        }
        return (collection, definition);
    }

    private static RuntimeInputCollectionDefinition NestedDefinition(
        RuntimeInputCollectionDefinition owner,
        string collectionJsonKey)
    {
        var matches = owner.Fields
            .Where((field) => field.JsonKey.Equals(collectionJsonKey, StringComparison.Ordinal)
                && field.StructuredCollection is not null)
            .Select((field) => field.StructuredCollection!)
            .ToList();
        return matches.Count == 1
            ? matches[0]
            : throw new InvalidOperationException(
                $"Structured collection '{owner.Id}' has no unique nested collection '{collectionJsonKey}'.");
    }

    private static JsonObject Add(
        JsonArray collection,
        RuntimeInputCollectionDefinition definition,
        AddStructuredCollectionItem mutation,
        Dictionary<string, string> mappings)
    {
        if (mutation.Prototype.ContainsKey("id"))
        {
            throw new InvalidOperationException(
                $"Structured collection '{definition.Id}' Add prototype must not contain an id.");
        }
        var item = mutation.Prototype.DeepClone().AsObject();
        item["id"] = $"{definition.Id}_{Guid.NewGuid():N}";
        StructuredCollectionItemIdentity.RebaseNestedItems(item, definition, mappings);
        InsertBefore(collection, item, mutation.BeforeItemId, definition.Id);
        return item;
    }

    private static JsonObject Duplicate(
        JsonArray collection,
        RuntimeInputCollectionDefinition definition,
        DuplicateStructuredCollectionItem mutation,
        JsonObject animation,
        Dictionary<string, string> mappings)
    {
        var sourceIndex = RequiredIndex(collection, mutation.SourceItemId, definition.Id);
        var source = collection[sourceIndex] as JsonObject
            ?? throw new InvalidOperationException(
                $"Structured collection '{definition.Id}' item '{mutation.SourceItemId}' must be an object.");
        var duplicate = source.DeepClone().AsObject();
        var duplicateId = $"{definition.Id}_{Guid.NewGuid():N}";
        duplicate["id"] = duplicateId;
        RuntimeInputForwardingContract.RebaseIds(duplicate, mutation.SourceItemId, duplicateId);
        if (!mappings.TryAdd(mutation.SourceItemId, duplicateId))
        {
            throw new InvalidOperationException(
                $"Structured collection '{definition.Id}' contains duplicate stable id '{mutation.SourceItemId}'.");
        }
        StructuredCollectionItemIdentity.RebaseNestedItems(duplicate, definition, mappings);
        InsertBefore(collection, duplicate, mutation.BeforeItemId, definition.Id);
        DuplicateAnimationTargets(animation, mappings);
        return duplicate;
    }

    private static void Move(
        JsonArray collection,
        RuntimeInputCollectionDefinition definition,
        MoveStructuredCollectionItem mutation)
    {
        var currentIndex = RequiredIndex(collection, mutation.ItemId, definition.Id);
        if (mutation.BeforeItemId?.Equals(mutation.ItemId, StringComparison.Ordinal) == true)
        {
            return;
        }
        var item = collection[currentIndex];
        collection.RemoveAt(currentIndex);
        InsertBefore(collection, item, mutation.BeforeItemId, definition.Id);
    }

    private static void Delete(
        JsonArray collection,
        RuntimeInputCollectionDefinition definition,
        DeleteStructuredCollectionItem mutation,
        JsonObject animation,
        HashSet<string> removed)
    {
        var index = RequiredIndex(collection, mutation.ItemId, definition.Id);
        var item = collection[index] as JsonObject
            ?? throw new InvalidOperationException(
                $"Structured collection '{definition.Id}' item '{mutation.ItemId}' must be an object.");
        removed.UnionWith(StructuredCollectionItemIdentity.TargetIds(item, definition));
        collection.RemoveAt(index);
        RemoveAnimationTargets(animation, removed);
    }

    private static void InsertBefore(
        JsonArray collection,
        JsonNode? item,
        string? beforeItemId,
        string owner)
    {
        if (beforeItemId is null)
        {
            collection.Add(item);
            return;
        }
        collection.Insert(RequiredIndex(collection, beforeItemId, owner), item);
    }

    private static int RequiredIndex(JsonArray collection, string itemId, string owner)
    {
        var index = IndexOf(collection, itemId);
        return index >= 0
            ? index
            : throw new InvalidOperationException(
                $"Structured collection '{owner}' has no item '{itemId}'.");
    }

    private static int IndexOf(JsonArray collection, string itemId)
    {
        for (var index = 0; index < collection.Count; index++)
        {
            if (collection[index]?["id"]?.GetValue<string>() == itemId) return index;
        }
        return -1;
    }

    private static string RequiredId(JsonObject item, string owner) =>
        item["id"]?.GetValue<string>() is { Length: > 0 } id
            ? id
            : throw new InvalidOperationException(
                $"Structured collection '{owner}' item requires a stable id.");

    private static void RemoveAnimationTargets(JsonObject animation, IReadOnlySet<string> targetIds)
    {
        if (animation["tracks"] is not JsonArray tracks) return;
        foreach (var track in tracks.OfType<JsonObject>()
                     .Where((candidate) => targetIds.Contains(
                         candidate["targetId"]?.GetValue<string>() ?? ""))
                     .ToList())
        {
            tracks.Remove(track);
        }
    }

    private static void DuplicateAnimationTargets(
        JsonObject animation,
        IReadOnlyDictionary<string, string> mappings)
    {
        if (animation["tracks"] is not JsonArray tracks) return;
        foreach (var sourceTrack in tracks.OfType<JsonObject>()
                     .Where((track) => mappings.ContainsKey(
                         track["targetId"]?.GetValue<string>() ?? ""))
                     .ToList())
        {
            var duplicateTrack = sourceTrack.DeepClone().AsObject();
            duplicateTrack["id"] = $"track_{Guid.NewGuid():N}";
            duplicateTrack["targetId"] = mappings[
                sourceTrack["targetId"]?.GetValue<string>() ?? ""];
            foreach (var keyframe in (duplicateTrack["keyframes"] as JsonArray)
                         ?.OfType<JsonObject>() ?? [])
            {
                keyframe["id"] = $"keyframe_{Guid.NewGuid():N}";
            }
            tracks.Add(duplicateTrack);
        }
    }
}
