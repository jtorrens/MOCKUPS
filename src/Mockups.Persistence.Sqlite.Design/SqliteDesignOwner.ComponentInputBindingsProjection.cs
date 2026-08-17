using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    private void ApplyComponentInputBindingsProjections(
        SqliteConnection connection,
        string projectId,
        JsonObject ownerConfig,
        IReadOnlyList<ComponentInputBindingsProjectionDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            var inputsNode = JsonPath.Get(
                ownerConfig,
                definition.InputsPath);
            var slotNode = JsonPath.Get(
                ownerConfig,
                definition.SlotPath);
            if (inputsNode is null && slotNode is null)
            {
                continue;
            }
            var inputs = inputsNode as JsonObject
                ?? throw new InvalidOperationException(
                    $"{definition.Id} Component Input bindings must be an object.");
            var slot = slotNode as JsonObject
                ?? throw new InvalidOperationException(
                    $"{definition.Id} Component Variant slot must be an object.");
            ComponentVariantSlotDocumentContract.Validate(
                slot,
                $"{definition.Id} Component Variant slot");
            var reference =
                ComponentVariantSlotDocumentContract.VariantReference(
                    slot,
                    $"{definition.Id} Component Variant slot");
            var contract = EffectiveComponentRuntimeContract(
                connection,
                projectId,
                reference,
                JsonPath.RequiredObject(
                    slot,
                    "overrides",
                    $"{definition.Id} Component Variant slot"));
            JsonPath.Set(
                ownerConfig,
                definition.InputsPath,
                RuntimeInputDocumentContract.ProjectInputValuesForContract(
                    inputs,
                    contract,
                    definition.CalculatedInputIds));
        }
    }

    private JsonObject EffectiveComponentRuntimeContract(
        SqliteConnection connection,
        string projectId,
        string variantReference,
        JsonObject overrides)
    {
        if (!VariantReferenceId.TryParse(
                variantReference,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid Component Variant reference '{variantReference}'.");
        }
        var row = _componentClassRepository.Get(
            connection,
            componentClassId);
        if (!row.ProjectId.Equals(projectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Component Variant '{variantReference}' belongs to another Project.");
        }
        var variant = RequiredComponentClassVariants(row)
            .Single((candidate) => candidate.Id.Equals(
                variantId,
                StringComparison.Ordinal));
        var config = ParseJsonObject(variant.ConfigJson);
        ComponentConfigOverrideMerger.MergeInto(config, overrides);
        var contract = RuntimeInputForwardingContract.EffectivePreview(
            ParseJsonObject(row.DesignPreviewJson),
            config);
        StructuredRuntimeCollectionProjection.Apply(contract, config);
        return contract;
    }
}
