using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ComponentPreviewTransientState(
    string ScopeKey,
    IReadOnlyDictionary<string, string> Values,
    bool HasCollectionTestValues,
    string CollectionTestValuesJson)
{
    public static ComponentPreviewTransientState Capture(
        string scopeKey,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, JsonObject> collectionTestValuesByScope)
    {
        var prefix = $"{scopeKey}:";
        var scopedValues = string.IsNullOrWhiteSpace(scopeKey)
            ? FrozenDictionary<string, string>.Empty
            : values
                .Where((entry) =>
                    entry.Key.StartsWith(prefix, StringComparison.Ordinal))
                .ToFrozenDictionary(
                    (entry) => entry.Key,
                    (entry) => entry.Value,
                    StringComparer.Ordinal);
        JsonObject? collectionTestValues = null;
        var hasCollectionTestValues =
            !string.IsNullOrWhiteSpace(scopeKey)
            && collectionTestValuesByScope.TryGetValue(
                scopeKey,
                out collectionTestValues);
        return new ComponentPreviewTransientState(
            scopeKey,
            scopedValues,
            hasCollectionTestValues,
            hasCollectionTestValues
                ? collectionTestValues!.ToJsonString()
                : "{}");
    }
}

internal static class ComponentPreviewTransientValues
{
    public static string ScopeKey(DesignPreviewPayload payload)
    {
        var instanceId = ParseJsonObject(payload.InstanceJson)["context"]?
            ["moduleInstanceId"]?.GetValue<string>() ?? "";
        var ownerIdentity = string.IsNullOrWhiteSpace(payload.OwnerId)
            ? $"{payload.ComponentType}:{payload.Name}"
            : payload.OwnerId;
        return $"{payload.Kind}:{ownerIdentity}:{instanceId}";
    }

    public static string ScopeKey(
        ProjectTreeNode node,
        bool isInstance)
    {
        var kind = node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass
                or ProjectTreeNodeKind.ComponentVariant =>
                "componentClass",
            ProjectTreeNodeKind.Module
                or ProjectTreeNodeKind.ModuleVariant =>
                "module",
            ProjectTreeNodeKind.ModuleInstance when isInstance =>
                "moduleInstance",
            _ => "",
        };
        if (kind.Length == 0)
        {
            return "";
        }

        return $"{kind}:{node.Id}:{(isInstance ? node.Id : "")}";
    }

    public static JsonObject Apply(
        JsonObject preview,
        JsonObject config,
        ComponentPreviewTransientState state,
        Func<string, JsonObject> componentVariantConfig)
    {
        var envelope = RuntimeInputForwardingContract.EffectivePreview(
            preview,
            config);
        if (state.HasCollectionTestValues)
        {
            envelope["testValues"] = ParseJsonObject(
                state.CollectionTestValuesJson).DeepClone();
        }

        var effective = ParseJsonObject(
            DesignPreviewTestValues.RuntimeJson(
                envelope.ToJsonString()));
        ReconcileRuntimeStructure(
            effective,
            config,
            componentVariantConfig);
        if (string.IsNullOrWhiteSpace(state.ScopeKey))
        {
            return effective;
        }

        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(
                     effective,
                     config))
        {
            var key = $"{state.ScopeKey}:{input.JsonKey}";
            if (state.Values.TryGetValue(key, out var value))
            {
                DesignPreviewTestValues.SetValue(
                    effective,
                    input,
                    value);
            }
        }
        effective = ParseJsonObject(
            DesignPreviewTestValues.RuntimeJson(
                effective.ToJsonString()));
        ReconcileRuntimeStructure(
            effective,
            config,
            componentVariantConfig);
        return effective;
    }

    public static void ReconcileRuntimeStructure(
        JsonObject preview,
        JsonObject config,
        Func<string, JsonObject> componentVariantConfig)
    {
        StructuredRuntimeCollectionProjection.Apply(preview, config);
        foreach (var collection in
                 RuntimeInputDefinitionReader.ReadCollections(
                     preview,
                     config,
                     includeHidden: true))
        {
            foreach (var item in
                     DesignPreviewTestValues.CurrentCollectionItems(
                         preview,
                         collection))
            {
                var runtimeKey = !string.IsNullOrWhiteSpace(
                    collection.ItemRuntimeContractJsonKey)
                    ? collection.ItemRuntimeContractJsonKey
                    : collection.ComponentItems?.InputsJsonKey ?? "";
                if (string.IsNullOrWhiteSpace(runtimeKey)
                    || item[runtimeKey] is not JsonObject childRuntime)
                {
                    continue;
                }

                var variantReference =
                    RuntimeCollectionItemContractOwner
                        .ResolveItemVariantReference(
                            item,
                            collection,
                            config,
                            componentVariantConfig);
                if (string.IsNullOrWhiteSpace(variantReference))
                {
                    continue;
                }
                var childConfig =
                    componentVariantConfig(variantReference);
                StructuredRuntimeCollectionProjection.Apply(
                    childRuntime,
                    childConfig);
            }
        }
    }

    private static JsonObject ParseJsonObject(string json)
    {
        return JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException(
                "Preview JSON must be an object.");
    }
}
