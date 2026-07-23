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

    public NestedRuntimeRecordReferenceResolver(ActorPreviewDataSource actorDataSource)
    {
        _recordInputResolver = new ComponentPreviewRecordInputResolver(actorDataSource);
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
            ComponentPreviewInputSession.ReadRuntimeInputs(values, new JsonObject()),
            themeMode,
            paletteColors);
    }
}
