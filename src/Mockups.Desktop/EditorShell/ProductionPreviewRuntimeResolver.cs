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

    public ProductionPreviewRuntimeResolver(
        IActorPreviewRepository database,
        IProjectPathResolver projectPaths)
    {
        var actorDataSource = new ActorPreviewDataSource(database);
        _nestedRecordInputResolver =
            new NestedRuntimeRecordReferenceResolver(
                actorDataSource,
                projectPaths);
    }

    public DesignPreviewPayload Resolve(DesignPreviewPayload payload, string themeMode)
    {
        if (payload.ScreenTransition is { } transition)
        {
            var outgoing =
                Resolve(
                    transition.Outgoing,
                    themeMode);
            var incoming =
                Resolve(
                    transition.Incoming,
                    themeMode);
            return payload with
            {
                ConfigJson = incoming.ConfigJson,
                DesignPreviewJson =
                    incoming.DesignPreviewJson,
                RuntimeContractJson =
                    incoming.RuntimeContractJson,
                InstanceJson = incoming.InstanceJson,
                ScreenTransition =
                    transition with
                    {
                        Outgoing = outgoing,
                        Incoming = incoming,
                    },
            };
        }

        var preview = ParseObject(payload.DesignPreviewJson);
        var timelineFrameBefore =
            ResolvedTimelineFrame(preview);
        var config = ParseObject(payload.ConfigJson);
        var instance = ParseObject(payload.InstanceJson);
        var animation = JsonPath.RequiredObject(
            instance,
            "animation",
            "Production Preview instance envelope");
        var runtimeRecordReferences =
            _nestedRecordInputResolver.CreateAnimationCatalog(
                preview,
                config,
                animation,
                payload.ProjectId,
                themeMode,
                payload.PaletteColors);
        _nestedRecordInputResolver.Resolve(config, themeMode, payload.PaletteColors);
        var inputs = RuntimeInputDefinitionReader.ReadInputs(preview, config);

        _nestedRecordInputResolver.ResolveDeclaredValues(
            preview,
            inputs,
            themeMode,
            payload.PaletteColors);

        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(preview, config))
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

        var resolved = payload with
        {
            ConfigJson = config.ToJsonString(),
            DesignPreviewJson = preview.ToJsonString(),
            RuntimeRecordReferencesJson = runtimeRecordReferences.ToJsonString(),
        };
        var timelineFrameAfter =
            ResolvedTimelineFrame(preview);
        if (timelineFrameAfter
            != timelineFrameBefore)
        {
            throw new InvalidOperationException(
                $"Production runtime resolution changed local frame from {timelineFrameBefore} to {timelineFrameAfter}.");
        }

        return resolved;
    }

    private static int ResolvedTimelineFrame(
        JsonObject preview)
    {
        var key =
            preview["timelineFrameJsonKey"]
                ?.GetValue<string>() ?? "";
        return !string.IsNullOrWhiteSpace(key)
            && preview[key] is JsonValue value
            && value.TryGetValue<int>(out var frame)
                ? frame
                : 0;
    }

    private static JsonObject ParseObject(string json)
    {
        return JsonPath.ParseRequiredObject(json, "Production Preview payload JSON");
    }
}
