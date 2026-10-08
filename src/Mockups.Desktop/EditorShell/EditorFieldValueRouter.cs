using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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

    public ComponentOverrideFieldOwner OverrideOwner(ProjectTreeNode node, EditorEmbeddedContext? context = null) => new(
        JsonSerializer.Serialize(new
        {
            node.Kind, node.Id, Scope = "dictionary",
            RuntimeOwner = context?.RuntimeSource?.OwnerIdentity,
            RuntimeAddress = context?.RuntimeSource?.Address.Identity,
            RuntimeVariant = context?.RuntimeSource?.VariantReference,
            Slots = context?.Slots.Select(slot => slot.FieldId).ToArray(),
        }),
        fieldId => context is null ? Create(node, fieldId).Value
            : _componentClassFields.CreateEmbeddedFieldValue(context, fieldId).Value,
        (address, value) =>
        {
            if (context is null) Persist(node, address.FieldId, value);
            else _componentClassFields.CommitEmbeddedFieldValue(context, address.FieldId, value);
        },
        context is null && node.Kind is ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant
            ? (address, name) => _componentClassFields.PromoteOverridesToVariant(
                new ComponentOverridePromotionRequest(node, PromotionTarget(node, address), name))
            : null);

    private ComponentOverridePromotionTarget PromotionTarget(ProjectTreeNode node, ComponentOverrideAddress address)
    {
        var definition = Create(node, address.FieldId).Definition;
        if (definition.ValueKind == ValueKind.ComponentVariantSlot && address.Path.Count == 0)
            return new ComponentOverrideFieldPromotionTarget(address.FieldId);
        var collection = definition.StructuredCollection
            ?? throw new InvalidOperationException("An Override collection address requires collection metadata.");
        var owners = new List<StructuredCollectionOwnerSegment>();
        var key = collection.JsonKey;
        var index = 0;
        while (index + 2 < address.Path.Count)
        {
            owners.Add(new StructuredCollectionOwnerSegment(key, address.Path[index].ItemId));
            key = address.Path[index + 1].Property;
            index += 2;
        }
        var itemId = address.Path[index].ItemId;
        var slotKey = index + 1 < address.Path.Count ? address.Path[index + 1].Property : "";
        return new ComponentOverrideCollectionPromotionTarget(address.FieldId,
            new StructuredCollectionAddress(collection.JsonKey, owners, key), itemId,
            new ComponentOverridePromotionBoundary(slotKey, address.VariantReferenceKey, address.OverridesKey));
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

    public void Persist(ProjectTreeNode node, string fieldId, string value)
    {
        if (_recordClassFields.CanHandle(node.Kind, fieldId))
        {
            _recordClassFields.CommitFieldValue(node, fieldId, value);
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
        }
    }

    public Task ApplyPostCommitEffectsAsync(
        ProjectTreeNode node,
        string fieldId,
        string value)
    {
        if (_recordClassFields.CanHandle(node.Kind, fieldId)
            || _coreFields.CanHandle(fieldId))
        {
            return _postCommitEffects.ApplyAsync(
                node,
                fieldId,
                value);
        }
        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<string, FieldValue>
        CreateRecordReferenceOverrideFields(
            EditorEmbeddedContext context,
            IEnumerable<string> fieldIds) =>
        _recordClassFields.CreateRecordReferenceOverrideFields(
            context,
            fieldIds);

    public string CurrentRecordReferenceOverrideStoredValue(
        EditorEmbeddedContext context,
        string fieldId) =>
        _recordClassFields
            .CurrentRecordReferenceOverrideStoredValue(
                context,
                fieldId);

    public void PersistRecordReferenceOverride(
        EditorEmbeddedContext context,
        string fieldId,
        string value) =>
        _recordClassFields.CommitRecordReferenceOverrideField(
            context,
            fieldId,
            value);

    public void ClearRecordReferenceOverrides(
        ProjectTreeNode ownerNode,
        FieldDefinition definition) =>
        _recordClassFields.ClearRecordReferenceOverrides(
            ownerNode,
            definition);
}
