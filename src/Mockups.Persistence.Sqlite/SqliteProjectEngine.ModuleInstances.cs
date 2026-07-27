using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId) =>
        _productionOwner.GetModuleInstanceSettings(moduleInstanceId);

    public string GetModuleInstanceModuleName(
        string moduleInstanceId) =>
        _productionOwner.GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceTransitionType(
        string moduleInstanceId) =>
        _productionOwner.GetModuleInstanceTransitionType(
            moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId) =>
        _productionOwner.GetModuleInstanceRuntimePreviewJson(
            moduleInstanceId);

    public void UpdateModuleInstanceRuntimeValue(
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value)
    {
        using var connection = OpenConnection();
        _productionOwner.UpdateModuleInstanceRuntimeValue(
            connection,
            moduleInstanceId,
            jsonKey,
            value,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
    }

    public void UpdateModuleInstanceAnimationJson(
        string moduleInstanceId,
        string animationJson)
    {
        using var connection = OpenConnection();
        _productionOwner.UpdateModuleInstanceAnimationJson(
            connection,
            moduleInstanceId,
            animationJson);
    }

    public void UpdateModuleInstanceRuntimeCollectionValue(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        string fieldJsonKey,
        JsonNode? value) =>
        UpdateModuleInstanceRuntimeCollectionValues(
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            new Dictionary<string, JsonNode?>
            {
                [fieldJsonKey] = value,
            });

    public void UpdateModuleInstanceRuntimeCollectionValues(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        using var connection = OpenConnection();
        _productionOwner.UpdateModuleInstanceRuntimeCollectionValues(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            values,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
    }

    public void AddModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        JsonObject item)
    {
        using var connection = OpenConnection();
        _productionOwner.AddModuleInstanceRuntimeCollectionItem(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            item,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
    }

    public void InsertModuleInstanceRuntimeCollectionItemAfter(
        string moduleInstanceId,
        string collectionJsonKey,
        string afterItemId,
        JsonObject item)
    {
        using var connection = OpenConnection();
        _productionOwner
            .InsertModuleInstanceRuntimeCollectionItemAfter(
                connection,
                moduleInstanceId,
                collectionJsonKey,
                afterItemId,
                item,
                ModuleInstanceProjectActorIds(
                    connection,
                    moduleInstanceId));
    }

    public void DuplicateModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        JsonObject duplicate,
        IReadOnlyDictionary<string, string> targetIdMappings)
    {
        using var connection = OpenConnection();
        _productionOwner
            .DuplicateModuleInstanceRuntimeCollectionItem(
                connection,
                moduleInstanceId,
                collectionJsonKey,
                itemId,
                duplicate,
                targetIdMappings,
                ModuleInstanceProjectActorIds(
                    connection,
                    moduleInstanceId));
    }

    public void DeleteModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId)
    {
        using var connection = OpenConnection();
        _productionOwner.DeleteModuleInstanceRuntimeCollectionItem(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
    }

    public void MoveModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        int offset)
    {
        using var connection = OpenConnection();
        _productionOwner.MoveModuleInstanceRuntimeCollectionItem(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            offset,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
    }

    public IReadOnlyList<ModuleInstanceSlot>
        GetShotModuleInstanceSlots(string shotId) =>
            _productionOwner.GetShotModuleInstanceSlots(shotId);

    public IReadOnlyList<ShotModuleChoice> GetAvailableShotModules(
        string shotId)
    {
        using var connection = OpenConnection();
        var shot = _productionOwner.ShotRepository.Get(
            connection,
            shotId);
        var apps = _designOwner.AppModuleRepository
            .QueryApps(connection)
            .Where((app) => app.ProjectId == shot.ProjectId)
            .OrderBy((app) => app.SortOrder)
            .ThenBy((app) => app.Name)
            .ToDictionary((app) => app.Id, StringComparer.Ordinal);
        return _designOwner.AppModuleRepository
            .QueryModules(connection)
            .Where((module) => apps.ContainsKey(module.AppId))
            .OrderBy((module) => apps[module.AppId].SortOrder)
            .ThenBy((module) => apps[module.AppId].Name)
            .ThenBy((module) => module.SortOrder)
            .ThenBy((module) => module.Name)
            .Select((module) => new ShotModuleChoice(
                module.Id,
                module.Name,
                apps[module.AppId].Name,
                module.AppId,
                module.RecordClassId))
            .ToList();
    }

    public ProjectTreeNode AddModuleInstance(
        ProjectTreeNode shot,
        ShotModuleInstanceDraft draft)
    {
        using var connection = OpenConnection();
        var moduleSettings =
            _designOwner.GetModuleSettings(draft.Module.Id);
        return _productionOwner.AddModuleInstance(
            connection,
            shot,
            draft,
            ProjectActorIds(
                connection,
                moduleSettings.ProjectId));
    }

    public ProjectTreeNode RenameModuleInstance(
        ProjectTreeNode node,
        string name) =>
        _productionOwner.RenameModuleInstance(node, name);

    public void MoveModuleInstance(
        string moduleInstanceId,
        int offset) =>
        _productionOwner.MoveModuleInstance(
            moduleInstanceId,
            offset);

    public void UpdateModuleInstanceField(
        string moduleInstanceId,
        string fieldId,
        string value)
    {
        using var connection = OpenConnection();
        _productionOwner.UpdateModuleInstanceField(
            connection,
            moduleInstanceId,
            fieldId,
            value,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
    }

}
