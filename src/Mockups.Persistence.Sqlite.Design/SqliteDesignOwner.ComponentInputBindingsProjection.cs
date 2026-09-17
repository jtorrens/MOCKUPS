using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    private void ApplyComponentInputBindingsProjections(
        SqliteConnection connection,
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
            var effective = EffectiveComponentRuntimeContract(
                connection,
                reference,
                JsonPath.RequiredObject(
                    slot,
                    "overrides",
                    $"{definition.Id} Component Variant slot"));
            var projected = RuntimeInputDocumentContract.ProjectInputValuesForContract(
                inputs,
                effective.Contract,
                definition.CalculatedInputIds);
            JsonPath.Set(
                ownerConfig,
                definition.InputsPath,
                RuntimePreviewDocumentContract.PrepareInputValues(
                    projected,
                    effective.Contract,
                    effective.Config,
                    GetComponentVariantConfig));
        }
    }

    private EffectiveComponentRuntimeProjection EffectiveComponentRuntimeContract(
        SqliteConnection connection,
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
        var variant = RequiredComponentClassVariants(row)
            .Single((candidate) => candidate.Id.Equals(
                variantId,
                StringComparison.Ordinal));
        var config = ParseJsonObject(variant.ConfigJson);
        ComponentConfigOverrideMerger.MergeInto(config, overrides);
        var contract = RuntimePreviewDocumentContract.PrepareFixture(
            ParseJsonObject(row.DesignPreviewJson),
            config,
            GetComponentVariantConfig);
        return new EffectiveComponentRuntimeProjection(
            config,
            contract);
    }

    private sealed record EffectiveComponentRuntimeProjection(
        JsonObject Config,
        JsonObject Contract);
}
