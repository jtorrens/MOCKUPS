using Mockups.DesktopEditorShell.Data;
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

    public NestedRuntimeRecordReferenceResolver(SpikeDatabase database)
    {
        _recordInputResolver = new ComponentPreviewRecordInputResolver(database);
    }

    public void Resolve(
        JsonNode? root,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        Visit(root, themeMode, paletteColors);
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

        foreach (var input in ComponentPreviewInputSession.ReadRuntimeInputs(values, new JsonObject())
                     .Where((input) => input.Kind == ComponentInputKind.RecordReference
                         && !string.IsNullOrWhiteSpace(input.ResolvedJsonKey)))
        {
            var recordId = values[input.JsonKey]?.GetValue<string>() ?? "";
            values[input.ResolvedJsonKey] = _recordInputResolver.ResolvedPreviewValue(
                input.TableId,
                recordId,
                themeMode,
                paletteColors,
                input.Id);
        }
    }
}
