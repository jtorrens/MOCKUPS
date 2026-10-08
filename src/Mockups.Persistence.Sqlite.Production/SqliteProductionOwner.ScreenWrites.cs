using Microsoft.Data.Sqlite;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner
{
    private T CommitModuleInstanceWrite<T>(
        SqliteConnection connection,
        string id,
        Func<SqliteTransaction, ModuleInstanceRecord, T> prepare) =>
        CommitScreenWrite(connection, transaction =>
        {
            var instance = _moduleInstanceRepository.Get(connection, id);
            return (prepare(transaction, instance), (IReadOnlyList<string>)[instance.ShotId]);
        });

    // The mutation prepares its candidate inside the transaction. No candidate is
    // published until the same semantic guard used at startup and timing succeed.
    private T CommitScreenWrite<T>(
        SqliteConnection connection,
        Func<SqliteTransaction, (T Result, IReadOnlyList<string> ShotIds)> prepare)
    {
        lock (WriteGate)
        {
            using var transaction = connection.BeginTransaction();
            var candidate = prepare(transaction);
            CompleteScreenWrite(connection, transaction, candidate.ShotIds);
            transaction.Commit();
            return candidate.Result;
        }
    }

    // An enclosing aggregate transaction (for example a Variant change) uses
    // this same completion boundary without opening or committing a second one.
    internal void CompleteScreenWrite(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IEnumerable<string> shotIds)
    {
        foreach (var shotId in shotIds.Distinct(StringComparer.Ordinal))
        {
            var shot = _shotRepository.Get(connection, shotId);
            var actors = _runtimeReferences.ActorIds(connection, shot.ProjectId);
            foreach (var instance in _moduleInstanceRepository.QueryByShot(connection, shotId))
                ValidateModuleInstanceDocuments(connection, instance.Id,
                    ParseJsonObject(instance.ContentJson), ParseJsonObject(instance.AnimationJson), actors);
            SynchronizeTimelineDurations(connection, shotId, transaction);
        }
    }

    internal EpisodeRecord DuplicateEpisode(SqliteConnection connection, string id, string name) =>
        CommitScreenWrite(connection, transaction =>
        {
            var copy = _projectEpisodeRepository.DuplicateEpisode(connection, id, name, transaction);
            var shots = _shotRepository.QueryByEpisode(connection, copy.Id).Select(shot => shot.Id).ToArray();
            return (copy, (IReadOnlyList<string>)shots);
        });

    internal void DeleteModuleInstance(SqliteConnection connection, string id) =>
        CommitScreenWrite(connection, transaction =>
        {
            var instance = _moduleInstanceRepository.Get(connection, id);
            _moduleInstanceRepository.Delete(connection, id, transaction);
            return (true, (IReadOnlyList<string>)[instance.ShotId]);
        });

    internal ModuleInstanceRecord DuplicateModuleInstance(
        SqliteConnection connection, string sourceId, string requestedName)
    {
        var id = CommitScreenWrite(connection, transaction =>
        {
            var source = _moduleInstanceRepository.Get(connection, sourceId);
            var copy = _moduleInstanceRepository.Duplicate(connection, sourceId,
                $"module_instance_{Guid.NewGuid():N}", source.ShotId,
                _moduleInstanceRepository.UniqueName(connection, source.ShotId, requestedName),
                _moduleInstanceRepository.NextSortOrder(connection, source.ShotId), transaction);
            return (copy.Id, (IReadOnlyList<string>)[source.ShotId]);
        });
        return _moduleInstanceRepository.Get(connection, id);
    }
}
