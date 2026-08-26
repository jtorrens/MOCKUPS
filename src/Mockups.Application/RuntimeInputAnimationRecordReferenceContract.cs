using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class RuntimeInputAnimationRecordReferenceContract
{
    public static void Validate(
        JsonObject runtimePreview,
        JsonObject animation,
        IReadOnlyDictionary<string, IReadOnlySet<string>> recordIdsByTable,
        string owner)
    {
        ModuleInstanceAnimationDocumentContract.Validate(animation, owner);
        var declarations =
            new Dictionary<(string FieldId, string TargetId), ComponentInputDefinition>();
        AddDeclarations(
            declarations,
            runtimePreview,
            RuntimeInputDefinitionReader.ReadInputs(runtimePreview, new JsonObject()),
            "");
        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(
                     runtimePreview,
                     new JsonObject()))
        {
            if (runtimePreview[collection.JsonKey] is not JsonArray items) continue;
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

        foreach (var track in JsonPath.RequiredArray(animation, "tracks", owner)
                     .OfType<JsonObject>())
        {
            var fieldId = JsonPath.RequiredString(track, "fieldId", owner);
            var targetId = track["targetId"]?.GetValue<string>() ?? "";
            if (!declarations.TryGetValue((fieldId, targetId), out var definition))
            {
                continue;
            }
            if (!recordIdsByTable.TryGetValue(definition.TableId, out var recordIds))
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

    private static void AddDeclarations(
        IDictionary<(string FieldId, string TargetId), ComponentInputDefinition> declarations,
        JsonObject values,
        IReadOnlyList<ComponentInputDefinition> inputs,
        string targetId)
    {
        foreach (var input in inputs)
        {
            if (input.Kind == ComponentInputKind.RecordReference)
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
