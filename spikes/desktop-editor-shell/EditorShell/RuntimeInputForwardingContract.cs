using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RuntimeInputForwardingContract
{
    public const string StorageKey = "$forwardedInputs";

    public static JsonObject EffectivePreview(JsonObject preview, JsonObject config)
    {
        var effective = preview.DeepClone().AsObject();
        var inputs = RequiredOrCreateArray(effective, "inputs", "Effective Preview");
        var collections = RequiredOrCreateArray(effective, "collections", "Effective Preview");
        var actions = OptionalArray(effective, "actions", "Effective Preview");
        var existingIds = ObjectItems(inputs, "Effective Preview inputs")
            .Select((input) => Text(input["id"]))
            .Where((id) => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var nestedRuntimeOwners = new HashSet<JsonObject>(ReferenceEqualityComparer.Instance);
        Visit(config, (container, targetKey, definition) =>
        {
            if (nestedRuntimeOwners.Contains(container)) return;
            var id = Text(definition["id"]);
            var jsonKey = Text(definition["jsonKey"]);
            if (id.Length == 0 || jsonKey.Length == 0)
            {
                throw new InvalidOperationException("Forwarded runtime inputs require stable id and jsonKey values.");
            }
            var next = definition.DeepClone().AsObject();
            RebaseTransitionForParent(next, container);
            RebaseNaturalTimingForParent(next, container);
            next["source"] = "runtime";
            next["defaultValue"] = StorageText(container[targetKey]);
            if (next["collection"] is JsonObject collection)
            {
                var sourceKey = $"{jsonKey}__variantSource";
                if ((next["projection"] as JsonObject)?["childCollection"] is JsonObject)
                    CollectForwardedOwners(container[targetKey], nestedRuntimeOwners);
                var source = container[targetKey]?.DeepClone() as JsonArray
                    ?? throw new InvalidOperationException("Forwarded runtime collection has no Variant collection value.");
                var projected = ProjectCollectionRuntimeValue(source, next["projection"] as JsonObject);
                effective[sourceKey] = source;
                if (!effective.TryGetPropertyValue(jsonKey, out var storedNode))
                    effective[jsonKey] = projected;
                else if (storedNode is JsonArray stored)
                    ApplyProjectedMetadata(stored, projected, MetadataKeys(next["projection"] as JsonObject));
                else
                    throw new InvalidOperationException(
                        $"Effective Preview forwarded collection '{jsonKey}' must be an array.");
                var runtimeCollection = collection.DeepClone().AsObject();
                runtimeCollection["jsonKey"] = jsonKey;
                runtimeCollection["sourceCollectionJsonKey"] = sourceKey;
                runtimeCollection["storageCollectionJsonKey"] = jsonKey;
                if (!ObjectItems(collections, "Effective Preview collections")
                    .Any((candidate) => Text(candidate["id"]) == Text(runtimeCollection["id"])))
                    collections.Add(runtimeCollection);
                ProjectChildRuntimeCollection(
                    effective,
                    collections,
                    source,
                    jsonKey,
                    next["projection"] as JsonObject,
                    nestedRuntimeOwners);
                return;
            }
            if (existingIds.Add(id)) inputs.Add(next);
            if (!effective.TryGetPropertyValue(jsonKey, out var effectiveValue))
            {
                effective[jsonKey] = container[targetKey]?.DeepClone()
                    ?? throw new InvalidOperationException($"Forwarded runtime input '{id}' has no variant value.");
            }
            else if (effectiveValue is null)
            {
                throw new InvalidOperationException($"Effective Preview forwarded input '{jsonKey}' cannot be null.");
            }
            var resolvedJsonKey = Text(definition["resolvedJsonKey"]);
            var targetResolvedJsonKey = Text(definition["targetResolvedJsonKey"]);
            if (resolvedJsonKey.Length > 0
                && targetResolvedJsonKey.Length > 0
                && effective[resolvedJsonKey] is null
                && container[targetResolvedJsonKey] is { } resolvedValue)
            {
                effective[resolvedJsonKey] = resolvedValue.DeepClone();
            }
        });
        VisitForwardedActionOwners(config, (container, forwards) =>
        {
            if (nestedRuntimeOwners.Contains(container)) return;
            var childActions = OptionalArray(container, "actions", "Forwarded runtime owner");
            if (childActions is null) return;
            foreach (var childAction in ObjectItems(childActions, "Forwarded runtime actions"))
            {
                var lifted = LiftAction(childAction, forwards);
                if (lifted is null) continue;
                var liftedId = Text(lifted["id"]);
                actions ??= new JsonArray();
                effective["actions"] = actions;
                if (!ObjectItems(actions, "Effective Preview actions")
                    .Any((candidate) => Text(candidate["id"]) == liftedId))
                    actions.Add(lifted);
            }
        });
        return effective;
    }

    private static void ProjectChildRuntimeCollection(
        JsonObject effective,
        JsonArray collections,
        JsonArray parentSource,
        string parentCollectionJsonKey,
        JsonObject? projection,
        HashSet<JsonObject> nestedRuntimeOwners)
    {
        if (projection?["childCollection"] is not JsonObject child) return;
        var childJsonKey = Text(child["jsonKey"]);
        var sourceCollectionJsonKey = Text(child["sourceCollectionJsonKey"]);
        var parentItemIdJsonKey = Text(child["parentItemIdJsonKey"]);
        var variantReferenceJsonKey = Text(child["variantReferenceJsonKey"]);
        var runtimeContractJsonKey = Text(child["runtimeContractJsonKey"]);
        var sourceRuntimeContractJsonKey = Text(child["sourceRuntimeContractJsonKey"]);
        var metadataKeys = MetadataKeys(child);
        if (childJsonKey.Length == 0
            || sourceCollectionJsonKey.Length == 0
            || parentItemIdJsonKey.Length == 0
            || runtimeContractJsonKey.Length == 0
            || sourceRuntimeContractJsonKey.Length == 0)
        {
            throw new InvalidOperationException("Projected child runtime collections require explicit keys.");
        }

        var values = new JsonArray();
        RuntimeCollectionDocumentContract.Validate(
            parentSource,
            "Projected child runtime collection parents");
        foreach (var parent in ObjectItems(parentSource, "Projected child runtime collection parents"))
        {
            var parentId = Text(parent["id"]);
            if (parentId.Length == 0)
                throw new InvalidOperationException("Projected child runtime collection parent has no stable id.");
            var children = parent[sourceCollectionJsonKey] as JsonArray
                ?? throw new InvalidOperationException($"Projected child runtime collection parent is missing '{sourceCollectionJsonKey}'.");
            RuntimeCollectionDocumentContract.Validate(
                children,
                $"Projected child runtime collection parent '{parentId}' items");
            foreach (var state in ObjectItems(children, "Projected child runtime collection items"))
            {
                CollectForwardedOwners(state, nestedRuntimeOwners);
                var stateId = Text(state["id"]);
                if (stateId.Length == 0)
                    throw new InvalidOperationException("Projected child runtime collection item has no stable id.");
                var sourceContract = state[sourceRuntimeContractJsonKey] as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Projected child runtime collection item requires object '{sourceRuntimeContractJsonKey}'.");
                var value = new JsonObject
                {
                    ["id"] = stateId,
                    [parentItemIdJsonKey] = parentId,
                    [variantReferenceJsonKey] = state[variantReferenceJsonKey]?.DeepClone(),
                    [runtimeContractJsonKey] = EffectivePreview(new JsonObject(), sourceContract),
                };
                foreach (var metadataKey in metadataKeys)
                    value[metadataKey] = state[metadataKey]?.DeepClone()
                        ?? throw new InvalidOperationException($"Projected child runtime collection item is missing '{metadataKey}'.");
                values.Add(value);
            }
        }

        if (!effective.TryGetPropertyValue(childJsonKey, out var storedNode))
            effective[childJsonKey] = values;
        else if (storedNode is JsonArray stored)
            ApplyProjectedMetadata(stored, values, metadataKeys);
        else
            throw new InvalidOperationException(
                $"Effective Preview projected child collection '{childJsonKey}' must be an array.");
        var runtimeCollection = child.DeepClone().AsObject();
        foreach (var projectionOnlyKey in new[]
                 {
                     "sourceCollectionJsonKey", "parentItemIdJsonKey", "variantReferenceJsonKey",
                     "runtimeContractJsonKey", "sourceRuntimeContractJsonKey", "itemMetadataJsonKeys",
                 })
        {
            runtimeCollection.Remove(projectionOnlyKey);
        }
        runtimeCollection["jsonKey"] = childJsonKey;
        runtimeCollection["storageCollectionJsonKey"] = childJsonKey;
        runtimeCollection["itemRuntimeContractJsonKey"] = runtimeContractJsonKey;
        runtimeCollection["uiParentCollectionJsonKey"] = parentCollectionJsonKey;
        runtimeCollection["uiParentItemIdJsonKey"] = parentItemIdJsonKey;
        if (!ObjectItems(collections, "Effective Preview collections")
            .Any((candidate) => Text(candidate["id"]) == Text(runtimeCollection["id"])))
            collections.Add(runtimeCollection);
    }

    private static void CollectForwardedOwners(JsonNode? node, HashSet<JsonObject> owners)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var child in array) CollectForwardedOwners(child, owners);
                break;
            case JsonObject obj:
                if (ForwardedDefinitions(obj) is not null) owners.Add(obj);
                foreach (var (key, child) in obj)
                    if (!key.Equals(StorageKey, StringComparison.Ordinal)) CollectForwardedOwners(child, owners);
                break;
        }
    }

    private static JsonObject? LiftAction(JsonObject action, JsonObject forwards)
    {
        JsonObject? Forward(string sourceId) => ForwardedDefinitionItems(forwards)
            .FirstOrDefault((definition) => Text(definition["sourceInputId"]) == sourceId);
        var play = Forward(Text(action["playInputId"]));
        var time = Forward(Text(action["timeJsonKey"]));
        if (play is null || time is null) return null;
        var lifted = action.DeepClone().AsObject();
        var childId = Text(action["id"]);
        lifted["id"] = $"forwarded:{Text(play["id"])}:{childId}";
        lifted["playFieldId"] = Text(play["id"]);
        lifted["playInputId"] = Text(play["jsonKey"]);
        lifted["timeJsonKey"] = Text(time["jsonKey"]);
        foreach (var key in new[] { "durationInputId", "durationBehaviorTimingInputId", "targetInputId", "targetFromJsonKey" })
        {
            var source = Text(action[key]);
            if (source.Length == 0) continue;
            var mapped = Forward(source);
            if (mapped is null) return null;
            lifted[key] = key == "durationBehaviorTimingInputId"
                ? Text(mapped["id"])
                : Text(mapped["jsonKey"]);
        }
        return lifted;
    }

    private static void VisitForwardedActionOwners(JsonNode? node, Action<JsonObject, JsonObject> visitor)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var child in array) VisitForwardedActionOwners(child, visitor);
                break;
            case JsonObject obj:
                if (ForwardedDefinitions(obj) is { } forwards) visitor(obj, forwards);
                foreach (var (key, child) in obj)
                    if (!key.Equals(StorageKey, StringComparison.Ordinal)) VisitForwardedActionOwners(child, visitor);
                break;
        }
    }

    private static JsonArray ProjectCollectionRuntimeValue(JsonArray source, JsonObject? projection)
    {
        if (projection is null)
            throw new InvalidOperationException("Forwarded structural runtime collection requires an explicit projection.");
        RuntimeCollectionDocumentContract.Validate(source, "Forwarded runtime collection source");
        var result = new JsonArray();
        var alternativesKey = Text(projection["optionsSourceCollectionJsonKey"]);
        var stateKey = Text(projection["stateJsonKey"]);
        var transitionKey = Text(projection["transitionJsonKey"]);
        var elapsedKey = Text(projection["elapsedJsonKey"]);
        var fromKey = Text(projection["fromJsonKey"]);
        if (alternativesKey.Length == 0
            || stateKey.Length == 0
            || transitionKey.Length == 0
            || elapsedKey.Length == 0
            || fromKey.Length == 0)
        {
            throw new InvalidOperationException(
                "Forwarded structural runtime collection projection requires explicit keys.");
        }
        var metadataKeys = MetadataKeys(projection);
        foreach (var item in ObjectItems(source, "Forwarded runtime collection source"))
        {
            var alternatives = item[alternativesKey] as JsonArray
                ?? throw new InvalidOperationException($"Forwarded runtime collection item is missing '{alternativesKey}'.");
            var firstId = ObjectItems(alternatives, $"Forwarded runtime collection '{alternativesKey}'")
                .FirstOrDefault()?["id"]?.GetValue<string>() ?? "";
            var id = Text(item["id"]);
            if (id.Length == 0) throw new InvalidOperationException("Forwarded runtime collection item has no stable id.");
            var value = new JsonObject
            {
                ["id"] = id,
                [stateKey] = firstId,
                [transitionKey] = false,
                [elapsedKey] = 0,
                [fromKey] = firstId,
            };
            foreach (var metadataKey in metadataKeys)
                value[metadataKey] = item[metadataKey]?.DeepClone()
                    ?? throw new InvalidOperationException($"Forwarded runtime collection item is missing '{metadataKey}'.");
            result.Add(value);
        }
        return result;
    }

    private static IReadOnlyList<string> MetadataKeys(JsonObject? projection)
    {
        if (projection is null || !projection.TryGetPropertyValue("itemMetadataJsonKeys", out var node))
            return [];
        if (node is not JsonArray keys)
            throw new InvalidOperationException("Projection itemMetadataJsonKeys must be an array.");
        var result = new List<string>();
        for (var index = 0; index < keys.Count; index++)
        {
            var key = Text(keys[index]);
            if (key.Length == 0)
                throw new InvalidOperationException(
                    $"Projection itemMetadataJsonKeys entry {index} must be a non-empty string.");
            result.Add(key);
        }
        return result;
    }

    private static void ApplyProjectedMetadata(
        JsonArray stored,
        JsonArray projected,
        IReadOnlyList<string> metadataKeys)
    {
        if (metadataKeys.Count == 0) return;
        RuntimeCollectionDocumentContract.Validate(projected, "Projected runtime collection");
        RuntimeCollectionDocumentContract.Validate(stored, "Stored projected runtime collection");
        var projectedItems = ObjectItems(projected, "Projected runtime collection").ToList();
        var storedItems = ObjectItems(stored, "Stored projected runtime collection").ToList();
        var projectedById = projectedItems
            .ToDictionary((item) => Text(item["id"]), StringComparer.Ordinal);
        for (var index = stored.Count - 1; index >= 0; index--)
        {
            var item = stored[index]!.AsObject();
            if (!projectedById.ContainsKey(Text(item["id"])))
            {
                stored.RemoveAt(index);
            }
        }
        var storedIds = storedItems
            .Select((item) => Text(item["id"]))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var source in projectedItems)
        {
            if (storedIds.Add(Text(source["id"]))) stored.Add(source.DeepClone());
        }
        foreach (var item in ObjectItems(stored, "Stored projected runtime collection"))
        {
            var id = Text(item["id"]);
            var source = projectedById[id];
            foreach (var metadataKey in metadataKeys)
                item[metadataKey] = source[metadataKey]?.DeepClone()
                    ?? throw new InvalidOperationException($"Projected runtime collection item is missing '{metadataKey}'.");
        }
    }

    private static void RebaseTransitionForParent(JsonObject definition, JsonObject container)
    {
        if (definition["transition"] is not JsonObject transition) return;
        var targetSourceInputId = Text(transition["targetInputId"]);
        var sibling = ForwardedDefinitionItems(
                ForwardedDefinitions(container)
                ?? throw new InvalidOperationException("Forwarded transition owner has no forwarding envelope."))
            .FirstOrDefault((candidate) =>
                Text(candidate["sourceInputId"]).Equals(targetSourceInputId, StringComparison.Ordinal));
        if (sibling is null)
        {
            definition.Remove("transition");
            return;
        }
        transition["targetInputId"] = Text(sibling["id"]);
    }

    private static void RebaseNaturalTimingForParent(JsonObject definition, JsonObject container)
    {
        if (definition["naturalTiming"] is not JsonObject timing) return;
        var sourceInputId = Text(timing["sourceFieldId"]);
        var sibling = ForwardedDefinitionItems(
                ForwardedDefinitions(container)
                ?? throw new InvalidOperationException("Forwarded timing owner has no forwarding envelope."))
            .FirstOrDefault((candidate) =>
                Text(candidate["sourceInputId"]).Equals(sourceInputId, StringComparison.Ordinal));
        if (sibling is null)
            throw new InvalidOperationException($"Forwarded BehaviorTiming input requires forwarded source field '{sourceInputId}'.");
        timing["sourceFieldId"] = Text(sibling["id"]);
    }

    public static JsonObject Definition(
        FieldDefinition owner,
        ComponentInputBindingDefinition input,
        string runtimeLabel,
        string defaultValue)
    {
        var id = $"forwarded.{owner.Id}.{input.Id}";
        var jsonKey = string.Join("_", id.Select((character) => char.IsLetterOrDigit(character) ? character : '_'));
        return new JsonObject
        {
            ["id"] = id,
            ["sourceInputId"] = input.Id,
            ["label"] = runtimeLabel,
            ["jsonKey"] = jsonKey,
            ["kind"] = RuntimeInputValueKindContract.InputKind(input.ValueKind),
            ["valueKind"] = input.ValueKind.ToString(),
            ["defaultValue"] = defaultValue,
            ["source"] = "runtime",
            ["componentType"] = input.ComponentType,
            ["tableId"] = input.TableId,
            ["resolvedJsonKey"] = string.IsNullOrWhiteSpace(input.ResolvedJsonKey)
                ? ""
                : $"{jsonKey}_resolved",
            ["targetResolvedJsonKey"] = input.ResolvedJsonKey,
            ["minimum"] = input.Number?.Minimum,
            ["maximum"] = input.Number?.Maximum,
            ["increment"] = input.Number?.Increment,
            ["options"] = input.Options is null
                ? new JsonArray()
                : new JsonArray(input.Options.Select((option) => (JsonNode?)new JsonObject
                {
                    ["value"] = option.Value,
                    ["label"] = option.Label,
                }).ToArray()),
            ["transition"] = TransitionNode(input.Transition),
            ["animatable"] = input.Animation is not null,
            ["animationInterpolations"] = input.Animation is null
                ? null
                : new JsonArray(input.Animation.Interpolations.Select((value) => (JsonNode?)value).ToArray()),
            ["animationTimeline"] = input.Animation is null
                ? null
                : new JsonObject { ["extendsOwnerDuration"] = input.Animation.ExtendsOwnerDuration },
            ["naturalTiming"] = NaturalTimingNode(input.BehaviorTiming),
            ["actionOnly"] = input.ActionOnly,
        };
    }

    private static JsonObject? NaturalTimingNode(BehaviorTimingDefinition? timing) => timing is null
        ? null
        : new JsonObject
        {
            ["sourceFieldId"] = timing.SourceFieldId,
            ["unit"] = timing.Unit,
            ["baseFramesPerUnit"] = timing.BaseFramesPerUnit,
        };

    private static JsonObject? TransitionNode(ComponentInputTransitionDefinition? transition) =>
        transition is null
            ? null
            : new JsonObject
            {
                ["targetInputId"] = transition.TargetInputId,
                ["triggerValues"] = new JsonArray(
                    transition.TriggerValues.Select((value) => (JsonNode?)value).ToArray()),
                ["replacementValue"] = transition.ReplacementValue,
                ["targetValuePattern"] = transition.TargetValuePattern,
                ["forwardedTargetOnly"] = transition.ForwardedTargetOnly,
            };

    public static void Visit(
        JsonNode? node,
        Action<JsonObject, string, JsonObject> visitor)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var child in array) Visit(child, visitor);
                break;
            case JsonObject obj:
                if (ForwardedDefinitions(obj) is { } forwards)
                {
                    foreach (var (targetKey, definitionNode) in forwards)
                    {
                        var definition = definitionNode as JsonObject
                            ?? throw new InvalidOperationException(
                                $"Forwarded runtime input '{targetKey}' definition must be an object.");
                        visitor(obj, targetKey, definition);
                    }
                }
                foreach (var (key, child) in obj.ToList())
                {
                    if (!key.Equals(StorageKey, StringComparison.Ordinal)) Visit(child, visitor);
                }
                break;
        }
    }

    public static void RebaseIds(JsonNode node, string oldOwnerSegment, string newOwnerSegment)
    {
        Visit(node, (_, _, definition) =>
        {
            var id = Text(definition["id"]);
            if (id.Length == 0) return;
            var nextId = id.Replace(oldOwnerSegment, newOwnerSegment, StringComparison.Ordinal);
            var nextJsonKey = string.Join("_", nextId.Select((character) =>
                char.IsLetterOrDigit(character) ? character : '_'));
            definition["id"] = nextId;
            definition["jsonKey"] = nextJsonKey;
            if (Text(definition["targetResolvedJsonKey"]).Length > 0)
            {
                definition["resolvedJsonKey"] = $"{nextJsonKey}_resolved";
            }
        });
    }

    public static IReadOnlyList<string> Labels(JsonNode? node)
    {
        var labels = new List<string>();
        Visit(node, (_, _, definition) =>
        {
            var label = Text(definition["label"]);
            if (label.Length == 0) label = Text(definition["id"]);
            if (label.Length > 0 && !labels.Contains(label, StringComparer.Ordinal))
            {
                labels.Add(label);
            }
        });
        return labels;
    }

    private static string StorageText(JsonNode? node) => node switch
    {
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        JsonValue value when value.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
        JsonValue value when value.GetValueKind() == System.Text.Json.JsonValueKind.Number =>
            value.ToJsonString(),
        JsonArray or JsonObject => node.ToJsonString(),
        _ => throw new InvalidOperationException("Forwarded runtime input has no supported Variant value."),
    };

    private static JsonArray RequiredOrCreateArray(JsonObject owner, string key, string context)
    {
        if (!owner.TryGetPropertyValue(key, out var node))
        {
            var created = new JsonArray();
            owner[key] = created;
            return created;
        }
        return node as JsonArray
            ?? throw new InvalidOperationException($"{context} '{key}' must be an array.");
    }

    private static JsonArray? OptionalArray(JsonObject owner, string key, string context)
    {
        if (!owner.TryGetPropertyValue(key, out var node)) return null;
        return node as JsonArray
            ?? throw new InvalidOperationException($"{context} '{key}' must be an array when present.");
    }

    private static IEnumerable<JsonObject> ObjectItems(JsonArray items, string context)
    {
        for (var index = 0; index < items.Count; index++)
        {
            yield return items[index] as JsonObject
                ?? throw new InvalidOperationException($"{context} item at index {index} must be an object.");
        }
    }

    private static JsonObject? ForwardedDefinitions(JsonObject owner)
    {
        if (!owner.TryGetPropertyValue(StorageKey, out var node)) return null;
        return node as JsonObject
            ?? throw new InvalidOperationException($"{StorageKey} must be an object when present.");
    }

    private static IEnumerable<JsonObject> ForwardedDefinitionItems(JsonObject forwards)
    {
        foreach (var (targetKey, node) in forwards)
        {
            yield return node as JsonObject
                ?? throw new InvalidOperationException(
                    $"Forwarded runtime input '{targetKey}' definition must be an object.");
        }
    }

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
}
