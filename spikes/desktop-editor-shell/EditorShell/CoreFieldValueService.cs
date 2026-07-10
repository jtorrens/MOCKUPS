using Mockups.DesktopEditorShell.Data;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class CoreFieldValueService
{
    private readonly SpikeDatabase _database;

    public CoreFieldValueService(SpikeDatabase database)
    {
        _database = database;
    }

    public bool CanHandle(string fieldId)
    {
        return fieldId is "core.name" or "core.kind" or "core.notes";
    }

    public FieldValue CreateFieldValue(ProjectTreeNode node, string fieldId)
    {
        return fieldId switch
        {
            "core.name" => new FieldValue(
                new FieldDefinition(
                    "core.name",
                    "Name",
                    ValueKind.StringSingleLine,
                    IsEditable: IsPersisted(node.Kind),
                    DefaultValue: node.Name),
                node.Name),
            "core.kind" => new FieldValue(
                new FieldDefinition(
                    "core.kind",
                    "Class",
                    ValueKind.StringReadOnly,
                    IsEditable: false,
                    DefaultValue: node.RecordClassId),
                node.RecordClassId),
            "core.notes" => new FieldValue(
                new FieldDefinition(
                    "core.notes",
                    node.Kind switch
                    {
                        ProjectTreeNodeKind.Project => "Production Notes",
                        ProjectTreeNodeKind.Episode => "Episode Notes",
                        _ => "Notes",
                    },
                    ValueKind.StringMultiline,
                    IsEditable: IsPersisted(node.Kind),
                    DefaultValue: node.Notes),
                node.Notes),
            _ => throw new InvalidOperationException($"Core field '{fieldId}' is not supported."),
        };
    }

    public void CommitFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        if (fieldId == "core.name")
        {
            node.Name = value;
        }
        else if (fieldId == "core.notes")
        {
            node.Notes = value;
        }

        if (IsPersisted(node.Kind) && fieldId is "core.name" or "core.notes")
        {
            _database.UpdateNode(node);
        }
    }

    private static bool IsPersisted(ProjectTreeNodeKind nodeKind)
    {
        return nodeKind is ProjectTreeNodeKind.Project
            or ProjectTreeNodeKind.App
            or ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.Episode
            or ProjectTreeNodeKind.Shot
            or ProjectTreeNodeKind.PaletteColor
            or ProjectTreeNodeKind.Device
            or ProjectTreeNodeKind.Actor
            or ProjectTreeNodeKind.Theme
            or ProjectTreeNodeKind.ProductionFont
            or ProjectTreeNodeKind.IconTheme
            or ProjectTreeNodeKind.RenderPreset
            or ProjectTreeNodeKind.ComponentClass;
    }
}
