using Microsoft.Data.Sqlite;

namespace Mockups.DesktopEditorShell.Data;

// One durable lifecycle for resource files. SQL owns the commit; this service
// never cleans a candidate transaction or runs from session startup.
internal sealed class ResourceAssetCleanupService(
    SqliteProjectContext context,
    Func<SqliteConnection, ResourceAssetCleanupPlan, bool> isReferenced) : IResourceAssetCleanupStore
{
    private readonly ResourceAssetCleanupRepository _repository = new(context);

    internal ResourceAssetDeletionResult Commit(SqliteConnection connection, IReadOnlyList<ResourceAssetCleanupPlan> plans,
        Action<SqliteTransaction> deleteRecords)
    {
        lock (context.WriteGate)
        {
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var plan in plans) _repository.Add(connection, transaction, plan);
                deleteRecords(transaction);
                transaction.Commit();
            }
            // Cleanup failure is recorded, not reported as a rolled-back delete.
            return new(plans.Count(plan => !TryClean(connection, plan)));
        }
    }

    public IReadOnlyList<ResourceAssetCleanupItem> GetPending()
    {
        using var connection = context.OpenConnection();
        return _repository.Read(connection).Select(plan => new ResourceAssetCleanupItem(
            plan.Id, plan.Label, ResourceAssetCleanupPlan.ContainedPath(plan.Root, plan.Target), plan.Error)).ToList();
    }

    public void Retry(string id)
    {
        lock (context.WriteGate)
        {
            using var connection = context.OpenConnection();
            var plan = _repository.Read(connection).Single(plan => plan.Id == id);
            TryClean(connection, plan);
        }
    }

    internal void RequireAvailable(string path)
    {
        using var connection = context.OpenConnection();
        if (_repository.Read(connection).Any(plan => ResourceAssetCleanupPlan.Overlaps(path,
                ResourceAssetCleanupPlan.ContainedPath(plan.Root, plan.Target))))
            throw new InvalidOperationException("This asset path has pending deletion cleanup. Review Resource cleanup in Settings first.");
    }

    private bool TryClean(SqliteConnection connection, ResourceAssetCleanupPlan plan)
    {
        try
        {
            plan.RequireNativeRoot();
            if (isReferenced(connection, plan))
                throw new IOException("The target is currently referenced by a resource; its files were retained.");
            plan.Clean();
            _repository.Complete(connection, plan.Id);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException)
        {
            // If recording the error fails, the original durable job still exists.
            // Do not turn a committed record deletion into a false failure result.
            try { _repository.Failed(connection, plan.Id, exception.Message); }
            catch (SqliteException) { }
            return false;
        }
    }
}
