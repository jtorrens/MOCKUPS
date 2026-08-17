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
        string projectId,
        JsonObject config)
    {
        var componentRows = _componentClassRepository
            .QueryByProject(connection, projectId);
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
                     candidate.StructuredCollection?.FixedComponentBoundary is not null))
        {
            var collection = descriptor.StructuredCollection!;
            var boundary = collection.FixedComponentBoundary!;
            if (!componentRows.Any((row) => row.Id.Equals(
                    boundary.ComponentClassId,
                    StringComparison.Ordinal)
                && row.ComponentType.Equals(
                    boundary.ComponentType,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Structured collection field '{descriptor.Id}' fixed Component class '{boundary.ComponentClassId}' is not a {boundary.ComponentType} class in project '{projectId}'.");
            }
            var node = JsonPath.Get(config, descriptor.JsonPath);
            if (node is null) continue;
            var items = node as JsonArray
                ?? throw new InvalidOperationException(
                    $"Structured collection field '{descriptor.Id}' must be an array.");
            StructuredCollectionDocumentContract.Validate(
                items,
                collection,
                $"Structured collection field '{descriptor.Id}'");
            foreach (var item in items.OfType<JsonObject>())
            {
                var reference = JsonPath.RequiredString(
                    item,
                    boundary.VariantReferenceJsonKey,
                    $"Structured collection field '{descriptor.Id}'");
                _ = ValidateComponentVariantReference(
                    connection,
                    projectId,
                    boundary.ComponentType,
                    reference);
            }
        }
    }

    internal string ValidateComponentVariantReference(
        SqliteConnection connection,
        string projectId,
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
            .QueryByProject(connection, projectId)
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
                $"Component variant reference '{reference}' does not name a {componentType} class in project '{projectId}'.");
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
