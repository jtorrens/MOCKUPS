using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record RuntimeComponentCollectionDocumentKeys(
    string VariantReferenceJsonKey,
    string OverridesJsonKey,
    string InputsJsonKey);

internal static class RuntimeComponentCollectionItemDocumentContract
{
    public static RuntimeComponentCollectionDocumentKeys? ReadDefinition(
        JsonObject collection,
        string owner)
    {
        if (!collection.TryGetPropertyValue("componentItems", out var node)) return null;
        var definition = node as JsonObject
            ?? throw new InvalidOperationException(
                $"{owner} componentItems must be an object when present.");
        var keys = new RuntimeComponentCollectionDocumentKeys(
            JsonPath.RequiredString(definition, "variantReferenceJsonKey", $"{owner} componentItems"),
            JsonPath.RequiredString(definition, "overridesJsonKey", $"{owner} componentItems"),
            JsonPath.RequiredString(definition, "inputsJsonKey", $"{owner} componentItems"));
        ValidateKeys(keys, owner);
        return keys;
    }

    public static void ValidateKeys(
        RuntimeComponentCollectionDocumentKeys keys,
        string owner)
    {
        if (string.IsNullOrWhiteSpace(keys.VariantReferenceJsonKey)
            || string.IsNullOrWhiteSpace(keys.OverridesJsonKey)
            || string.IsNullOrWhiteSpace(keys.InputsJsonKey))
        {
            throw new InvalidOperationException(
                $"{owner} componentItems requires three non-empty document keys.");
        }
        var unique = new HashSet<string>(StringComparer.Ordinal)
        {
            keys.VariantReferenceJsonKey,
            keys.OverridesJsonKey,
            keys.InputsJsonKey,
        };
        if (unique.Count != 3)
        {
            throw new InvalidOperationException(
                $"{owner} componentItems requires three distinct document keys.");
        }
    }

    public static void ValidateItem(
        JsonObject item,
        RuntimeComponentCollectionDocumentKeys keys,
        string owner)
    {
        ValidateKeys(keys, owner);
        _ = RequireVariantReference(item, keys, owner);
        _ = RequireOverrides(item, keys, owner);
        _ = RequireInputs(item, keys, owner);
    }

    public static string RequireVariantReference(
        JsonObject item,
        RuntimeComponentCollectionDocumentKeys keys,
        string owner)
    {
        if (!item.TryGetPropertyValue(keys.VariantReferenceJsonKey, out var node)
            || node is not JsonValue value
            || !value.TryGetValue<string>(out var reference))
        {
            throw new InvalidOperationException(
                $"{owner} '{keys.VariantReferenceJsonKey}' must be a string.");
        }
        if (reference.Length == 0) return reference;
        if (!VariantReferenceId.TryParse(reference, out _, out _))
        {
            throw new InvalidOperationException(
                $"{owner} '{keys.VariantReferenceJsonKey}' must be a full Component Variant reference or the explicit empty sentinel.");
        }
        return reference;
    }

    public static JsonObject RequireOverrides(
        JsonObject item,
        RuntimeComponentCollectionDocumentKeys keys,
        string owner) =>
        RequireObject(item, keys.OverridesJsonKey, owner);

    public static JsonObject RequireInputs(
        JsonObject item,
        RuntimeComponentCollectionDocumentKeys keys,
        string owner) =>
        RequireObject(item, keys.InputsJsonKey, owner);

    private static JsonObject RequireObject(JsonObject item, string key, string owner)
    {
        if (!item.TryGetPropertyValue(key, out var node) || node is not JsonObject value)
        {
            throw new InvalidOperationException($"{owner} requires object '{key}'.");
        }
        return value;
    }
}
