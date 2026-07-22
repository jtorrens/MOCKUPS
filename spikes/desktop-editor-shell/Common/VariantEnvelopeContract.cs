using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record CurrentVariantEnvelope(
    string Id,
    string Name,
    bool IsProtected,
    bool IsLocked,
    JsonObject Config,
    JsonObject Source);

internal static class VariantEnvelopeContract
{
    public static IReadOnlyList<CurrentVariantEnvelope> Read(
        JsonObject metadata,
        string storageKey,
        string owner)
    {
        var array = RequiredArray(metadata, storageKey, owner);
        return array.Select((node, index) => ReadEntry(node, index, owner)).ToList();
    }

    public static JsonArray RequiredArray(
        JsonObject metadata,
        string storageKey,
        string owner)
    {
        if (metadata[storageKey] is not JsonArray array || array.Count == 0)
        {
            throw new InvalidOperationException($"{owner} must contain a non-empty '{storageKey}' Variant array.");
        }

        var variants = array.Select((node, index) => ReadEntry(node, index, owner)).ToList();
        var duplicate = variants
            .GroupBy((variant) => variant.Id, StringComparer.Ordinal)
            .FirstOrDefault((group) => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"{owner} contains duplicate Variant id '{duplicate.Key}'.");
        }

        var defaultVariant = variants.SingleOrDefault((variant) => variant.Id.Equals("default", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"{owner} must contain the stable Default Variant id 'default'.");
        if (!defaultVariant.IsProtected)
        {
            throw new InvalidOperationException($"{owner} Default Variant must be protected.");
        }

        return array;
    }

    public static JsonObject? FindSource(JsonArray variants, string variantId) =>
        variants
            .OfType<JsonObject>()
            .FirstOrDefault((variant) =>
                JsonPath.String(variant, "id", "").Equals(variantId, StringComparison.Ordinal));

    public static string UniqueId(JsonArray variants, string name)
    {
        var baseId = new string(name
                .Trim()
                .ToLowerInvariant()
                .Select((character) => char.IsLetterOrDigit(character) ? character : '_')
                .ToArray())
            .Trim('_');
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "variant";
        }

        var existing = variants
            .OfType<JsonObject>()
            .Select((variant) => JsonPath.String(variant, "id", ""))
            .ToHashSet(StringComparer.Ordinal);
        var candidate = baseId;
        for (var suffix = 2; existing.Contains(candidate); suffix++)
        {
            candidate = $"{baseId}_{suffix}";
        }

        return candidate;
    }

    private static CurrentVariantEnvelope ReadEntry(JsonNode? node, int index, string owner)
    {
        if (node is not JsonObject variant)
        {
            throw new InvalidOperationException($"{owner} Variant at index {index} must be an object.");
        }

        var id = RequiredString(variant, "id", owner, index);
        if (!id.All(IsStableIdCharacter))
        {
            throw new InvalidOperationException($"{owner} Variant at index {index} has invalid stable id '{id}'.");
        }

        var name = RequiredString(variant, "name", owner, index);
        var isProtected = RequiredBoolean(variant, "protected", owner, index);
        var isLocked = RequiredBoolean(variant, "locked", owner, index);
        var config = variant["config"] as JsonObject
            ?? throw new InvalidOperationException($"{owner} Variant '{id}' must contain an object config snapshot.");
        return new CurrentVariantEnvelope(id, name, isProtected, isLocked, config, variant);
    }

    private static string RequiredString(JsonObject variant, string key, string owner, int index)
    {
        if (variant[key] is not JsonValue value
            || !value.TryGetValue<string>(out var text)
            || string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"{owner} Variant at index {index} must contain a non-empty string '{key}'.");
        }

        return text;
    }

    private static bool RequiredBoolean(JsonObject variant, string key, string owner, int index)
    {
        if (variant[key] is not JsonValue value || !value.TryGetValue<bool>(out var boolean))
        {
            throw new InvalidOperationException($"{owner} Variant at index {index} must contain an explicit boolean '{key}'.");
        }

        return boolean;
    }

    private static bool IsStableIdCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '-' or '.';
}
