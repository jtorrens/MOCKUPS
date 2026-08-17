using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    public void UpdateEmbeddedComponentField(
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId,
        string value)
    {
        var slot = EmbeddedComponentSlotCatalog.Get(slotFieldId);
        if (!slot.EmbeddedComponentType.Equals(
                embeddedComponentType,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Embedded component '{embeddedComponentType}' is not supported for slot '{slotFieldId}'.");
        }

        UpdateEmbeddedComponentField(
            componentClassId,
            [slot],
            embeddedFieldId,
            value);
    }

    public void UpdateEmbeddedComponentField(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException(
                $"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(
            embeddedFieldId);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(
                connection,
                componentClassId);
            var config = ParseJsonObject(settings.ConfigJson);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var overrides = EmbeddedOverrides(
                config,
                slots,
                createIfMissing: true)
                ?? throw new InvalidOperationException(
                    $"Missing embedded override slot '{slots[^1].FieldId}'.");
            UpdateEmbeddedOverrideValue(
                overrides,
                descriptor,
                value);
            SynchronizeStructuralRuntimeInputs(
                config,
                slots,
                descriptor,
                value);
            PersistDefaultComponentConfig(
                connection,
                componentClassId,
                config,
                metadata);
        }
    }

    public void UpdateEmbeddedComponentField(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
    {
        if (ownerNode.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            UpdateEmbeddedComponentField(
                ownerNode.Id,
                slots,
                embeddedFieldId,
                value);
            return;
        }

        if (ownerNode.Kind is
            ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.ModuleVariant)
        {
            UpdateModuleEmbeddedComponentField(
                ownerNode,
                slots,
                embeddedFieldId,
                value);
            return;
        }

        if (ownerNode.Kind != ProjectTreeNodeKind.ComponentVariant)
        {
            throw new InvalidOperationException(
                $"Embedded component field '{embeddedFieldId}' is not supported for '{ownerNode.Kind}'.");
        }

        if (slots.Count == 0)
        {
            throw new InvalidOperationException(
                $"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(
            embeddedFieldId);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var config = ComponentVariantConfigForUpdate(
                connection,
                ownerNode,
                out var componentClassId,
                out var metadata);
            var overrides = EmbeddedOverrides(
                config,
                slots,
                createIfMissing: true)
                ?? throw new InvalidOperationException(
                    $"Missing embedded override slot '{slots[^1].FieldId}'.");
            UpdateEmbeddedOverrideValue(
                overrides,
                descriptor,
                value);
            SynchronizeStructuralRuntimeInputs(
                config,
                slots,
                descriptor,
                value);
            PersistComponentVariantUpdate(
                connection,
                ownerNode,
                componentClassId,
                config,
                metadata);
        }
    }

    private void UpdateModuleEmbeddedComponentField(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException(
                $"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(
            embeddedFieldId);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings =
                ownerNode.Kind == ProjectTreeNodeKind.Module
                    ? GetModuleSettings(ownerNode.Id)
                    : GetModuleVariantSettings(ownerNode);
            var config = ParseJsonObject(settings.ConfigJson);
            var overrides = EmbeddedOverrides(
                config,
                slots,
                createIfMissing: true)
                ?? throw new InvalidOperationException(
                    $"Missing embedded override slot '{slots[^1].FieldId}'.");
            UpdateEmbeddedOverrideValue(
                overrides,
                descriptor,
                value);
            SynchronizeStructuralRuntimeInputs(
                config,
                slots,
                descriptor,
                value);
            if (ownerNode.Kind == ProjectTreeNodeKind.Module)
            {
                _appModuleRepository.UpdateModuleConfig(
                    connection,
                    ownerNode.Id,
                    config.ToJsonString());
            }
            else
            {
                ReplaceModuleVariantConfig(
                    ownerNode,
                    config.ToJsonString());
            }
        }
    }

    private static void UpdateEmbeddedOverrideValue(
        JsonObject overrides,
        ComponentClassFieldDescriptor descriptor,
        string value)
    {
        if (value.Equals(
                "inherited",
                StringComparison.Ordinal)
            || descriptor.ValueKind == ValueKind.TypographyStyle
                && TypographyStyleValue.IsEmpty(value))
        {
            RemoveJsonValue(overrides, descriptor.JsonPath);
            return;
        }

        SetJsonValue(
            overrides,
            descriptor.JsonPath,
            ComponentConfigJsonValue(
                descriptor,
                value));
    }

    // The parent owns Runtime values across an embedded boundary. A declared
    // structural collection edit therefore replaces the matching parent
    // collection in the same write.
    private static void SynchronizeStructuralRuntimeInputs(
        JsonObject config,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        ComponentClassFieldDescriptor descriptor,
        string value)
    {
        if (value.Equals("inherited", StringComparison.Ordinal))
        {
            return;
        }

        if (!EmbeddedComponentSlotCatalog.TryRuntimeCollectionProjection(
                slots[^1].FieldId,
                out var projection)
            || !descriptor.Id.Equals(
                projection.StructuralFieldId,
                StringComparison.Ordinal))
        {
            return;
        }

        var inputs = JsonPath.Get(config, projection.RuntimeInputPath) as JsonObject
            ?? throw new InvalidOperationException(
                $"Embedded collection '{slots[^1].FieldId}' has no declared Runtime inputs.");
        var items = JsonPath.ParseRequiredArray(
            value,
            $"Embedded collection '{slots[^1].FieldId}' items");
        inputs[projection.RuntimeValueKey] = items.DeepClone();
    }

    private static JsonObject? EmbeddedOverrides(
        JsonObject config,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        bool createIfMissing)
    {
        JsonObject? currentConfig = config;
        JsonObject? overrides = null;
        foreach (var slot in slots)
        {
            if (currentConfig is null)
            {
                return null;
            }

            overrides = EmbeddedOverrides(
                currentConfig,
                slot,
                createIfMissing);
            currentConfig = overrides;
        }

        return overrides;
    }

    private static JsonObject? EmbeddedOverrides(
        JsonObject config,
        EmbeddedComponentSlotDefinition slot,
        bool createIfMissing)
    {
        var slotValue = JsonPath.Get(config, slot.SlotPath);
        JsonObject slotNode;
        if (slotValue is null)
        {
            if (!createIfMissing)
            {
                return null;
            }

            slotNode = [];
            JsonPath.Set(config, slot.SlotPath, slotNode);
        }
        else
        {
            slotNode = slotValue as JsonObject
                ?? throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' must be an object.");
        }

        if (slotNode.TryGetPropertyValue(
                "overrides",
                out var overridesNode))
        {
            return overridesNode as JsonObject
                ?? throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' overrides must be an object.");
        }

        if (!createIfMissing)
        {
            return null;
        }

        var overrides = new JsonObject();
        slotNode["overrides"] = overrides;
        return overrides;
    }
}
