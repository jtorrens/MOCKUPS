using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
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
        StructuredCollectionAddress address,
        string itemId,
        string fieldJsonKey,
        JsonNode? value) =>
        UpdateModuleInstanceRuntimeCollectionValues(
            moduleInstanceId,
            address,
            itemId,
            new Dictionary<string, JsonNode?>
            {
                [fieldJsonKey] = value,
            });

    public void UpdateModuleInstanceRuntimeCollectionValues(
        string moduleInstanceId,
        StructuredCollectionAddress address,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        using var connection = context.OpenConnection();
        production.UpdateModuleInstanceRuntimeCollectionValues(
            connection,
            moduleInstanceId,
            address,
            itemId,
            values,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
    }

    public StructuredCollectionMutationResult MutateModuleInstanceStructuredCollection(
        string moduleInstanceId,
        StructuredCollectionMutation mutation)
    {
        using var connection = context.OpenConnection();
        return production.MutateModuleInstanceStructuredCollection(
            connection,
            moduleInstanceId,
            mutation,
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
