using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ComponentClassFieldValueService
{
    private readonly IComponentClassFieldStore _database;
    private readonly IComponentDocumentStore _documents;
    private readonly EmbeddedComponentDocumentStore _embeddedDocuments;

    public ComponentClassFieldValueService(
        IComponentClassFieldStore database,
        IComponentDocumentStore documents)
    {
        _database = database;
        _documents = documents;
        _embeddedDocuments =
            new EmbeddedComponentDocumentStore(documents);
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
        return ValidateFieldValue(ApplyVariantLock(node, fieldValue));
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

            ValidateNextValue(node, fieldId, value);
            _database.UpdateComponentVariantField(node, fieldId, value);
            return;
        }

        ValidateNextValue(node, fieldId, value);
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

        return ValidateFieldValue(ApplyVariantLock(
            node,
            _documents.CreateEmbeddedComponentFieldValue(
                node,
                [slot],
                embeddedFieldId)));
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

        return ValidateFieldValue(ApplyVariantLock(
            node,
            _documents.CreateEmbeddedComponentFieldValue(
                node,
                slots,
                embeddedFieldId)));
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

        FieldOptionContract.ValidateValue(
            CreateEmbeddedFieldValue(node, [slot], embeddedFieldId).Definition,
            value,
            $"Dictionary field '{embeddedFieldId}'");
        _documents.UpdateEmbeddedComponentField(
            node,
            [slot],
            embeddedFieldId,
            value);
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

        FieldOptionContract.ValidateValue(
            CreateEmbeddedFieldValue(node, slots, embeddedFieldId).Definition,
            value,
            $"Dictionary field '{embeddedFieldId}'");
        _documents.UpdateEmbeddedComponentField(
            node,
            slots,
            embeddedFieldId,
            value);
    }

    public FieldValue CreateEmbeddedFieldValue(EditorEmbeddedContext context, string embeddedFieldId) =>
        ValidateFieldValue(_embeddedDocuments.CreateFieldValue(context, embeddedFieldId));

    public Task CommitEmbeddedFieldValueAsync(
        EditorEmbeddedContext context,
        string embeddedFieldId,
        string value)
    {
        FieldOptionContract.ValidateValue(
            CreateEmbeddedFieldValue(context, embeddedFieldId).Definition,
            value,
            $"Dictionary field '{embeddedFieldId}'");
        return _embeddedDocuments.CommitFieldValueAsync(
            context,
            embeddedFieldId,
            value);
    }

    public void CommitEmbeddedFieldValue(
        EditorEmbeddedContext context,
        string embeddedFieldId,
        string value)
    {
        FieldOptionContract.ValidateValue(
            CreateEmbeddedFieldValue(context, embeddedFieldId).Definition,
            value,
            $"Dictionary field '{embeddedFieldId}'");
        _embeddedDocuments.CommitFieldValue(
            context,
            embeddedFieldId,
            value);
    }

    private void ValidateNextValue(
        ProjectTreeNode node,
        string fieldId,
        string value)
    {
        FieldOptionContract.ValidateValue(
            CreateFieldValue(node, fieldId).Definition,
            value,
            $"Dictionary field '{fieldId}'");
    }

    private static FieldValue ValidateFieldValue(FieldValue fieldValue)
    {
        FieldOptionContract.ValidateValue(
            fieldValue.Definition,
            fieldValue.Value,
            $"Dictionary field '{fieldValue.Definition.Id}'");
        return fieldValue;
    }

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
