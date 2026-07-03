using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorFieldValueRouter
{
    private readonly CoreFieldValueService _coreFields;
    private readonly RecordClassFieldValueService _recordClassFields;
    private readonly ComponentClassFieldValueService _componentClassFields;
    private readonly ActorAvatarPreviewController _actorAvatarPreviews;
    private readonly EditorFieldPostCommitEffects _postCommitEffects;

    public EditorFieldValueRouter(
        CoreFieldValueService coreFields,
        RecordClassFieldValueService recordClassFields,
        ComponentClassFieldValueService componentClassFields,
        ActorAvatarPreviewController actorAvatarPreviews,
        EditorFieldPostCommitEffects postCommitEffects)
    {
        _coreFields = coreFields;
        _recordClassFields = recordClassFields;
        _componentClassFields = componentClassFields;
        _actorAvatarPreviews = actorAvatarPreviews;
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
        if (node.Kind == ProjectTreeNodeKind.Actor && fieldId == "actor.avatar.filePath")
        {
            return _actorAvatarPreviews.RelativeActorMediaPath(node.Id, value) ?? value;
        }

        return value;
    }

    public string CurrentStoredValue(ProjectTreeNode node, string fieldId)
    {
        return fieldId switch
        {
            "core.name" => node.Name,
            "core.notes" => node.Notes,
            _ => Create(node, fieldId).Value,
        };
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
