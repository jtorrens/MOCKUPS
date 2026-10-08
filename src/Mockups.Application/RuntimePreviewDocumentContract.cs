using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

/// <summary>
/// The one preparation boundary for a Runtime Preview document. Configuration
/// ownership is resolved before Runtime values are overlaid, for both Design
/// fixtures and Production Screen content.
/// </summary>
public static class RuntimePreviewDocumentContract
{
    public static JsonObject PrepareDeclaredInputDefaults(
        JsonObject runtimeContract,
        JsonObject effectiveConfig)
    {
        var values = new JsonObject();
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(
                     runtimeContract, effectiveConfig))
        {
            if (input.Source != ComponentInputSource.Runtime) continue;
            if (values.ContainsKey(input.JsonKey))
                throw new InvalidOperationException(
                    $"Runtime defaults contain duplicate storage key '{input.JsonKey}'.");
            values[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, input.DefaultValue);
        }
        return values;
    }

    public static JsonObject PrepareFixture(
        JsonObject previewFixture,
        JsonObject effectiveConfig,
        Func<string, JsonObject>? componentVariantConfig = null,
        Func<string, JsonObject>? componentRuntimeValues = null)
    {
        var prepared = RuntimeInputForwardingContract.EffectivePreview(
            previewFixture,
            effectiveConfig);
        StructuredRuntimeCollectionProjection.Apply(
            prepared,
            effectiveConfig,
            componentVariantConfig,
            componentRuntimeValues);
        RuntimeTemporalPhaseContract.Hydrate(prepared, effectiveConfig);
        PrepareNestedRuntimeContracts(
            prepared,
            effectiveConfig,
            componentVariantConfig,
            componentRuntimeValues);
        RuntimeInputOptionSourceContract.ValidateValues(prepared, effectiveConfig);
        return prepared;
    }

    public static JsonObject PrepareInputValues(
        JsonObject inputValues,
        JsonObject runtimeContract,
        JsonObject effectiveConfig,
        Func<string, JsonObject>? componentVariantConfig = null,
        Func<string, JsonObject>? componentRuntimeValues = null)
    {
        var prepared = PrepareRuntime(
            runtimeContract,
            effectiveConfig,
            inputValues,
            componentVariantConfig,
            componentRuntimeValues);
        var result = new JsonObject();
        foreach (var (key, originalValue) in inputValues)
        {
            if (!prepared.TryGetPropertyValue(key, out var value))
            {
                if (key.Equals(
                        RuntimeInputForwardingContract.StorageKey,
                        StringComparison.Ordinal))
                {
                    result[key] = originalValue?.DeepClone();
                    continue;
                }
                throw new InvalidOperationException(
                    $"Prepared Runtime Input values are missing '{key}'.");
            }
            result[key] = value?.DeepClone();
        }
        return result;
    }

    public static JsonObject PrepareRuntime(
        JsonObject previewFixture,
        JsonObject effectiveConfig,
        JsonObject runtimeValues,
        Func<string, JsonObject>? componentVariantConfig = null,
        Func<string, JsonObject>? componentRuntimeValues = null)
    {
        var prepared = PrepareFixture(
            previewFixture,
            effectiveConfig,
            componentVariantConfig,
            componentRuntimeValues);
        var current = RuntimeInputDocumentContract.CreateContentForContract(
            runtimeValues,
            prepared);
        foreach (var (key, value) in current)
        {
            if (!key.Equals("schemaVersion", StringComparison.Ordinal))
            {
                prepared[key] = value?.DeepClone();
            }
        }
        StructuredRuntimeCollectionProjection.Apply(
            prepared,
            effectiveConfig,
            componentVariantConfig,
            componentRuntimeValues);
        PrepareNestedRuntimeContracts(
            prepared,
            effectiveConfig,
            componentVariantConfig,
            componentRuntimeValues);
        RuntimeInputOptionSourceContract.ValidateValues(prepared, effectiveConfig);
        return prepared;
    }

    private static void PrepareNestedRuntimeContracts(
        JsonObject runtimeContract,
        JsonObject effectiveConfig,
        Func<string, JsonObject>? componentVariantConfig,
        Func<string, JsonObject>? componentRuntimeValues)
    {
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(
                     runtimeContract, effectiveConfig, includeHidden: true))
        {
            if (input.ValueKind != ValueKind.StructuredCollection || input.StructuredCollection is null) continue;
            PrepareCollectionItems(
                JsonPath.ObjectItems(
                    JsonPath.RequiredArray(runtimeContract, input.JsonKey, "Runtime structured input"),
                    "Runtime structured input").ToArray(),
                input.StructuredCollection,
                effectiveConfig,
                componentVariantConfig,
                componentRuntimeValues);
        }
        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(
                     runtimeContract,
                     effectiveConfig,
                     includeHidden: true))
        {
            PrepareCollectionItems(
                DesignPreviewTestValues.CurrentCollectionItems(
                    runtimeContract,
                    collection),
                collection,
                effectiveConfig,
                componentVariantConfig,
                componentRuntimeValues);
        }
    }

    private static void PrepareCollectionItems(
        IReadOnlyList<JsonObject> items,
        RuntimeInputCollectionDefinition collection,
        JsonObject ownerConfig,
        Func<string, JsonObject>? componentVariantConfig,
        Func<string, JsonObject>? componentRuntimeValues)
    {
        foreach (var item in items)
        {
            var runtimeKey = !string.IsNullOrWhiteSpace(
                collection.ItemRuntimeContractJsonKey)
                ? collection.ItemRuntimeContractJsonKey
                : collection.ComponentItems?.InputsJsonKey ?? "";
            if (!string.IsNullOrWhiteSpace(runtimeKey)
                && item[runtimeKey] is JsonObject childRuntime)
            {
                if (!string.IsNullOrWhiteSpace(collection.ItemRuntimeVariantSlotJsonKey)
                    && item[collection.ItemRuntimeVariantSlotJsonKey] is null)
                {
                    continue;
                }
                var variantConfig = componentVariantConfig
                    ?? throw new InvalidOperationException(
                        $"Runtime collection '{collection.Id}' item contract "
                        + "requires a Component Variant config resolver.");
                var childConfig = RuntimeCollectionItemContractOwner
                    .ResolveItemVariantConfig(
                        item,
                        collection,
                        ownerConfig,
                        variantConfig);
                if (childConfig.Count > 0)
                {
                    item[runtimeKey] = PrepareFixture(
                        childRuntime,
                        childConfig,
                        variantConfig,
                        componentRuntimeValues);
                }
            }

            foreach (var field in collection.Fields)
            {
                if (field.ValueKind != ValueKind.StructuredCollection
                    || field.StructuredCollection is null
                    || item[field.JsonKey] is not JsonArray nestedItems)
                {
                    continue;
                }
                RuntimeCollectionDocumentContract.Validate(
                    nestedItems,
                    $"Runtime collection '{collection.Id}' item field '{field.JsonKey}'");
                PrepareCollectionItems(
                    nestedItems.OfType<JsonObject>().ToList(),
                    field.StructuredCollection,
                    ownerConfig,
                    componentVariantConfig,
                    componentRuntimeValues);
            }
        }
    }
}

/// <summary>
/// Converts declarative temporal phase sources into the exact Runtime document
/// consumed by every timeline. The authored contract keeps a stable config
/// path; Preview and Production never resolve that path independently.
/// </summary>
internal static class RuntimeTemporalPhaseContract
{
    public static void Hydrate(JsonObject runtimeContract, JsonObject effectiveConfig)
    {
        HydrateTimeline(
            JsonPath.OptionalObject(runtimeContract, "animationTimeline", "Runtime Preview document"),
            effectiveConfig,
            "Runtime Preview document animation timeline");
        foreach (var collection in JsonPath.OptionalObjectArray(
                     runtimeContract,
                     "collections",
                     "Runtime Preview document"))
        {
            HydrateTimeline(
                JsonPath.OptionalObject(collection, "animationTimeline", "Runtime Preview collection"),
                effectiveConfig,
                "Runtime Preview collection animation timeline");
        }
    }

    private static void HydrateTimeline(JsonObject? timeline, JsonObject effectiveConfig, string owner)
    {
        if (timeline is null || !timeline.TryGetPropertyValue("ownerPhase", out var phaseNode)) return;
        var phase = phaseNode as JsonObject
            ?? throw new InvalidOperationException($"{owner} ownerPhase must be an object.");
        var kind = JsonPath.RequiredString(phase, "kind", $"{owner} ownerPhase");
        if (kind.Equals("resolvedMotion", StringComparison.Ordinal)
            || kind.Equals("itemMotion", StringComparison.Ordinal))
        {
            return;
        }
        if (!kind.Equals("configMotion", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{owner} has unknown ownerPhase kind '{kind}'.");
        }
        var path = JsonPath.OptionalStringArray(phase, "configPath", $"{owner} configMotion");
        if (path.Count == 0)
        {
            throw new InvalidOperationException($"{owner} configMotion requires a non-empty configPath.");
        }
        var motion = JsonPath.Get(effectiveConfig, path.ToArray()) as JsonObject
            ?? throw new InvalidOperationException(
                $"{owner} configMotion path '{string.Join('.', path)}' must resolve to a Motion object.");
        phase.Clear();
        phase["kind"] = "resolvedMotion";
        phase["motion"] = motion.DeepClone();
    }
}
