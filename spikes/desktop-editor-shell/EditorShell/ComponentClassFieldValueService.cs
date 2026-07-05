using Mockups.DesktopEditorShell.Data;
using System;

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
        return nodeKind == ProjectTreeNodeKind.ComponentClass
            && fieldId.StartsWith("component.", StringComparison.Ordinal);
    }

    public FieldValue CreateFieldValue(ProjectTreeNode node, string fieldId)
    {
        if (!CanHandle(node.Kind, fieldId))
        {
            throw new InvalidOperationException($"Component class field '{fieldId}' is not supported for '{node.Kind}'.");
        }

        return _database.CreateComponentClassFieldValue(node.Id, fieldId);
    }

    public void CommitFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        if (!CanHandle(node.Kind, fieldId))
        {
            throw new InvalidOperationException($"Component class field '{fieldId}' is not supported for '{node.Kind}'.");
        }

        _database.UpdateComponentClassField(node.Id, fieldId, value);
    }

    public FieldValue CreateEmbeddedFieldValue(
        ProjectTreeNode node,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId)
    {
        if (node.Kind != ProjectTreeNodeKind.ComponentClass)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{node.Kind}'.");
        }

        return _database.CreateEmbeddedComponentFieldValue(
            node.Id,
            slotFieldId,
            embeddedComponentType,
            embeddedFieldId);
    }

    public void CommitEmbeddedFieldValue(
        ProjectTreeNode node,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId,
        string value)
    {
        if (node.Kind != ProjectTreeNodeKind.ComponentClass)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{node.Kind}'.");
        }

        _database.UpdateEmbeddedComponentField(
            node.Id,
            slotFieldId,
            embeddedComponentType,
            embeddedFieldId,
            value);
    }
}
