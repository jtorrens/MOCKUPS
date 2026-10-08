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
    public ComponentPreviewTransientState SavedValues(
        IReadOnlyList<ComponentInputDefinition> inputs,
        IReadOnlyList<RuntimeInputCollectionDefinition> collections)
    {
        var keys = inputs.Select(input => $"{ScopeKey}:{input.JsonKey}").ToHashSet(StringComparer.Ordinal);
        var roots = collections.Select(collection => collection.StorageJsonKey).ToHashSet(StringComparer.Ordinal);
        var capturedRoots = JsonPath.ParseRequiredObject(CollectionTestValuesJson, "Captured Test Values collections");
        var savedRoots = new JsonObject(capturedRoots.Where(pair => roots.Contains(pair.Key))
            .Select(pair => KeyValuePair.Create(pair.Key, pair.Value?.DeepClone())));
        return this with
        {
            Values = Values.Where(pair => keys.Contains(pair.Key)).ToFrozenDictionary(StringComparer.Ordinal),
            HasCollectionTestValues = savedRoots.Count > 0,
            CollectionTestValuesJson = savedRoots.ToJsonString(),
        };
    }

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
    public static string ActionStateKey(string scope, string id) => $"{scope}:action:{id}:state";
    public static string ActionTimeKey(string scope, string id) => $"{scope}:action:{id}:time";
    public static string ActionTargetFromKey(string scope, string id) => $"{scope}:action:{id}:target-from";
    public static string ActionTargetValueKey(string scope, string id) => $"{scope}:action:{id}:target-value";
    public static IEnumerable<string> ActionKeys(string scope, string id) =>
        [ActionStateKey(scope, id), ActionTimeKey(scope, id), ActionTargetFromKey(scope, id), ActionTargetValueKey(scope, id)];

    public static string ScopeKey(DesignPreviewPayload payload)
    {
        var instanceId = ParseJsonObject(payload.InstanceJson)["context"]?
            ["moduleInstanceId"]?.GetValue<string>() ?? "";
        var ownerIdentity = !string.IsNullOrWhiteSpace(payload.OwnerId)
            ? payload.OwnerId
            : throw new InvalidOperationException("Preview transient state requires an exact owner id.");
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
        Func<string, JsonObject> componentVariantConfig,
        Func<string, JsonObject> componentRuntimeValues)
    {
        var authoring = preview.DeepClone().AsObject();
        NestedRuntimeRecordReferenceResolver.RemoveDeclaredResolvedValues(
            authoring,
            config);
        if (state.HasCollectionTestValues)
        {
            // The transient owner stores complete storage-root snapshots, not
            // sparse per-item overlays. Apply them before structure preparation
            // so additions, deletion and order travel with scalar item edits.
            authoring.Remove("testValues");
            foreach (var (storageKey, value) in ParseJsonObject(state.CollectionTestValuesJson))
            {
                authoring[storageKey] = value?.DeepClone();
            }
        }
        var envelope = RuntimePreviewDocumentContract.PrepareFixture(
            authoring,
            config,
            componentVariantConfig,
            componentRuntimeValues);
        NestedRuntimeRecordReferenceResolver.RemoveDeclaredResolvedValues(
            envelope,
            config);

        var effective = ParseJsonObject(
            DesignPreviewTestValues.RuntimeJson(
                envelope.ToJsonString()));
        ReconcileRuntimeStructure(
            effective,
            config,
            componentVariantConfig,
            componentRuntimeValues);
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
        NestedRuntimeRecordReferenceResolver.RemoveDeclaredResolvedValues(
            effective,
            config);
        effective = ParseJsonObject(
            DesignPreviewTestValues.RuntimeJson(
                effective.ToJsonString()));
        ReconcileRuntimeStructure(
            effective,
            config,
            componentVariantConfig,
            componentRuntimeValues);
        return effective;
    }

    public static void ReconcileRuntimeStructure(
        JsonObject preview,
        JsonObject config,
        Func<string, JsonObject> componentVariantConfig,
        Func<string, JsonObject> componentRuntimeValues)
    {
        var prepared = RuntimePreviewDocumentContract.PrepareFixture(
            preview,
            config,
            componentVariantConfig,
            componentRuntimeValues);
        preview.Clear();
        foreach (var (key, value) in prepared)
        {
            preview[key] = value?.DeepClone();
        }
    }

    private static JsonObject ParseJsonObject(string json)
    {
        return JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException(
                "Preview JSON must be an object.");
    }
}
