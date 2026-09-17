using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    internal void ValidateDeclaredComponentVariantReferences(
        SqliteConnection connection,
        JsonObject config)
    {
        var componentRows = _componentClassRepository
            .QueryAll(connection);
        foreach (var slot in EmbeddedComponentSlotCatalog.All())
        {
            if (JsonPath.Get(config, slot.SlotPath)
                is not JsonObject slotNode)
            {
                continue;
            }

            var reference = JsonPath.String(
                slotNode,
                "variantReference",
                "");
            if (!VariantReferenceId.TryParse(
                    reference,
                    out var componentClassId,
                    out var variantId))
            {
                throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' must use a full component variant reference.");
            }

            var componentClass = componentRows.FirstOrDefault(
                (row) => row.Id.Equals(
                        componentClassId,
                        StringComparison.Ordinal)
                    && row.ComponentType.Equals(
                        slot.EmbeddedComponentType,
                        StringComparison.Ordinal));
            if (componentClass is null)
            {
                throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' references missing {slot.EmbeddedComponentType} class '{componentClassId}'.");
            }

            if (!ComponentClassVariants(
                    componentClass.MetadataJson,
                    $"Component class '{componentClass.Id}'")
                .Any(
                    (variant) => variant.Id.Equals(
                        variantId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' references missing variant '{variantId}' on '{componentClassId}'.");
            }
        }

        foreach (var descriptor in ComponentClassFieldCatalog.All().Where((candidate) =>
                     candidate.StructuredCollection is not null))
        {
            var collection = descriptor.StructuredCollection!;
            if (collection.FixedComponentBoundary is { } boundary
                && !componentRows.Any((row) => row.Id.Equals(
                        boundary.ComponentClassId,
                        StringComparison.Ordinal)
                    && row.ComponentType.Equals(
                        boundary.ComponentType,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Structured collection field '{descriptor.Id}' fixed Component class '{boundary.ComponentClassId}' is not a global {boundary.ComponentType} class.");
            }
            var node = JsonPath.Get(config, descriptor.JsonPath);
            if (node is null) continue;
            var items = node as JsonArray
                ?? throw new InvalidOperationException(
                    $"Structured collection field '{descriptor.Id}' must be an array.");
            if (collection.FixedComponentBoundary is not null)
            {
                StructuredCollectionDocumentContract.Validate(
                    items,
                    collection,
                    $"Structured collection field '{descriptor.Id}'");
            }
            ValidateStructuredComponentReferences(
                connection,
                items,
                collection,
                $"Structured collection field '{descriptor.Id}'");
            if (collection.FixedComponentBoundary is not { } fixedBoundary) continue;
            foreach (var item in items.OfType<JsonObject>())
            {
                var reference = JsonPath.RequiredString(
                    item,
                    fixedBoundary.VariantReferenceJsonKey,
                    $"Structured collection field '{descriptor.Id}'");
                _ = ValidateComponentVariantReference(
                    connection,
                    fixedBoundary.ComponentType,
                    reference);
            }
        }
    }

    private void ValidateStructuredComponentReferences(
        SqliteConnection connection,
        JsonArray items,
        RuntimeInputCollectionDefinition collection,
        string owner)
    {
        foreach (var item in items.OfType<JsonObject>())
        {
            var itemId = JsonPath.RequiredString(item, "id", owner);
            foreach (var field in collection.Fields)
            {
                var fieldOwner = $"{owner} item '{itemId}' field '{field.JsonKey}'";
                if (field.ValueKind == ValueKind.StructuredCollection
                    && field.StructuredCollection is { } nested
                    && item[field.JsonKey] is JsonArray nestedItems)
                {
                    ValidateStructuredComponentReferences(
                        connection,
                        nestedItems,
                        nested,
                        fieldOwner);
                    continue;
                }
                if (field.ValueKind is not (ValueKind.ComponentVariant or ValueKind.ComponentVariantSlot))
                {
                    continue;
                }
                var value = item[field.JsonKey];
                if (value is null)
                {
                    if (field.AllowEmpty) continue;
                    throw new InvalidOperationException($"{fieldOwner} requires a Component Variant.");
                }
                var reference = field.ValueKind == ValueKind.ComponentVariantSlot
                    ? ComponentVariantSlotDocumentContract.VariantReference(
                        value as JsonObject
                            ?? throw new InvalidOperationException($"{fieldOwner} must be a Component Variant Slot."),
                        fieldOwner)
                    : value.GetValue<string>();
                if (string.IsNullOrWhiteSpace(reference) && field.AllowEmpty) continue;
                ValidateComponentVariantSelectorReference(
                    connection,
                    field.ComponentType,
                    reference,
                    fieldOwner);
            }
        }
    }

    private void ValidateComponentVariantSelectorReference(
        SqliteConnection connection,
        string selector,
        string reference,
        string owner)
    {
        if (!VariantReferenceId.TryParse(reference, out var classId, out var variantId))
        {
            throw new InvalidOperationException(
                $"{owner} Component Variant reference '{reference}' must use the full componentClassId::variant::variantId form.");
        }
        var componentClass = _componentClassRepository.QueryAll(connection)
            .FirstOrDefault((candidate) => candidate.Id.Equals(classId, StringComparison.Ordinal));
        if (componentClass is null || !MatchesComponentSelector(componentClass.ComponentType, selector))
        {
            throw new InvalidOperationException(
                $"{owner} Component Variant reference '{reference}' is not allowed by selector '{selector}'.");
        }
        if (!ComponentClassVariants(
                componentClass.MetadataJson,
                $"Component class '{componentClass.Id}'")
            .Any((variant) => variant.Id.Equals(variantId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{owner} Component Variant reference '{reference}' names a missing Variant.");
        }
    }

    private static bool MatchesComponentSelector(string componentType, string selector)
    {
        var terms = selector.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var excluded = terms.Where((term) => term.StartsWith("-", StringComparison.Ordinal))
            .Select((term) => term[1..])
            .ToHashSet(StringComparer.Ordinal);
        if (excluded.Contains(componentType)) return false;
        return terms.Contains("*", StringComparer.Ordinal)
            || terms.Contains(componentType, StringComparer.Ordinal);
    }

    internal string ValidateComponentVariantReference(
        SqliteConnection connection,
        string componentType,
        string reference,
        bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            if (allowEmpty)
            {
                return "";
            }

            throw new InvalidOperationException(
                $"A {componentType} component variant reference is required.");
        }

        if (!VariantReferenceId.TryParse(
                reference,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Component variant reference '{reference}' must use the full componentClassId::variant::variantId form.");
        }

        var componentClass = _componentClassRepository
            .QueryAll(connection)
            .Where(
                (candidate) => candidate.ComponentType.Equals(
                    componentType,
                    StringComparison.Ordinal))
            .FirstOrDefault(
                (candidate) => candidate.Id.Equals(
                    componentClassId,
                    StringComparison.Ordinal));
        if (componentClass is null)
        {
            throw new InvalidOperationException(
                $"Component variant reference '{reference}' does not name a global {componentType} class.");
        }

        var metadata = ParseJsonObject(componentClass.MetadataJson);
        var variants = VariantEnvelopeContract.Read(
            metadata,
            "variants",
            $"Component class '{componentClass.Id}'");
        if (!variants.Any(
                (candidate) => candidate.Id.Equals(
                    variantId,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Component variant reference '{reference}' names a missing variant on '{componentClassId}'.");
        }

        return reference;
    }
}
