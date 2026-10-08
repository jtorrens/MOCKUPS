using Microsoft.Data.Sqlite;

namespace Mockups.DesktopEditorShell.Data;

// One durable lifecycle for resource files. SQL owns the commit; this service
// never cleans a candidate transaction or runs from session startup.
internal sealed class ResourceAssetCleanupService(
    SqliteProjectContext context,
    Func<SqliteConnection, ResourceAssetCleanupPlan, bool> isReferenced) : IResourceAssetCleanupStore
{
    private readonly ResourceAssetCleanupRepository _repository = new(context);

    internal void Write(SqliteConnection connection, string label, string root,
        IReadOnlyDictionary<string, byte[]> files, Action<SqliteTransaction> writeRecords)
    {
        lock (context.WriteGate)
        {
            root = ResourceAssetCleanupPlan.StoredPath(root);
            foreach (var relative in files.Keys)
                RequireAvailable(ResourceAssetCleanupPlan.ContainedPath(root, relative));
            var plan = ResourceAssetWritePlan.Capture(label, root, files);
            _repository.AddWrite(connection, plan);
            try
            {
                plan.Apply();
                using var transaction = connection.BeginTransaction();
                writeRecords(transaction);
                _repository.ConfirmWrite(connection, transaction, plan.Id);
                transaction.Commit();
            }
            catch (Exception exception)
            {
                // Commit errors can have an ambiguous outcome. The durable
                // state, not the caught exception, decides whether undo is safe.
                var persisted = _repository.ReadWrites(connection).Single(item => item.Id == plan.Id);
                if (persisted.Committed)
                {
                    TryRecover(connection, persisted);
                    return;
                }
                if (!TryRecover(connection, persisted))
                    throw new IOException("Resource write failed; recovery is pending in Settings → Resource cleanup. " + exception.Message, exception);
                throw;
            }
            // After SQL confirmation, failure to release the undo image is only
            // pending cleanup, never permission to roll back committed files.
            TryRecover(connection, plan with { Committed = true });
        }
    }

    internal ResourceAssetDeletionResult Commit(SqliteConnection connection, IReadOnlyList<ResourceAssetCleanupPlan> plans,
        Action<SqliteTransaction> deleteRecords)
    {
        lock (context.WriteGate)
        {
            foreach (var plan in plans)
                RequireAvailable(ResourceAssetCleanupPlan.ContainedPath(plan.Root, plan.Target));
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
            plan.Id, plan.Label, ResourceAssetCleanupPlan.ContainedPath(plan.Root, plan.Target), plan.Error,
            "The record deletion was committed. Delete its retained files."))
            .Concat(_repository.ReadWrites(connection).Select(plan => new ResourceAssetCleanupItem(
                plan.Id, plan.Label, string.Join(Environment.NewLine, plan.Paths), plan.Error,
                plan.Committed ? "The write was committed. Release its retained recovery data."
                    : "The write was not committed. Restore the original files."))).ToList();
    }

    public void Retry(string id)
    {
        lock (context.WriteGate)
        {
            using var connection = context.OpenConnection();
            var write = _repository.ReadWrites(connection).SingleOrDefault(plan => plan.Id == id);
            if (write is not null) TryRecover(connection, write);
            else TryClean(connection, _repository.Read(connection).Single(plan => plan.Id == id));
        }
    }

    internal void RequireAvailable(string path)
    {
        using var connection = context.OpenConnection();
        if (_repository.Read(connection).Any(plan => ResourceAssetCleanupPlan.Overlaps(path,
                ResourceAssetCleanupPlan.ContainedPath(plan.Root, plan.Target)))
            || _repository.ReadWrites(connection).Any(plan => plan.Paths.Any(target => ResourceAssetCleanupPlan.Overlaps(path, target))))
            throw new InvalidOperationException("This asset path has pending recovery. Review Resource cleanup in Settings first.");
    }

    private bool TryRecover(SqliteConnection connection, ResourceAssetWritePlan plan)
    {
        try
        {
            plan.Recover();
            _repository.CompleteWrite(connection, plan.Id);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException)
        {
            try { _repository.FailedWrite(connection, plan.Id, exception.Message); }
            catch (SqliteException) { }
            return false;
        }
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
