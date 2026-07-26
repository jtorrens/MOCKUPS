using Mockups.DesktopEditorShell.Data;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RuntimeInputInstanceDocumentStore
{
    private readonly SpikeDatabase _database;
    private readonly ModuleInstanceAnimationDocumentStore _animationDocuments;

    public RuntimeInputInstanceDocumentStore(SpikeDatabase database)
    {
        _database = database;
        _animationDocuments = new ModuleInstanceAnimationDocumentStore(
            database,
            new ModuleInstanceTimelineDataSource(database));
    }

    public void UpdateRuntimeValue(string moduleInstanceId, string jsonKey, JsonNode? value)
    {
        _database.UpdateModuleInstanceRuntimeValue(moduleInstanceId, jsonKey, value);
    }

    public void AddCollectionItem(string moduleInstanceId, string collectionJsonKey, JsonObject item)
    {
        _database.AddModuleInstanceRuntimeCollectionItem(moduleInstanceId, collectionJsonKey, item);
    }

    public void InsertCollectionItemAfter(
        string moduleInstanceId,
        string collectionJsonKey,
        string afterItemId,
        JsonObject item)
    {
        _database.InsertModuleInstanceRuntimeCollectionItemAfter(
            moduleInstanceId,
            collectionJsonKey,
            afterItemId,
            item);
    }

    public void DuplicateCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        JsonObject duplicate,
        IReadOnlyDictionary<string, string> targetIdMappings)
    {
        _database.DuplicateModuleInstanceRuntimeCollectionItem(
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            duplicate,
            targetIdMappings);
    }

    public void MoveCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        int offset)
    {
        _database.MoveModuleInstanceRuntimeCollectionItem(
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            offset);
    }

    public void DeleteCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId)
    {
        _database.DeleteModuleInstanceRuntimeCollectionItem(
            moduleInstanceId,
            collectionJsonKey,
            itemId);
    }

    public void UpdateCollectionValue(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        string fieldJsonKey,
        JsonNode? value)
    {
        _database.UpdateModuleInstanceRuntimeCollectionValue(
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            fieldJsonKey,
            value);
    }

    public void UpdateCollectionValues(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        _database.UpdateModuleInstanceRuntimeCollectionValues(
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            values);
    }

    public string AnimationJson(string moduleInstanceId)
    {
        return _animationDocuments.Load(moduleInstanceId).AnimationJson;
    }

    public string SaveAnimationJson(string moduleInstanceId, string animationJson)
    {
        return _animationDocuments.SaveAnimationJson(moduleInstanceId, animationJson);
    }
}
