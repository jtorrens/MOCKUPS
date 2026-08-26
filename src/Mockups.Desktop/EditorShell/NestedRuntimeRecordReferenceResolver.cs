using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

/// <summary>
/// Resolves record-reference values in any nested object that carries its own
/// runtime-input contract. The contract, rather than the containing editor or
/// component class, determines which values need resolution.
/// </summary>
internal sealed class NestedRuntimeRecordReferenceResolver
{
    private readonly ComponentPreviewRecordInputResolver _recordInputResolver;

    public NestedRuntimeRecordReferenceResolver(
        ActorPreviewDataSource actorDataSource,
        IProjectPathResolver projectPaths)
    {
        _recordInputResolver = new ComponentPreviewRecordInputResolver(
            actorDataSource,
            projectPaths);
    }

    public void Resolve(
        JsonNode? root,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        Visit(root, themeMode, paletteColors);
    }

    public void ResolveDeclaredValues(
        JsonObject values,
        IReadOnlyList<ComponentInputDefinition> inputs,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        foreach (var input in inputs.Where((field) =>
                     field.Kind == ComponentInputKind.RecordReference
                     && !string.IsNullOrWhiteSpace(field.ResolvedJsonKey)))
        {
            var recordId = values[input.JsonKey]?.GetValue<string>() ?? "";
            values[input.ResolvedJsonKey] = _recordInputResolver.ResolvedPreviewValue(
                input.TableId,
                recordId,
                themeMode,
                paletteColors,
                input.Id,
                CollectionFieldAvailability.AllowsEmpty(values, input));
        }

        foreach (var input in inputs.Where((field) =>
                     field.ValueKind == ValueKind.StructuredCollection
                     && field.StructuredCollection is not null))
        {
            if (values[input.JsonKey] is not JsonArray items) continue;
            foreach (var item in items.OfType<JsonObject>())
            {
                ResolveDeclaredValues(
                    item,
                    input.StructuredCollection!.Fields,
                    themeMode,
                    paletteColors);
            }
        }
    }

    public JsonObject CreateAnimationCatalog(
        JsonObject values,
        JsonObject config,
        JsonObject animation,
        string projectId,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        var catalog = new JsonObject();
        var tracks = JsonPath.RequiredArray(
            animation,
            "tracks",
            "Runtime animation document");
        CollectAnimationReferences(
            catalog,
            tracks,
            values,
            RuntimeInputDefinitionReader.ReadInputs(values, config),
            "",
            projectId,
            themeMode,
            paletteColors);

        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(values, config))
        {
            if (values[collection.JsonKey] is not JsonArray items) continue;
            foreach (var item in items.OfType<JsonObject>())
            {
                var targetId = JsonPath.RequiredString(
                    item,
                    "id",
                    $"Runtime collection '{collection.Id}' item");
                CollectAnimationReferences(
                    catalog,
                    tracks,
                    item,
                    collection.Fields,
                    targetId,
                    projectId,
                    themeMode,
                    paletteColors);
            }
        }

        return catalog;
    }

    private void CollectAnimationReferences(
        JsonObject catalog,
        JsonArray tracks,
        JsonObject values,
        IReadOnlyList<ComponentInputDefinition> inputs,
        string targetId,
        string projectId,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        foreach (var input in inputs)
        {
            if (input.Kind == ComponentInputKind.RecordReference)
            {
                var track = tracks.OfType<JsonObject>().SingleOrDefault((candidate) =>
                    JsonPath.RequiredString(candidate, "fieldId", "Runtime animation track")
                        .Equals(input.Id, StringComparison.Ordinal)
                    && (candidate["targetId"]?.GetValue<string>() ?? "")
                        .Equals(targetId, StringComparison.Ordinal));
                if (track is not null)
                {
                    foreach (var keyframe in JsonPath.RequiredArray(
                                 track,
                                 "keyframes",
                                 $"Runtime animation track '{input.Id}'").OfType<JsonObject>())
                    {
                        var recordId = JsonPath.RequiredString(
                            keyframe,
                            "value",
                            $"Runtime animation track '{input.Id}' keyframe");
                        if (!_recordInputResolver.ProjectId(input.TableId, recordId, input.Id)
                                .Equals(projectId, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                $"Runtime animation track '{input.Id}' references record '{recordId}' outside Project '{projectId}'.");
                        }
                        var table = catalog[input.TableId] as JsonObject;
                        if (table is null)
                        {
                            table = new JsonObject();
                            catalog[input.TableId] = table;
                        }
                        table[recordId] = _recordInputResolver.ResolvedPreviewValue(
                            input.TableId,
                            recordId,
                            themeMode,
                            paletteColors,
                            input.Id);
                    }
                }
            }

            if (input.StructuredCollection is null
                || values[input.JsonKey] is not JsonArray nestedItems)
            {
                continue;
            }
            foreach (var nestedItem in nestedItems.OfType<JsonObject>())
            {
                CollectAnimationReferences(
                    catalog,
                    tracks,
                    nestedItem,
                    input.StructuredCollection.Fields,
                    JsonPath.RequiredString(
                        nestedItem,
                        "id",
                        $"Runtime structured collection '{input.Id}' item"),
                    projectId,
                    themeMode,
                    paletteColors);
            }
        }
    }

    private void Visit(
        JsonNode? node,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var child in array.ToList())
                {
                    Visit(child, themeMode, paletteColors);
                }
                break;
            case JsonObject obj:
                ResolveDeclaredInputs(obj, themeMode, paletteColors);
                foreach (var (key, child) in obj.ToList())
                {
                    if (!key.Equals("inputs", StringComparison.Ordinal) || child is not JsonArray)
                    {
                        Visit(child, themeMode, paletteColors);
                    }
                }
                break;
        }
    }

    private void ResolveDeclaredInputs(
        JsonObject values,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        if (values["inputs"] is not JsonArray) return;

        ResolveDeclaredValues(
            values,
            RuntimeInputDefinitionReader.ReadInputs(values, new JsonObject()),
            themeMode,
            paletteColors);
    }
}
