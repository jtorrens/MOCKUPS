using Microsoft.Data.Sqlite;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ResourceAssetCleanupRepository(SqliteProjectContext context)
{
    internal IReadOnlyList<ResourceAssetWritePlan> ReadWrites(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,label,root_path,entries_json,directories_json,committed,last_error FROM resource_asset_writes ORDER BY id";
        using var reader = command.ExecuteReader();
        var result = new List<ResourceAssetWritePlan>();
        while (reader.Read())
        {
            var state = reader.GetInt32(5);
            if (state is not (0 or 1)) throw new InvalidOperationException("Invalid resource write state.");
            result.Add(ResourceAssetWritePlan.Read(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), state == 1, reader.GetString(6)));
        }
        return result;
    }

    internal void AddWrite(SqliteConnection connection, ResourceAssetWritePlan plan) =>
        context.Execute(connection,
            "INSERT INTO resource_asset_writes (id,label,root_path,entries_json,directories_json,committed,last_error) VALUES ($id,$label,$root,$entries,$directories,0,'')",
            ("$id", plan.Id), ("$label", plan.Label), ("$root", plan.Root), ("$entries", plan.EntriesJson()), ("$directories", plan.DirectoriesJson()));

    internal void ConfirmWrite(SqliteConnection connection, SqliteTransaction transaction, string id) =>
        context.Execute(connection, transaction, "UPDATE resource_asset_writes SET committed=1 WHERE id=$id", ("$id", id));

    internal void CompleteWrite(SqliteConnection connection, string id) =>
        context.Execute(connection, "DELETE FROM resource_asset_writes WHERE id=$id", ("$id", id));

    internal void FailedWrite(SqliteConnection connection, string id, string error) =>
        context.Execute(connection, "UPDATE resource_asset_writes SET last_error=$error WHERE id=$id", ("$id", id), ("$error", error));

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
