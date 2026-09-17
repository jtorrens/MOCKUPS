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
    public static void ApplyProductionMediaFallback(
        JsonObject runtimeContract,
        JsonObject effectiveConfig,
        JsonObject animation,
        Func<ValueKind, string, bool> isAvailable)
    {
        ApplyMediaFallbackToContract(
            runtimeContract,
            effectiveConfig,
            isAvailable);
        foreach (var (_, child) in runtimeContract)
        {
            ApplyMediaFallbackToNestedContracts(
                child,
                isAvailable);
        }

        var targets = RuntimeInputAnimationValueContract.ReadTargets(
                runtimeContract,
                effectiveConfig,
                runtimeContract)
            .ToDictionary(
                (target) => (target.FieldId, target.TargetId));
        foreach (var track in JsonPath.RequiredArray(
                     animation,
                     "tracks",
                     "Production Runtime animation").OfType<JsonObject>())
        {
            var key = (
                JsonPath.RequiredString(
                    track,
                    "fieldId",
                    "Production Runtime animation track"),
                track["targetId"]?.GetValue<string>() ?? "");
            if (!targets.TryGetValue(key, out var target)
                || !IsMedia(target.Input.ValueKind))
            {
                continue;
            }
            foreach (var keyframe in JsonPath.RequiredArray(
                         track,
                         "keyframes",
                         $"Production Runtime animation track '{key.Item1}'")
                     .OfType<JsonObject>())
            {
                if (!Available(
                        target.Input.ValueKind,
                        keyframe["value"],
                        isAvailable))
                {
                    keyframe["value"] = target.Input.DefaultValue;
                }
            }
        }
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
        return prepared;
    }

    private static void ApplyMediaFallbackToContract(
        JsonObject runtime,
        JsonObject config,
        Func<ValueKind, string, bool> isAvailable)
    {
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(
                     runtime,
                     config,
                     includeHidden: true))
        {
            if (input.Source != ComponentInputSource.Runtime
                || !CollectionFieldAvailability.IsEnabled(runtime, input))
            {
                continue;
            }
            ApplyMediaFallback(runtime, input, isAvailable);
            ApplyStructuredMediaFallback(
                runtime[input.JsonKey],
                input,
                isAvailable);
        }

        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(
                     runtime,
                     config,
                     includeHidden: true))
        {
            foreach (var item in DesignPreviewTestValues.CurrentCollectionItems(
                         runtime,
                         collection))
            {
                ApplyMediaFallbackToCollectionItem(
                    item,
                    collection,
                    isAvailable);
            }
        }
    }

    private static void ApplyMediaFallbackToCollectionItem(
        JsonObject item,
        RuntimeInputCollectionDefinition collection,
        Func<ValueKind, string, bool> isAvailable)
    {
        foreach (var field in collection.Fields.Where((field) =>
                     field.Source == ComponentInputSource.Runtime
                     && CollectionFieldAvailability.IsEnabled(item, field)))
        {
            ApplyMediaFallback(item, field, isAvailable);
            ApplyStructuredMediaFallback(
                item[field.JsonKey],
                field,
                isAvailable);
        }
    }

    private static void ApplyStructuredMediaFallback(
        JsonNode? value,
        ComponentInputDefinition input,
        Func<ValueKind, string, bool> isAvailable)
    {
        if (input.ValueKind != ValueKind.StructuredCollection
            || input.StructuredCollection is null
            || value is not JsonArray items)
        {
            return;
        }
        foreach (var item in items.OfType<JsonObject>())
        {
            ApplyMediaFallbackToCollectionItem(
                item,
                input.StructuredCollection,
                isAvailable);
        }
    }

    private static void ApplyMediaFallback(
        JsonObject owner,
        ComponentInputDefinition input,
        Func<ValueKind, string, bool> isAvailable)
    {
        if (!IsMedia(input.ValueKind)
            || Available(
                input.ValueKind,
                owner[input.JsonKey],
                isAvailable))
        {
            return;
        }
        owner[input.JsonKey] = input.DefaultValue;
    }

    private static bool Available(
        ValueKind valueKind,
        JsonNode? value,
        Func<ValueKind, string, bool> isAvailable) =>
        value is JsonValue scalar
        && scalar.TryGetValue<string>(out var text)
        && !string.IsNullOrWhiteSpace(text)
        && isAvailable(valueKind, text);

    private static bool IsMedia(ValueKind valueKind) =>
        valueKind is ValueKind.ImageFilePath
            or ValueKind.MediaFilePath
            or ValueKind.MediaDirectoryPath;

    private static void ApplyMediaFallbackToNestedContracts(
        JsonNode? node,
        Func<ValueKind, string, bool> isAvailable)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                ApplyMediaFallbackToNestedContracts(child, isAvailable);
            }
            return;
        }
        if (node is not JsonObject value) return;

        if (value["inputs"] is JsonArray || value["collections"] is JsonArray)
        {
            ApplyMediaFallbackToContract(
                value,
                new JsonObject(),
                isAvailable);
        }
        foreach (var (_, child) in value)
        {
            ApplyMediaFallbackToNestedContracts(child, isAvailable);
        }
    }

    private static void PrepareNestedRuntimeContracts(
        JsonObject runtimeContract,
        JsonObject effectiveConfig,
        Func<string, JsonObject>? componentVariantConfig,
        Func<string, JsonObject>? componentRuntimeValues)
    {
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
