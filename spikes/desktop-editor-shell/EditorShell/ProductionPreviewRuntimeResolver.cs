using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

/// <summary>
/// Resolves declared runtime references for persisted Production payloads.
/// It deliberately does not apply Test Values, action clocks or scalar input state.
/// </summary>
internal sealed class ProductionPreviewRuntimeResolver
{
    private readonly ComponentPreviewRecordInputResolver _recordInputResolver;

    public ProductionPreviewRuntimeResolver(SpikeDatabase database)
    {
        _recordInputResolver = new ComponentPreviewRecordInputResolver(database);
    }

    public DesignPreviewPayload Resolve(DesignPreviewPayload payload, string themeMode)
    {
        var preview = ParseObject(payload.DesignPreviewJson);
        var config = ParseObject(payload.ConfigJson);
        var inputs = ComponentPreviewInputSession.ReadRuntimeInputs(preview, config);

        foreach (var input in inputs.Where((input) =>
                     input.Kind == ComponentInputKind.RecordReference
                     && !string.IsNullOrWhiteSpace(input.ResolvedJsonKey)))
        {
            var recordId = preview[input.JsonKey]?.GetValue<string>() ?? "";
            preview[input.ResolvedJsonKey] = _recordInputResolver.ResolvedPreviewValue(
                input.TableId,
                recordId,
                themeMode,
                payload.PaletteColors,
                input.Id);
        }

        foreach (var collection in ComponentPreviewInputSession.ReadRuntimeCollections(preview, config))
        {
            if (preview[collection.JsonKey] is not JsonArray items) continue;
            foreach (var item in items.OfType<JsonObject>())
            {
                foreach (var input in collection.Fields.Where((field) =>
                             field.Kind == ComponentInputKind.RecordReference
                             && !string.IsNullOrWhiteSpace(field.ResolvedJsonKey)))
                {
                    var recordId = item[input.JsonKey]?.GetValue<string>() ?? "";
                    item[input.ResolvedJsonKey] = _recordInputResolver.ResolvedPreviewValue(
                        input.TableId,
                        recordId,
                        themeMode,
                        payload.PaletteColors,
                        input.Id);
                }
            }
        }

        preview.Remove("testValues");
        preview.Remove("actions");
        if (preview["collections"] is JsonArray collections)
        {
            foreach (var collection in collections.OfType<JsonObject>())
            {
                collection.Remove("itemActions");
            }
        }

        return payload with { DesignPreviewJson = preview.ToJsonString() };
    }

    private static JsonObject ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject
                ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }
}
