using Microsoft.Data.Sqlite;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ResourceAssetCleanupRepository(SqliteProjectContext context)
{
    internal IReadOnlyList<ResourceAssetCleanupPlan> Read(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, label, root_path, target_path, entries_json, last_error FROM resource_asset_cleanup ORDER BY id";
        using var reader = command.ExecuteReader();
        var result = new List<ResourceAssetCleanupPlan>();
        while (reader.Read())
            result.Add(ResourceAssetCleanupPlan.Read(reader.GetString(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        return result;
    }

    internal void Add(SqliteConnection connection, SqliteTransaction transaction, ResourceAssetCleanupPlan plan) =>
        context.Execute(connection, transaction,
            "INSERT INTO resource_asset_cleanup (id,label,root_path,target_path,entries_json,last_error) VALUES ($id,$label,$root,$target,$entries,'')",
            ("$id", plan.Id), ("$label", plan.Label), ("$root", plan.Root), ("$target", plan.Target), ("$entries", plan.EntriesJson()));

    internal void Complete(SqliteConnection connection, string id) =>
        context.Execute(connection, "DELETE FROM resource_asset_cleanup WHERE id=$id", ("$id", id));

    internal void Failed(SqliteConnection connection, string id, string error) =>
        context.Execute(connection, "UPDATE resource_asset_cleanup SET last_error=$error WHERE id=$id", ("$id", id), ("$error", error));
}
