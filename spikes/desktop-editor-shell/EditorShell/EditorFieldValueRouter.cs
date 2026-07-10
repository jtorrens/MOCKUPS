using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorFieldValueRouter
{
    private readonly CoreFieldValueService _coreFields;
    private readonly RecordClassFieldValueService _recordClassFields;
    private readonly ComponentClassFieldValueService _componentClassFields;
    private readonly IEditorInlinePreviewController _inlinePreviews;
    private readonly EditorFieldPostCommitEffects _postCommitEffects;

    public EditorFieldValueRouter(
        CoreFieldValueService coreFields,
        RecordClassFieldValueService recordClassFields,
        ComponentClassFieldValueService componentClassFields,
        IEditorInlinePreviewController inlinePreviews,
        EditorFieldPostCommitEffects postCommitEffects)
    {
        _coreFields = coreFields;
        _recordClassFields = recordClassFields;
        _componentClassFields = componentClassFields;
        _inlinePreviews = inlinePreviews;
        _postCommitEffects = postCommitEffects;
    }

    public FieldValue Create(ProjectTreeNode node, string fieldId)
    {
        if (_recordClassFields.CanHandle(node.Kind, fieldId))
        {
            return _recordClassFields.CreateFieldValue(node, fieldId);
        }

        if (_componentClassFields.CanHandle(node.Kind, fieldId))
        {
            return _componentClassFields.CreateFieldValue(node, fieldId);
        }

        if (_coreFields.CanHandle(fieldId))
        {
            return _coreFields.CreateFieldValue(node, fieldId);
        }

        throw new InvalidOperationException($"Unknown field '{fieldId}' for record class '{node.RecordClassId}'.");
    }

    public string ToStorageValue(ProjectTreeNode node, string fieldId, string value)
    {
        return _inlinePreviews.ToStoragePath(node, fieldId, value);
    }

    public string CurrentStoredValue(ProjectTreeNode node, string fieldId)
    {
        if (fieldId == "core.name") return node.Name;
        if (fieldId == "core.notes") return node.Notes;

        var fieldValue = Create(node, fieldId);
        return fieldValue.IsInherited
            ? fieldValue.Definition.InheritedStorageValue
            : fieldValue.Value;
    }

    public void Commit(ProjectTreeNode node, string fieldId, string value)
    {
        if (_recordClassFields.CanHandle(node.Kind, fieldId))
        {
            _recordClassFields.CommitFieldValue(node, fieldId, value);
            _postCommitEffects.Apply(node, fieldId, value);
            return;
        }

        if (_componentClassFields.CanHandle(node.Kind, fieldId))
        {
            _componentClassFields.CommitFieldValue(node, fieldId, value);
            return;
        }

        if (_coreFields.CanHandle(fieldId))
        {
            _coreFields.CommitFieldValue(node, fieldId, value);
            _postCommitEffects.Apply(node, fieldId, value);
        }
    }
}
