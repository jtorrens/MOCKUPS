using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ComponentClassFieldValueService
{
    private readonly SpikeDatabase _database;
    private readonly EmbeddedComponentDocumentStore _embeddedDocuments;

    public ComponentClassFieldValueService(SpikeDatabase database)
    {
        _database = database;
        _embeddedDocuments = new EmbeddedComponentDocumentStore(database);
    }

    public bool CanHandle(ProjectTreeNodeKind nodeKind, string fieldId)
    {
        return nodeKind is ProjectTreeNodeKind.ComponentClass or ProjectTreeNodeKind.ComponentVariant
            && fieldId.StartsWith("component.", StringComparison.Ordinal);
    }

    public FieldValue CreateFieldValue(ProjectTreeNode node, string fieldId)
    {
        if (!CanHandle(node.Kind, fieldId))
        {
            throw new InvalidOperationException($"Component class field '{fieldId}' is not supported for '{node.Kind}'.");
        }

        var fieldValue = node.Kind == ProjectTreeNodeKind.ComponentVariant
            ? _database.CreateComponentVariantFieldValue(node, fieldId)
            : _database.CreateComponentClassFieldValue(node.Id, fieldId);
        return ApplyVariantLock(node, fieldValue);
    }

    public void CommitFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        if (!CanHandle(node.Kind, fieldId))
        {
            throw new InvalidOperationException($"Component class field '{fieldId}' is not supported for '{node.Kind}'.");
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentVariant)
        {
            if (node.IsLocked) return;

            _database.UpdateComponentVariantField(node, fieldId, value);
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
        if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.Module and not ProjectTreeNodeKind.ModuleVariant)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{node.Kind}'.");
        }

        var slot = EmbeddedComponentSlotCatalog.Get(slotFieldId);
        if (!slot.EmbeddedComponentType.Equals(embeddedComponentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Embedded component '{embeddedComponentType}' is not supported for slot '{slotFieldId}'.");
        }

        return ApplyVariantLock(node, _database.CreateEmbeddedComponentFieldValue(node, [slot], embeddedFieldId));
    }

    public FieldValue CreateEmbeddedFieldValue(
        ProjectTreeNode node,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.Module and not ProjectTreeNodeKind.ModuleVariant)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{node.Kind}'.");
        }

        return ApplyVariantLock(node, _database.CreateEmbeddedComponentFieldValue(node, slots, embeddedFieldId));
    }

    public void CommitEmbeddedFieldValue(
        ProjectTreeNode node,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId,
        string value)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.Module and not ProjectTreeNodeKind.ModuleVariant)
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
        if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.Module and not ProjectTreeNodeKind.ModuleVariant)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{node.Kind}'.");
        }

        if (node.IsLocked) return;

        _database.UpdateEmbeddedComponentField(node, slots, embeddedFieldId, value);
    }

    public FieldValue CreateEmbeddedFieldValue(EditorEmbeddedContext context, string embeddedFieldId) =>
        _embeddedDocuments.CreateFieldValue(context, embeddedFieldId);

    public void CommitEmbeddedFieldValue(EditorEmbeddedContext context, string embeddedFieldId, string value) =>
        _embeddedDocuments.CommitFieldValue(context, embeddedFieldId, value);

    private static FieldValue ApplyVariantLock(ProjectTreeNode node, FieldValue fieldValue)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.ModuleVariant || !node.IsLocked)
        {
            return fieldValue;
        }

        return fieldValue with
        {
            Definition = fieldValue.Definition with { IsEditable = false },
        };
    }
}
