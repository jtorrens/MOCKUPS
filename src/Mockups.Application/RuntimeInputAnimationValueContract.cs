using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record RuntimeInputAnimationTargetDefinition(
    string FieldId,
    string TargetId,
    ComponentInputDefinition Input,
    string BaseValue);

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
        var declarations = new Dictionary<
            (string FieldId, string TargetId),
            (ComponentInputDefinition Input, string BaseValue)>();
        AddDeclarations(
            declarations,
            values,
            RuntimeInputDefinitionReader.ReadInputs(
                runtimePreview,
                config,
                includeHidden: true),
            "",
            "");
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
                "");
        }
        return declarations.Select((entry) =>
                new RuntimeInputAnimationTargetDefinition(
                    entry.Key.FieldId,
                    entry.Key.TargetId,
                    entry.Value.Input,
                    entry.Value.BaseValue))
            .ToArray();
    }

    private static void AddDeclarations(
        IDictionary<(string FieldId, string TargetId), (ComponentInputDefinition Input, string BaseValue)> declarations,
        JsonObject values,
        IReadOnlyList<ComponentInputDefinition> inputs,
        string targetId,
        string prefix)
    {
        foreach (var input in inputs)
        {
            var fieldId = RuntimeNestedAnimationFieldContract.Join(prefix, input.Id);
            if (input.Animation is not null)
            {
                AddDeclaration(
                    declarations,
                    fieldId,
                    targetId,
                    input,
                    DesignPreviewTestValues.CollectionValue(values, input));
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
                    fieldId);
            }
        }
    }

    private static void AddCollectionDeclarations(
        IDictionary<(string FieldId, string TargetId), (ComponentInputDefinition Input, string BaseValue)> declarations,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<JsonObject> items,
        string inheritedTargetId,
        string prefix)
    {
        foreach (var item in items)
        {
            var itemId = JsonPath.RequiredString(
                item,
                "id",
                $"Runtime collection '{collection.Id}' item");
            var targetId = string.IsNullOrWhiteSpace(inheritedTargetId)
                && string.IsNullOrWhiteSpace(prefix)
                    ? itemId
                    : inheritedTargetId;
            var itemPrefix = string.IsNullOrWhiteSpace(prefix)
                ? ""
                : RuntimeNestedAnimationFieldContract.Join(prefix, itemId);
            AddDeclarations(
                declarations,
                item,
                collection.Fields,
                targetId,
                itemPrefix);

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
                itemPrefix);
        }
    }

    private static void AddDeclaration(
        IDictionary<(string FieldId, string TargetId), (ComponentInputDefinition Input, string BaseValue)> declarations,
        string fieldId,
        string targetId,
        ComponentInputDefinition input,
        string baseValue)
    {
        if (!declarations.TryAdd((fieldId, targetId), (input, baseValue)))
        {
            throw new InvalidOperationException(
                $"Duplicate Runtime Input animation target '{fieldId}'/'{targetId}'.");
        }
    }
}
