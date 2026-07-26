using Mockups.DesktopEditorShell.Common;
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
    private readonly NestedRuntimeRecordReferenceResolver _nestedRecordInputResolver;

    public ProductionPreviewRuntimeResolver(SpikeDatabase database)
    {
        var actorDataSource = new ActorPreviewDataSource(database);
        _nestedRecordInputResolver = new NestedRuntimeRecordReferenceResolver(actorDataSource);
    }

    public DesignPreviewPayload Resolve(DesignPreviewPayload payload, string themeMode)
    {
        var preview = ParseObject(payload.DesignPreviewJson);
        var config = ParseObject(payload.ConfigJson);
        _nestedRecordInputResolver.Resolve(config, themeMode, payload.PaletteColors);
        var inputs = ComponentPreviewInputSession.ReadRuntimeInputs(preview, config);

        _nestedRecordInputResolver.ResolveDeclaredValues(
            preview,
            inputs,
            themeMode,
            payload.PaletteColors);

        foreach (var collection in ComponentPreviewInputSession.ReadRuntimeCollections(preview, config))
        {
            if (preview[collection.JsonKey] is not JsonArray items) continue;
            foreach (var item in items.OfType<JsonObject>())
            {
                _nestedRecordInputResolver.ResolveDeclaredValues(
                    item,
                    collection.Fields,
                    themeMode,
                    payload.PaletteColors);
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

        _nestedRecordInputResolver.Resolve(preview, themeMode, payload.PaletteColors);

        return payload with
        {
            ConfigJson = config.ToJsonString(),
            DesignPreviewJson = preview.ToJsonString(),
        };
    }

    private static JsonObject ParseObject(string json)
    {
        return JsonPath.ParseRequiredObject(json, "Production Preview payload JSON");
    }
}
