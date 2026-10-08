using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record RuntimeInputAnimationTargetDefinition(
    IReadOnlyList<RuntimeAnimationFieldSegment> FieldPath,
    string TargetId,
    ComponentInputDefinition Input,
    string BaseValue,
    IReadOnlyList<string> OwnerItemIds)
{
    public string FieldId => RuntimeNestedAnimationFieldContract.Join(FieldPath.Select(segment => segment.Id).ToArray());

    public (string FieldId, string TargetId) Rebase(IReadOnlyDictionary<string, string> ids) =>
        (RuntimeNestedAnimationFieldContract.Join(FieldPath.Select(segment =>
            segment.IsItem && ids.TryGetValue(segment.Id, out var next) ? next : segment.Id).ToArray()),
         ids.TryGetValue(TargetId, out var target) ? target : TargetId);
}

public sealed record RuntimeAnimationFieldSegment(string Id, bool IsItem);

public static class RuntimeInputAnimationValueContract
{
    public static void Validate(
        JsonObject runtimePreview,
        JsonObject animation,
        IReadOnlyDictionary<string, IReadOnlySet<string>> recordIdsByTable,
        string owner)
    {
        ModuleInstanceAnimationDocumentContract.Validate(animation, owner);
        var declarations = ReadTargets(
                runtimePreview,
                new JsonObject(),
                runtimePreview)
            .ToDictionary(
                (target) => (target.FieldId, target.TargetId),
                (target) => target.Input);

        foreach (var track in JsonPath.RequiredArray(animation, "tracks", owner)
                     .OfType<JsonObject>())
        {
            var fieldId = JsonPath.RequiredString(track, "fieldId", owner);
            var targetId = track["targetId"]?.GetValue<string>() ?? "";
            if (!declarations.TryGetValue((fieldId, targetId), out var definition))
            {
                throw new InvalidOperationException(
                    $"{owner} targets undeclared Runtime Input '{fieldId}'/'{targetId}'.");
            }
            var animationDefinition = definition.Animation!;
            foreach (var keyframe in JsonPath.RequiredArray(
                         track,
                         "keyframes",
                         $"{owner} track '{fieldId}'").OfType<JsonObject>())
            {
                var keyframeOwner = $"{owner} track '{fieldId}' keyframe";
                var interpolation = JsonPath.RequiredString(
                    keyframe,
                    "interpolation",
                    keyframeOwner);
                if (!animationDefinition.Interpolations.Contains(
                        interpolation,
                        StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{keyframeOwner} interpolation '{interpolation}' is not declared.");
                }
                RuntimeInputValueKindContract.ValidateRuntimeValue(
                    definition,
                    keyframe["value"],
                    keyframeOwner);
            }
            if (definition.Kind != ComponentInputKind.RecordReference)
            {
                continue;
            }
            if (!recordIdsByTable.TryGetValue(
                    definition.TableId,
                    out var recordIds))
            {
                throw new InvalidOperationException(
                    $"{owner} has no record catalog for Runtime table '{definition.TableId}'.");
            }
            foreach (var keyframe in JsonPath.RequiredArray(
                         track,
                         "keyframes",
                         $"{owner} track '{fieldId}'").OfType<JsonObject>())
            {
                var recordId = JsonPath.RequiredString(
                    keyframe,
                    "value",
                    $"{owner} track '{fieldId}' keyframe");
                if (!recordIds.Contains(recordId))
                {
                    throw new InvalidOperationException(
                        $"{owner} track '{fieldId}' references missing or cross-Project {definition.TableId} record '{recordId}'.");
                }
            }
        }
    }

    public static IReadOnlyList<RuntimeInputAnimationTargetDefinition> ReadTargets(
        JsonObject runtimePreview,
        JsonObject config,
        JsonObject values)
    {
        var declarations = new Dictionary<(string FieldId, string TargetId), RuntimeInputAnimationTargetDefinition>();
        AddDeclarations(
            declarations,
            values,
            RuntimeInputDefinitionReader.ReadInputs(
                runtimePreview,
                config,
                includeHidden: true),
            "",
            [],
            []);
        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(
                     runtimePreview,
                     config,
                     includeHidden: true))
        {
            var collectionNode = values[collection.StorageJsonKey]
                ?? values[collection.JsonKey];
            if (collectionNode is not JsonArray items) continue;
            AddCollectionDeclarations(
                declarations,
                collection,
                items.OfType<JsonObject>().ToArray(),
                "",
                [],
                []);
        }
        return declarations.Values.ToArray();
    }

    public static IReadOnlyList<RuntimeInputAnimationTargetDefinition> ReadCollectionTargets(
        RuntimeInputCollectionDefinition collection,
        JsonArray items)
    {
        var declarations = new Dictionary<(string FieldId, string TargetId), RuntimeInputAnimationTargetDefinition>();
        AddCollectionDeclarations(declarations, collection, items.OfType<JsonObject>().ToArray(), "", [], []);
        return declarations.Values.ToArray();
    }

    private static void AddDeclarations(
        IDictionary<(string FieldId, string TargetId), RuntimeInputAnimationTargetDefinition> declarations,
        JsonObject values,
        IReadOnlyList<ComponentInputDefinition> inputs,
        string targetId,
        IReadOnlyList<RuntimeAnimationFieldSegment> prefix,
        IReadOnlyList<string> ownerItemIds)
    {
        foreach (var input in inputs)
        {
            RuntimeAnimationFieldSegment[] fieldPath = [.. prefix, new(input.Id, false)];
            var fieldId = RuntimeNestedAnimationFieldContract.Join(fieldPath.Select(segment => segment.Id).ToArray());
            if (input.Animation is not null)
            {
                AddDeclaration(
                    declarations,
                    fieldId,
                    targetId,
                    RuntimeInputOptionSourceContract.Close(input, values),
                    DesignPreviewTestValues.CollectionValue(values, input),
                    fieldPath,
                    ownerItemIds);
            }
            if (input.StructuredCollection is null
                || values[input.JsonKey] is not JsonArray nestedItems)
            {
                continue;
            }
            foreach (var nestedItem in nestedItems.OfType<JsonObject>())
            {
                AddCollectionDeclarations(
                    declarations,
                    input.StructuredCollection,
                    [nestedItem],
                    targetId,
                    fieldPath,
                    ownerItemIds);
            }
        }
    }

    private static void AddCollectionDeclarations(
        IDictionary<(string FieldId, string TargetId), RuntimeInputAnimationTargetDefinition> declarations,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<JsonObject> items,
        string inheritedTargetId,
        IReadOnlyList<RuntimeAnimationFieldSegment> prefix,
        IReadOnlyList<string> ownerItemIds)
    {
        foreach (var item in items)
        {
            var itemId = JsonPath.RequiredString(
                item,
                "id",
                $"Runtime collection '{collection.Id}' item");
            var targetId = string.IsNullOrWhiteSpace(inheritedTargetId)
                && prefix.Count == 0
                    ? itemId
                    : inheritedTargetId;
            RuntimeAnimationFieldSegment[] itemPrefix = prefix.Count == 0
                ? []
                : [.. prefix, new(itemId, true)];
            string[] itemOwners = [.. ownerItemIds, itemId];
            AddDeclarations(
                declarations,
                item,
                collection.Fields,
                targetId,
                itemPrefix,
                itemOwners);

            var runtimeKey = !string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey)
                ? collection.ItemRuntimeContractJsonKey
                : collection.ComponentItems?.InputsJsonKey ?? "";
            if (runtimeKey.Length == 0) continue;
            var runtime = JsonPath.RequiredObject(
                item,
                runtimeKey,
                $"Runtime collection '{collection.Id}' item '{itemId}'");
            AddDeclarations(
                declarations,
                runtime,
                RuntimeInputDefinitionReader.ReadInputs(
                    runtime,
                    new JsonObject(),
                    includeHidden: true),
                targetId,
                itemPrefix,
                itemOwners);
        }
    }

    private static void AddDeclaration(
        IDictionary<(string FieldId, string TargetId), RuntimeInputAnimationTargetDefinition> declarations,
        string fieldId,
        string targetId,
        ComponentInputDefinition input,
        string baseValue,
        IReadOnlyList<RuntimeAnimationFieldSegment> fieldPath,
        IReadOnlyList<string> ownerItemIds)
    {
        if (!declarations.TryAdd((fieldId, targetId), new(fieldPath, targetId, input, baseValue, ownerItemIds)))
        {
            throw new InvalidOperationException(
                $"Duplicate Runtime Input animation target '{fieldId}'/'{targetId}'.");
        }
    }
}
