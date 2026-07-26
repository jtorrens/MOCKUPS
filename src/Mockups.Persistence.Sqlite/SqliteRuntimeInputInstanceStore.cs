using Microsoft.Data.Sqlite;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteRuntimeInputInstanceStore(
    SqliteProjectContext context,
    SqliteDesignOwner design,
    SqliteProductionOwner production,
    SqliteResourceOwner resources)
    : IRuntimeInputInstanceStore
{
    public void UpdateModuleInstanceRuntimeValue(
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value)
    {
        using var connection = context.OpenConnection();
        production.UpdateModuleInstanceRuntimeValue(
            connection,
            moduleInstanceId,
            jsonKey,
            value,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
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
        using var connection = context.OpenConnection();
        production.UpdateModuleInstanceRuntimeCollectionValues(
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
        using var connection = context.OpenConnection();
        production.AddModuleInstanceRuntimeCollectionItem(
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
        using var connection = context.OpenConnection();
        production.InsertModuleInstanceRuntimeCollectionItemAfter(
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
        using var connection = context.OpenConnection();
        production.DuplicateModuleInstanceRuntimeCollectionItem(
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
        using var connection = context.OpenConnection();
        production.DeleteModuleInstanceRuntimeCollectionItem(
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
        using var connection = context.OpenConnection();
        production.MoveModuleInstanceRuntimeCollectionItem(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            offset,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
    }

    private IReadOnlySet<string> ModuleInstanceProjectActorIds(
        SqliteConnection connection,
        string moduleInstanceId)
    {
        var instance = production.ModuleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        var module = design.AppModuleRepository.GetModule(
            connection,
            instance.ModuleId);
        return resources.ActorRepository.QueryAll(connection)
            .Where((actor) => actor.ProjectId.Equals(
                module.ProjectId,
                StringComparison.Ordinal))
            .Select((actor) => actor.Id)
            .ToHashSet(StringComparer.Ordinal);
    }
}
