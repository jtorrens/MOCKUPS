using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteRuntimeInputInstanceStore(
    SqliteProjectContext context,
    SqliteProductionOwner production)
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
            value);
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
            values);
    }

    public StructuredCollectionMutationResult MutateModuleInstanceStructuredCollection(
        string moduleInstanceId,
        StructuredCollectionMutation mutation)
    {
        using var connection = context.OpenConnection();
        return production.MutateModuleInstanceStructuredCollection(
            connection,
            moduleInstanceId,
            mutation);
    }

}
