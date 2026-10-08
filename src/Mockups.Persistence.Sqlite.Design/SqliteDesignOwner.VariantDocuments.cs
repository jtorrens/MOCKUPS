using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    // A class fixture is shared by its complete named snapshots. This is an
    // ownership edge, not a reference inferred from names or JSON contents.
    internal IReadOnlyList<ReferenceTarget> VariantTargets(SqliteConnection connection, ReferenceTarget owner)
    {
        var metadataJson = owner.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => _componentClassRepository.Get(connection, owner.Id).MetadataJson,
            ProjectTreeNodeKind.Module => _appModuleRepository.GetModule(connection, owner.Id).MetadataJson,
            _ => null,
        };
        if (metadataJson is null) return [];
        var kind = owner.Kind == ProjectTreeNodeKind.ComponentClass
            ? ProjectTreeNodeKind.ComponentVariant : ProjectTreeNodeKind.ModuleVariant;
        return VariantEnvelopeContract.Read(ParseJsonObject(metadataJson), "variants", $"Variant owner '{owner.Id}'")
            .Select(variant => new ReferenceTarget(kind, VariantReferenceId.Format(owner.Id, variant.Id))).ToArray();
    }

    internal void StoreVariantDocument(
        SqliteConnection connection, SqliteTransaction transaction, VariantDocumentChange change)
    {
        switch (change.Kind)
        {
            case ProjectTreeNodeKind.ComponentVariant:
                var metadata = ParseJsonObject(change.MetadataJson);
                var defaultConfig = VariantEnvelopeContract.Read(metadata, "variants", $"Component '{change.OwnerId}'")
                    .Single(variant => variant.Id == VariantEnvelopeContract.DefaultId).Config;
                _componentClassRepository.UpdateConfigAndMetadata(connection, change.OwnerId,
                    defaultConfig.ToJsonString(), change.MetadataJson, transaction);
                break;
            case ProjectTreeNodeKind.ModuleVariant:
                _appModuleRepository.UpdateModuleMetadata(connection, change.OwnerId, change.MetadataJson, transaction);
                break;
            default:
                throw new InvalidOperationException($"Invalid Variant document kind '{change.Kind}'.");
        }
    }

    internal void PrepareVariantDependencies(
        SqliteConnection connection, SqliteTransaction transaction, ProjectTreeNodeKind kind, string reference)
    {
        if (!VariantReferenceId.TryParse(reference, out var ownerId, out var variantId))
            throw new InvalidOperationException($"Invalid Variant reference '{reference}'.");
        var metadataJson = kind switch
        {
            ProjectTreeNodeKind.ComponentVariant => _componentClassRepository.Get(connection, ownerId).MetadataJson,
            ProjectTreeNodeKind.ModuleVariant => _appModuleRepository.GetModule(connection, ownerId).MetadataJson,
            _ => throw new InvalidOperationException($"Invalid Variant kind '{kind}'."),
        };
        var metadata = ParseJsonObject(metadataJson);
        var variant = VariantEnvelopeContract.FindSource(
            VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Variant '{reference}'"), variantId)
            ?? throw new InvalidOperationException($"Missing Variant '{reference}'.");
        var config = JsonPath.RequiredObject(variant, "config", $"Variant '{reference}'");
        ApplyComponentInputBindingsProjections(connection, config,
            kind == ProjectTreeNodeKind.ComponentVariant
                ? ComponentInputBindingsProjectionCatalog.ComponentOwners()
                : ComponentInputBindingsProjectionCatalog.RecordOwners());
        ValidateDeclaredComponentVariantReferences(connection, config);
        var fixture = kind == ProjectTreeNodeKind.ComponentVariant
            ? _componentClassRepository.Get(connection, ownerId).DesignPreviewJson
            : _appModuleRepository.GetModule(connection, ownerId).DesignPreviewJson;
        _ = RuntimePreviewDocumentContract.PrepareFixture(ParseJsonObject(fixture), config,
            id => GetComponentVariantConfig(connection, id),
            id => GetComponentVariantRuntimeContract(connection, id));
        if (!JsonNode.DeepEquals(ParseJsonObject(metadataJson), metadata))
            StoreVariantDocument(connection, transaction, new(kind, ownerId, metadata.ToJsonString()));
    }
}
