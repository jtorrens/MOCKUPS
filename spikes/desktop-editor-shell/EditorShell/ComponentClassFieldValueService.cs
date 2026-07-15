using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ComponentClassFieldValueService
{
    private readonly SpikeDatabase _database;

    public ComponentClassFieldValueService(SpikeDatabase database)
    {
        _database = database;
    }

    public bool CanHandle(ProjectTreeNodeKind nodeKind, string fieldId)
    {
        return nodeKind is ProjectTreeNodeKind.ComponentClass or ProjectTreeNodeKind.ComponentPreset
            && fieldId.StartsWith("component.", StringComparison.Ordinal);
    }

    public FieldValue CreateFieldValue(ProjectTreeNode node, string fieldId)
    {
        if (!CanHandle(node.Kind, fieldId))
        {
            throw new InvalidOperationException($"Component class field '{fieldId}' is not supported for '{node.Kind}'.");
        }

        var fieldValue = node.Kind == ProjectTreeNodeKind.ComponentPreset
            ? _database.CreateComponentPresetFieldValue(node, fieldId)
            : _database.CreateComponentClassFieldValue(node.Id, fieldId);
        return ApplyPresetLock(node, fieldValue);
    }

    public void CommitFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        if (!CanHandle(node.Kind, fieldId))
        {
            throw new InvalidOperationException($"Component class field '{fieldId}' is not supported for '{node.Kind}'.");
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentPreset)
        {
            if (node.IsLocked) return;

            _database.UpdateComponentPresetField(node, fieldId, value);
            return;
        }

        _database.UpdateComponentClassField(node.Id, fieldId, value);
    }

    public FieldValue CreateEmbeddedFieldValue(
        ProjectTreeNode node,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentPreset and not ProjectTreeNodeKind.Module and not ProjectTreeNodeKind.ModuleVariant)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{node.Kind}'.");
        }

        var slot = EmbeddedComponentSlotCatalog.Get(slotFieldId);
        if (!slot.EmbeddedComponentType.Equals(embeddedComponentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Embedded component '{embeddedComponentType}' is not supported for slot '{slotFieldId}'.");
        }

        return ApplyPresetLock(node, _database.CreateEmbeddedComponentFieldValue(node, [slot], embeddedFieldId));
    }

    public FieldValue CreateEmbeddedFieldValue(
        ProjectTreeNode node,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentPreset and not ProjectTreeNodeKind.Module and not ProjectTreeNodeKind.ModuleVariant)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{node.Kind}'.");
        }

        return ApplyPresetLock(node, _database.CreateEmbeddedComponentFieldValue(node, slots, embeddedFieldId));
    }

    public void CommitEmbeddedFieldValue(
        ProjectTreeNode node,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId,
        string value)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentPreset and not ProjectTreeNodeKind.Module and not ProjectTreeNodeKind.ModuleVariant)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{node.Kind}'.");
        }

        var slot = EmbeddedComponentSlotCatalog.Get(slotFieldId);
        if (!slot.EmbeddedComponentType.Equals(embeddedComponentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Embedded component '{embeddedComponentType}' is not supported for slot '{slotFieldId}'.");
        }

        if (node.IsLocked) return;

        _database.UpdateEmbeddedComponentField(node, [slot], embeddedFieldId, value);
    }

    public void CommitEmbeddedFieldValue(
        ProjectTreeNode node,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentPreset and not ProjectTreeNodeKind.Module and not ProjectTreeNodeKind.ModuleVariant)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{node.Kind}'.");
        }

        if (node.IsLocked) return;

        _database.UpdateEmbeddedComponentField(node, slots, embeddedFieldId, value);
    }

    public FieldValue CreateEmbeddedFieldValue(EditorEmbeddedContext context, string embeddedFieldId) =>
        context.CreateFieldValue(_database, embeddedFieldId);

    public void CommitEmbeddedFieldValue(EditorEmbeddedContext context, string embeddedFieldId, string value) =>
        context.CommitFieldValue(_database, embeddedFieldId, value);

    private static FieldValue ApplyPresetLock(ProjectTreeNode node, FieldValue fieldValue)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentPreset and not ProjectTreeNodeKind.ModuleVariant || !node.IsLocked)
        {
            return fieldValue;
        }

        return fieldValue with
        {
            Definition = fieldValue.Definition with { IsEditable = false },
        };
    }
}
