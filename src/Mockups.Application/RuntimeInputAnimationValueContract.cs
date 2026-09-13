using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record RuntimeInputAnimationTargetDefinition(
    string FieldId,
    string TargetId,
    ComponentInputDefinition Input);

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
            ComponentInputDefinition>();
        AddDeclarations(
            declarations,
            values,
            RuntimeInputDefinitionReader.ReadInputs(
                runtimePreview,
                config,
                includeHidden: true),
            "");
        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(
                     runtimePreview,
                     config,
                     includeHidden: true))
        {
            var collectionNode = values[collection.StorageJsonKey]
                ?? values[collection.JsonKey];
            if (collectionNode is not JsonArray items) continue;
            foreach (var item in items.OfType<JsonObject>())
            {
                AddDeclarations(
                    declarations,
                    item,
                    collection.Fields,
                    JsonPath.RequiredString(
                        item,
                        "id",
                        $"Runtime collection '{collection.Id}' item"));
            }
        }
        return declarations.Select((entry) =>
                new RuntimeInputAnimationTargetDefinition(
                    entry.Key.FieldId,
                    entry.Key.TargetId,
                    entry.Value))
            .ToArray();
    }

    private static void AddDeclarations(
        IDictionary<(string FieldId, string TargetId), ComponentInputDefinition> declarations,
        JsonObject values,
        IReadOnlyList<ComponentInputDefinition> inputs,
        string targetId)
    {
        foreach (var input in inputs)
        {
            if (input.Animation is not null)
            {
                declarations.Add((input.Id, targetId), input);
            }
            if (input.StructuredCollection is null
                || values[input.JsonKey] is not JsonArray nestedItems)
            {
                continue;
            }
            foreach (var nestedItem in nestedItems.OfType<JsonObject>())
            {
                AddDeclarations(
                    declarations,
                    nestedItem,
                    input.StructuredCollection.Fields,
                    JsonPath.RequiredString(
                        nestedItem,
                        "id",
                        $"Runtime structured collection '{input.Id}' item"));
            }
        }
    }
}
