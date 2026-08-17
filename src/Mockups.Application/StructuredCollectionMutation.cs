using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public enum StructuredCollectionMutationKind
{
    Duplicate,
    Delete,
}

public sealed record StructuredCollectionPathSegment(
    string CollectionJsonKey,
    string ItemId);

public sealed record StructuredCollectionMutation(
    StructuredCollectionMutationKind Kind,
    IReadOnlyList<StructuredCollectionPathSegment> Path);

public sealed record StructuredCollectionMutationResult(
    JsonArray Collection,
    JsonObject? Item);

public static class StructuredCollectionMutationEngine
{
    public static StructuredCollectionMutationResult Apply(
        JsonObject content,
        JsonObject animation,
        RuntimeInputCollectionDefinition rootDefinition,
        string rootStorageKey,
        StructuredCollectionMutation mutation)
    {
        if (mutation.Path.Count == 0)
        {
            throw new InvalidOperationException(
                "A structured collection mutation requires one stable path segment.");
        }
        if (!mutation.Path[0].CollectionJsonKey.Equals(
                rootStorageKey,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Structured collection mutation root '{mutation.Path[0].CollectionJsonKey}' does not match '{rootStorageKey}'.");
        }

        var definition = rootDefinition;
        var collection = content[rootStorageKey] as JsonArray
            ?? throw new InvalidOperationException(
                $"Structured collection mutation requires array '{rootStorageKey}'.");
        for (var depth = 0; depth < mutation.Path.Count; depth++)
        {
            var segment = mutation.Path[depth];
            var index = IndexOf(collection, segment.ItemId);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Structured collection '{segment.CollectionJsonKey}' has no item '{segment.ItemId}'.");
            }
            if (depth == mutation.Path.Count - 1)
            {
                return ApplyAtItem(
                    collection,
                    definition,
                    index,
                    animation,
                    mutation.Kind);
            }

            var owner = collection[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Structured collection '{segment.CollectionJsonKey}' item '{segment.ItemId}' must be an object.");
            var next = mutation.Path[depth + 1];
            var nestedMatches = definition.Fields
                .Where((field) =>
                    field.JsonKey.Equals(
                        next.CollectionJsonKey,
                        StringComparison.Ordinal)
                    && field.StructuredCollection is not null)
                .ToList();
            if (nestedMatches.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Structured collection '{definition.Id}' has no unique nested collection '{next.CollectionJsonKey}'.");
            }
            definition = nestedMatches[0].StructuredCollection!;
            collection = owner[next.CollectionJsonKey] as JsonArray
                ?? throw new InvalidOperationException(
                    $"Structured collection item '{segment.ItemId}' requires nested array '{next.CollectionJsonKey}'.");
        }

        throw new InvalidOperationException(
            "Structured collection mutation did not resolve a target item.");
    }

    private static StructuredCollectionMutationResult ApplyAtItem(
        JsonArray collection,
        RuntimeInputCollectionDefinition definition,
        int index,
        JsonObject animation,
        StructuredCollectionMutationKind kind)
    {
        var source = collection[index] as JsonObject
            ?? throw new InvalidOperationException(
                $"Structured collection '{definition.Id}' item at index {index} must be an object.");
        if (kind == StructuredCollectionMutationKind.Delete)
        {
            var removedTargetIds = StructuredCollectionItemIdentity.TargetIds(source);
            collection.RemoveAt(index);
            RemoveAnimationTargets(animation, removedTargetIds);
            return new StructuredCollectionMutationResult(
                collection.DeepClone().AsArray(),
                null);
        }

        if (kind != StructuredCollectionMutationKind.Duplicate)
        {
            throw new InvalidOperationException(
                $"Unsupported structured collection mutation '{kind}'.");
        }

        var duplicate = source.DeepClone().AsObject();
        var previousId = RequiredId(source, definition.Id);
        var duplicateId = $"{definition.Id}_{Guid.NewGuid():N}";
        duplicate["id"] = duplicateId;
        RuntimeInputForwardingContract.RebaseIds(
            duplicate,
            previousId,
            duplicateId);
        var mappings = StructuredCollectionItemIdentity
            .RebaseNestedItems(duplicate, definition)
            .ToDictionary(
                (entry) => entry.Key,
                (entry) => entry.Value,
                StringComparer.Ordinal);
        mappings.Add(previousId, duplicateId);
        collection.Insert(index + 1, duplicate);
        DuplicateAnimationTargets(animation, mappings);
        return new StructuredCollectionMutationResult(
            collection.DeepClone().AsArray(),
            duplicate.DeepClone().AsObject());
    }

    private static int IndexOf(JsonArray collection, string itemId)
    {
        for (var index = 0; index < collection.Count; index++)
        {
            if (collection[index]?["id"]?.GetValue<string>() == itemId)
            {
                return index;
            }
        }
        return -1;
    }

    private static string RequiredId(JsonObject item, string owner) =>
        item["id"]?.GetValue<string>() is { Length: > 0 } id
            ? id
            : throw new InvalidOperationException(
                $"Structured collection '{owner}' item requires a stable id.");

    private static void RemoveAnimationTargets(
        JsonObject animation,
        IReadOnlyCollection<string> targetIds)
    {
        if (animation["tracks"] is not JsonArray tracks)
        {
            return;
        }
        foreach (var track in tracks
                     .OfType<JsonObject>()
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
        if (animation["tracks"] is not JsonArray tracks)
        {
            return;
        }
        foreach (var sourceTrack in tracks
                     .OfType<JsonObject>()
                     .Where((track) => mappings.ContainsKey(
                         track["targetId"]?.GetValue<string>() ?? ""))
                     .ToList())
        {
            var duplicateTrack = sourceTrack.DeepClone().AsObject();
            duplicateTrack["id"] = $"track_{Guid.NewGuid():N}";
            duplicateTrack["targetId"] = mappings[
                sourceTrack["targetId"]?.GetValue<string>() ?? ""];
            foreach (var keyframe in
                     (duplicateTrack["keyframes"] as JsonArray)
                     ?.OfType<JsonObject>() ?? [])
            {
                keyframe["id"] = $"keyframe_{Guid.NewGuid():N}";
            }
            tracks.Add(duplicateTrack);
        }
    }
}
