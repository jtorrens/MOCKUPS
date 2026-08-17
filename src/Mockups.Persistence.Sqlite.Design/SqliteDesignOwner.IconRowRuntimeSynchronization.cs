using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    // A Variant-owned structural collection and its parent Runtime collection
    // are connected only by a declared projection in the slot catalog. Keep
    // every direct parent document aligned when that structure changes.
    private void SynchronizeVariantOwnedCollectionConsumers(
        SqliteConnection connection,
        string projectId,
        string variantReference,
        ComponentClassFieldDescriptor descriptor,
        JsonArray items)
    {
        RuntimeInputValueKindContract.ValidateValue(
            descriptor.ValueKind,
            items,
            $"Variant collection '{descriptor.Id}'");

        foreach (var component in _componentClassRepository
                     .QueryByProject(connection, projectId))
        {
            var config = ParseJsonObject(component.ConfigJson);
            var metadata = ParseJsonObject(component.MetadataJson);
            var configChanged = SynchronizeDirectCollectionConsumer(
                config,
                variantReference,
                descriptor,
                items);
            if (configChanged)
            {
                SetDefaultComponentVariantConfig(metadata, config);
            }

            var metadataChanged = false;
            var variants = VariantEnvelopeContract.RequiredArray(
                metadata,
                "variants",
                $"Component class '{component.Id}'");
            foreach (var variantNode in variants.OfType<JsonObject>())
            {
                var variantId = JsonPath.RequiredString(
                    variantNode,
                    "id",
                    $"Component class '{component.Id}' Variant");
                if (variantId.Equals(
                        VariantEnvelopeContract.DefaultId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var variantConfig = JsonPath.RequiredObject(
                    variantNode,
                    "config",
                    $"Component class '{component.Id}' Variant '{variantId}'");
                metadataChanged |= SynchronizeDirectCollectionConsumer(
                    variantConfig,
                    variantReference,
                    descriptor,
                    items);
            }

            if (configChanged || metadataChanged)
            {
                _componentClassRepository.UpdateConfigAndMetadata(
                    connection,
                    component.Id,
                    config.ToJsonString(),
                    metadata.ToJsonString());
            }
        }

        foreach (var module in _appModuleRepository
                     .QueryModules(connection)
                     .Where((candidate) => candidate.ProjectId.Equals(
                         projectId,
                         StringComparison.Ordinal)))
        {
            var config = ParseJsonObject(module.ConfigJson);
            var configChanged = SynchronizeDirectCollectionConsumer(
                config,
                variantReference,
                descriptor,
                items);
            var metadata = ParseJsonObject(module.MetadataJson);
            var metadataChanged = false;
            foreach (var variantNode in VariantEnvelopeContract.RequiredArray(
                         metadata,
                         "variants",
                         $"Module '{module.Id}'").OfType<JsonObject>())
            {
                var variantId = JsonPath.RequiredString(
                    variantNode,
                    "id",
                    $"Module '{module.Id}' Variant");
                var variantConfig = JsonPath.RequiredObject(
                    variantNode,
                    "config",
                    $"Module '{module.Id}' Variant '{variantId}'");
                metadataChanged |= SynchronizeDirectCollectionConsumer(
                    variantConfig,
                    variantReference,
                    descriptor,
                    items);
            }

            if (configChanged)
            {
                _appModuleRepository.UpdateModuleConfig(
                    connection,
                    module.Id,
                    config.ToJsonString());
            }
            if (metadataChanged)
            {
                _appModuleRepository.UpdateModuleMetadata(
                    connection,
                    module.Id,
                    metadata.ToJsonString());
            }
        }
    }

    private static bool SynchronizeDirectCollectionConsumer(
        JsonObject config,
        string variantReference,
        ComponentClassFieldDescriptor descriptor,
        JsonArray changedItems)
    {
        var changed = false;
        foreach (var slot in EmbeddedComponentSlotCatalog.All())
        {
            if (!EmbeddedComponentSlotCatalog.TryRuntimeCollectionProjection(
                    slot.FieldId,
                    out var projection)
                || !descriptor.Id.Equals(
                    projection.StructuralFieldId,
                    StringComparison.Ordinal)
                || JsonPath.Get(config, slot.SlotPath) is not JsonObject slotValue
                || !JsonPath.RequiredString(
                        slotValue,
                        "variantReference",
                        $"Embedded Icon Row '{slot.FieldId}'")
                    .Equals(variantReference, StringComparison.Ordinal))
            {
                continue;
            }

            var items = slotValue["overrides"] is JsonObject overrides
                && JsonPath.Get(overrides, projection.StructuralConfigPath) is JsonArray overrideItems
                    ? overrideItems
                    : changedItems;
            RuntimeInputValueKindContract.ValidateValue(
                descriptor.ValueKind,
                items,
                $"Embedded collection '{slot.FieldId}' items");
            var inputs = JsonPath.Get(config, projection.RuntimeInputPath) as JsonObject
                ?? throw new InvalidOperationException(
                    $"Embedded collection '{slot.FieldId}' has no declared Runtime inputs.");
            if (JsonNode.DeepEquals(inputs[projection.RuntimeValueKey], items))
            {
                continue;
            }

            inputs[projection.RuntimeValueKey] = items.DeepClone();
            changed = true;
        }

        return changed;
    }
}
