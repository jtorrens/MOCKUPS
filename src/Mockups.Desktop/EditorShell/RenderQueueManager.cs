using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RenderQueueManager : IDisposable
{
    private const int MaximumTerminalJobs = 50;
    private const int MaximumRetrySnapshots = 5;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly string _path;
    private readonly string _storageRoot;
    private readonly IRenderJobExecutor _executor;
    private readonly Dictionary<string, CancellationTokenSource>
        _preparationCancellations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _preparationTasks =
        new(StringComparer.Ordinal);
    private RenderQueueDocument _document;
    private CancellationTokenSource? _activeCancellation;
    private Task? _workerTask;
    private bool _workerScheduled;
    private string _activeJobId = "";
    private int _disposeRequested;

    public RenderQueueManager(
        string? path = null,
        IRenderJobExecutor? executor = null)
    {
        _path = Path.GetFullPath(path ?? DefaultPath());
        _storageRoot = Path.Combine(
            Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException(
                    "The render queue has no parent directory."),
            "render-queue-data");
        _executor = executor ?? new RenderJobExecutor();
        try
        {
            _document = Load();
            RecoverInterruptedJobs();
            MaintainHistory();
            Save();
            CleanupOrphanedStorage();
        }
        catch (Exception exception)
        {
            _document = new RenderQueueDocument();
            InitializationError =
                $"The local render queue could not be opened: {exception.Message}";
        }
        Kick();
    }

    public event Action? Changed;

    public string? InitializationError { get; }

    public bool Paused
    {
        get
        {
            lock (_gate) return _document.Paused;
        }
    }

    public IReadOnlyList<RenderQueueJobView> Jobs()
    {
        lock (_gate)
        {
            return _document.Jobs
                .Select(ToView)
                .ToList();
        }
    }

    public IReadOnlySet<string> ActiveOutputPaths()
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        lock (_gate)
        {
            return _document.Jobs
                .Where((job) => !RenderQueueStatus.IsTerminal(job.Status))
                .Select((job) => job.Summary.Output.OutputPath)
                .Where((path) => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .ToHashSet(comparer);
        }
    }

    public string? LastRoute(string projectId)
    {
        lock (_gate)
        {
            return _document.LastRouteByProject.TryGetValue(
                projectId,
                out var route)
                ? route
                : null;
        }
    }

    public void RememberRoute(string projectId, string structureEntryId)
    {
        RequireAvailable();
        lock (_gate)
        {
            _document.LastRouteByProject[projectId] = structureEntryId;
            Save();
        }
        NotifyChanged();
    }

    public IReadOnlyList<RenderQueueJobView> EnqueueBatch(
        IReadOnlyList<RenderJobSnapshot> snapshots)
    {
        RequireAvailable();
        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException(
                "A render batch requires at least one child job.");
        }
        foreach (var snapshot in snapshots)
        {
            snapshot.Validate();
            RenderSnapshotStore.RequireContainedBatchRoot(
                snapshot.FrameStore.BatchRootPath,
                _storageRoot);
            RenderOutputPathSecurity.RequireOutputTarget(snapshot.Output);
        }
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var newPaths = snapshots.Select((snapshot) =>
                Path.GetFullPath(snapshot.Output.OutputPath))
            .ToList();
        if (newPaths.Distinct(comparer).Count() != newPaths.Count)
        {
            throw new InvalidOperationException(
                "A render batch contains duplicate output paths.");
        }

        List<RenderQueueJob> created;
        lock (_gate)
        {
            var activePaths = _document.Jobs
                .Where((job) => !RenderQueueStatus.IsTerminal(job.Status))
                .Select((job) => Path.GetFullPath(
                    job.Summary.Output.OutputPath))
                .ToHashSet(comparer);
            if (newPaths.Any(activePaths.Contains))
            {
                throw new InvalidOperationException(
                    "Another queued render already owns one of these output paths.");
            }
            var now = DateTimeOffset.UtcNow.ToString("O");
            var batchId = Guid.NewGuid().ToString();
            created = snapshots.Select((snapshot) => new RenderQueueJob
            {
                Id = Guid.NewGuid().ToString(),
                BatchId = batchId,
                CreatedAt = now,
                UpdatedAt = now,
                Status = RenderQueueStatus.Pending,
                Progress = new RenderQueueProgress(
                    0,
                    snapshot.FrameStore.TotalFrames,
                    "Pending"),
                Snapshot = snapshot,
                Summary = Summary(snapshot),
            }).ToList();
            _document.Jobs.AddRange(created);
            Save();
        }
        NotifyChanged();
        Kick();
        return created.Select(ToView).ToList();
    }

    public IReadOnlyList<RenderQueueJobView> EnqueuePreparingBatch(
        IReadOnlyList<RenderJobSummary> summaries,
        Func<
            string,
            IProgress<RenderSnapshotFreezeProgress>,
            CancellationToken,
            Task<IReadOnlyList<RenderJobSnapshot>>> freeze)
    {
        RequireAvailable();
        if (summaries.Count == 0)
        {
            throw new InvalidOperationException(
                "A render batch requires at least one child job.");
        }
        ValidateSummaries(summaries);
        var newPaths = summaries
            .Select((summary) => Path.GetFullPath(
                summary.Output.OutputPath))
            .ToList();
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        if (newPaths.Distinct(comparer).Count() != newPaths.Count)
        {
            throw new InvalidOperationException(
                "A render batch contains duplicate output paths.");
        }

        List<RenderQueueJob> created;
        string batchId;
        string batchRoot;
        CancellationTokenSource preparationCancellation;
        lock (_gate)
        {
            var activePaths = _document.Jobs
                .Where((job) =>
                    !RenderQueueStatus.IsTerminal(job.Status))
                .Select((job) => Path.GetFullPath(
                    job.Summary.Output.OutputPath))
                .ToHashSet(comparer);
            if (newPaths.Any(activePaths.Contains))
            {
                throw new InvalidOperationException(
                    "Another queued render already owns one of these output paths.");
            }
            var now = DateTimeOffset.UtcNow.ToString("O");
            batchId = Guid.NewGuid().ToString();
            batchRoot = RenderSnapshotStore.BatchRoot(
                _storageRoot,
                batchId);
            created = summaries.Select((summary) =>
                new RenderQueueJob
                {
                    Id = Guid.NewGuid().ToString(),
                    BatchId = batchId,
                    CreatedAt = now,
                    UpdatedAt = now,
                    StartedAt = now,
                    Status = RenderQueueStatus.Preparing,
                    Progress = new RenderQueueProgress(
                        0,
                        summary.TotalFrames,
                        "Waiting for snapshot"),
                    Summary = summary,
                }).ToList();
            _document.Jobs.AddRange(created);
            preparationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    _shutdown.Token);
            _preparationCancellations.Add(
                batchId,
                preparationCancellation);
            Save();
        }
        NotifyChanged();
        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var task = PrepareBatchAsync(
            start.Task,
            batchId,
            batchRoot,
            created.Select((job) => job.Id).ToArray(),
            freeze,
            preparationCancellation);
        lock (_gate)
        {
            _preparationTasks[batchId] = task;
        }
        start.SetResult();
        return created.Select(ToView).ToList();
    }

    private async Task PrepareBatchAsync(
        Task start,
        string batchId,
        string batchRoot,
        IReadOnlyList<string> jobIds,
        Func<
            string,
            IProgress<RenderSnapshotFreezeProgress>,
            CancellationToken,
            Task<IReadOnlyList<RenderJobSnapshot>>> freeze,
        CancellationTokenSource preparationCancellation)
    {
        await start;
        try
        {
            var progress = new SynchronousProgress<
                RenderSnapshotFreezeProgress>((value) =>
                    UpdatePreparationProgress(batchId, value));
            var snapshots = await freeze(
                batchRoot,
                progress,
                preparationCancellation.Token);
            preparationCancellation.Token
                .ThrowIfCancellationRequested();
            CompletePreparation(
                jobIds,
                snapshots);
        }
        catch (OperationCanceledException)
        {
            if (!_shutdown.IsCancellationRequested)
            {
                FinishPreparation(
                    batchId,
                    RenderQueueStatus.Canceled,
                    null);
            }
        }
        catch (Exception exception)
        {
            FinishPreparation(
                batchId,
                RenderQueueStatus.Failed,
                exception.Message);
        }
        finally
        {
            lock (_gate)
            {
                _preparationCancellations.Remove(batchId);
                _preparationTasks.Remove(batchId);
            }
            preparationCancellation.Dispose();
            CleanupOrphanedStorage();
        }
    }

    private void CompletePreparation(
        IReadOnlyList<string> jobIds,
        IReadOnlyList<RenderJobSnapshot> snapshots)
    {
        if (snapshots.Count != jobIds.Count)
        {
            throw new InvalidOperationException(
                "Render snapshot preparation returned an incomplete batch.");
        }
        foreach (var snapshot in snapshots)
        {
            snapshot.Validate();
            RenderSnapshotStore.RequireContainedBatchRoot(
                snapshot.FrameStore.BatchRootPath,
                _storageRoot);
            RenderOutputPathSecurity.RequireOutputTarget(
                snapshot.Output);
        }

        lock (_gate)
        {
            var jobs = jobIds.Select((jobId) =>
                    _document.Jobs.SingleOrDefault((candidate) =>
                        candidate.Id.Equals(
                            jobId,
                            StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        "A preparing render child is unavailable."))
                .ToList();
            if (jobs.Any((job) =>
                    job.Status != RenderQueueStatus.Preparing
                    || job.Snapshot is not null
                    || job.CancelRequested))
            {
                throw new OperationCanceledException(
                    "Render snapshot preparation was canceled.");
            }
            foreach (var job in jobs)
            {
                var snapshot = snapshots.SingleOrDefault((candidate) =>
                    candidate.RequestedAppearance.Equals(
                        job.Summary.Appearance,
                        StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        "Render snapshot preparation returned the wrong appearance.");
                if (!snapshot.Output.OutputPath.Equals(
                        job.Summary.Output.OutputPath,
                        PathComparison())
                    || snapshot.FrameStore.TotalFrames
                        != job.Summary.TotalFrames)
                {
                    throw new InvalidOperationException(
                        "Render snapshot preparation changed its queued output.");
                }
                job.Snapshot = snapshot;
                job.Status = RenderQueueStatus.Pending;
                job.StartedAt = null;
                job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
                job.Progress = new RenderQueueProgress(
                    0,
                    job.Summary.TotalFrames,
                    "Pending");
            }
            Save();
        }
        NotifyChanged();
        Kick();
    }

    private void UpdatePreparationProgress(
        string batchId,
        RenderSnapshotFreezeProgress value)
    {
        lock (_gate)
        {
            var job = _document.Jobs.SingleOrDefault((candidate) =>
                candidate.BatchId.Equals(
                    batchId,
                    StringComparison.Ordinal)
                && candidate.Summary.Appearance.Equals(
                    value.Appearance,
                    StringComparison.Ordinal)
                && candidate.Status
                    == RenderQueueStatus.Preparing
                && candidate.Snapshot is null);
            if (job is null) return;
            var total = Math.Max(0, value.Total);
            var current = Math.Clamp(
                value.Current,
                0,
                total);
            if (total != job.Progress.Total
                || current < job.Progress.Current)
            {
                return;
            }
            job.Progress = new RenderQueueProgress(
                current,
                total,
                $"Freezing {DisplayAppearance(value.Appearance)}"
                + (string.IsNullOrWhiteSpace(value.ScreenName)
                    ? ""
                    : $" · {value.ScreenName}"));
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        }
        NotifyChanged();
    }

    private void FinishPreparation(
        string batchId,
        string status,
        string? error)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            foreach (var job in _document.Jobs.Where((candidate) =>
                         candidate.BatchId.Equals(
                             batchId,
                             StringComparison.Ordinal)
                         && candidate.Status
                             == RenderQueueStatus.Preparing
                         && candidate.Snapshot is null))
            {
                job.Status = status;
                job.Error = error;
                job.CancelRequested = false;
                job.CompletedAt = now;
                job.UpdatedAt = now;
                job.Progress = job.Progress with
                {
                    Phase = status == RenderQueueStatus.Canceled
                        ? "Canceled"
                        : "Snapshot error",
                };
            }
            MaintainHistory();
            Save();
        }
        NotifyChanged();
    }

    public bool Cancel(string jobId)
    {
        RequireAvailable();
        var changed = false;
        CancellationTokenSource? preparationCancellation = null;
        lock (_gate)
        {
            var job = _document.Jobs.SingleOrDefault((candidate) =>
                candidate.Id.Equals(jobId, StringComparison.Ordinal));
            if (job is null || RenderQueueStatus.IsTerminal(job.Status))
            {
                return false;
            }
            job.CancelRequested = true;
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
            if (job.Status == RenderQueueStatus.Preparing
                && job.Snapshot is null
                && _preparationCancellations.TryGetValue(
                    job.BatchId,
                    out preparationCancellation))
            {
                foreach (var child in _document.Jobs.Where((candidate) =>
                             candidate.BatchId.Equals(
                                 job.BatchId,
                                 StringComparison.Ordinal)
                             && candidate.Status
                                 == RenderQueueStatus.Preparing
                             && candidate.Snapshot is null))
                {
                    child.CancelRequested = true;
                    child.UpdatedAt = job.UpdatedAt;
                    child.Progress = child.Progress with
                    {
                        Phase = "Canceling snapshot",
                    };
                }
            }
            else if (job.Status == RenderQueueStatus.Pending)
            {
                job.Status = RenderQueueStatus.Canceled;
                job.CompletedAt = job.UpdatedAt;
                job.Progress = job.Progress with { Phase = "Canceled" };
                MaintainHistory();
                Save();
            }
            else
            {
                job.Progress = job.Progress with { Phase = "Canceling" };
                if (job.Id.Equals(_activeJobId, StringComparison.Ordinal))
                {
                    _activeCancellation?.Cancel();
                }
            }
            changed = true;
        }
        preparationCancellation?.Cancel();
        if (changed) NotifyChanged();
        return changed;
    }

    public bool Retry(string jobId)
    {
        RequireAvailable();
        RenderJobSnapshot? snapshot;
        lock (_gate)
        {
            var job = _document.Jobs.SingleOrDefault((candidate) =>
                candidate.Id.Equals(jobId, StringComparison.Ordinal));
            if (job?.Snapshot is null
                || job.Status is not RenderQueueStatus.Failed
                    and not RenderQueueStatus.Canceled)
            {
                return false;
            }
            snapshot = job.Snapshot;
        }
        EnqueueBatch([snapshot]);
        return true;
    }

    public bool Remove(string jobId)
    {
        RequireAvailable();
        lock (_gate)
        {
            var index = _document.Jobs.FindIndex((candidate) =>
                candidate.Id.Equals(jobId, StringComparison.Ordinal));
            if (index < 0
                || !RenderQueueStatus.IsTerminal(
                    _document.Jobs[index].Status))
            {
                return false;
            }
            _document.Jobs.RemoveAt(index);
            Save();
        }
        CleanupOrphanedStorage();
        NotifyChanged();
        return true;
    }

    public int ClearFinished()
    {
        RequireAvailable();
        int removed;
        lock (_gate)
        {
            removed = _document.Jobs.RemoveAll((job) =>
                RenderQueueStatus.IsTerminal(job.Status));
            if (removed > 0) Save();
        }
        if (removed > 0) CleanupOrphanedStorage();
        if (removed > 0) NotifyChanged();
        return removed;
    }

    public void SetPaused(bool value)
    {
        RequireAvailable();
        lock (_gate)
        {
            if (_document.Paused == value) return;
            _document.Paused = value;
            Save();
        }
        NotifyChanged();
        if (!value) Kick();
    }

    private void Kick()
    {
        if (_shutdown.IsCancellationRequested
            || !string.IsNullOrWhiteSpace(InitializationError))
        {
            return;
        }
        lock (_gate)
        {
            if (_workerScheduled
                || _document.Paused
                || !_document.Jobs.Any((job) =>
                    job.Status == RenderQueueStatus.Pending))
            {
                return;
            }
            _workerScheduled = true;
            _workerTask = Task.Run(ProcessQueueAsync);
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                RenderQueueJob? job;
                lock (_gate)
                {
                    job = _document.Paused
                        ? null
                        : _document.Jobs.FirstOrDefault((candidate) =>
                            candidate.Status == RenderQueueStatus.Pending);
                    if (job is null)
                    {
                        _workerScheduled = false;
                        return;
                    }
                    var now = DateTimeOffset.UtcNow.ToString("O");
                    job.Status = RenderQueueStatus.Preparing;
                    job.StartedAt = now;
                    job.UpdatedAt = now;
                    job.CancelRequested = false;
                    job.Progress = new RenderQueueProgress(
                        0,
                        job.Summary.TotalFrames,
                        "Preparing");
                    _activeJobId = job.Id;
                    _activeCancellation = CancellationTokenSource
                        .CreateLinkedTokenSource(_shutdown.Token);
                    Save();
                }
                NotifyChanged();
                var progress = new Progress<RenderQueueExecutionProgress>(
                    (value) => UpdateProgress(job.Id, value));
                try
                {
                    var snapshot = job.Snapshot
                        ?? throw new InvalidOperationException(
                            "The pending render job has no snapshot.");
                    await _executor.ExecuteAsync(
                        snapshot,
                        progress,
                        _activeCancellation.Token);
                    Finish(job.Id, RenderQueueStatus.Completed, null);
                }
                catch (OperationCanceledException)
                {
                    if (_shutdown.IsCancellationRequested)
                    {
                        RecoverForShutdown(job.Id);
                        return;
                    }
                    Finish(job.Id, RenderQueueStatus.Canceled, null);
                }
                catch (Exception exception)
                {
                    Finish(
                        job.Id,
                        RenderQueueStatus.Failed,
                        exception.Message);
                }
                finally
                {
                    lock (_gate)
                    {
                        _activeCancellation?.Dispose();
                        _activeCancellation = null;
                        _activeJobId = "";
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Closing the app deliberately returns the active job to Pending.
        }
        finally
        {
            lock (_gate)
            {
                _workerScheduled = false;
            }
        }
    }

    private void UpdateProgress(
        string jobId,
        RenderQueueExecutionProgress value)
    {
        lock (_gate)
        {
            var job = _document.Jobs.SingleOrDefault((candidate) =>
                candidate.Id.Equals(jobId, StringComparison.Ordinal));
            if (job is null || RenderQueueStatus.IsTerminal(job.Status)) return;
            var currentRank = ExecutionStatusRank(job.Status);
            var nextRank = ExecutionStatusRank(value.Status);
            if (nextRank < currentRank
                || (nextRank == currentRank
                    && value.Current < job.Progress.Current))
            {
                return;
            }
            job.Status = value.Status;
            job.Progress = new RenderQueueProgress(
                Math.Max(0, value.Current),
                Math.Max(0, value.Total),
                value.Phase);
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        }
        NotifyChanged();
    }

    private static int ExecutionStatusRank(string status) =>
        status switch
        {
            RenderQueueStatus.Pending => 0,
            RenderQueueStatus.Preparing => 1,
            RenderQueueStatus.Rendering => 2,
            RenderQueueStatus.Encoding => 3,
            _ => 4,
        };

    private void Finish(string jobId, string status, string? error)
    {
        lock (_gate)
        {
            var job = _document.Jobs.Single((candidate) =>
                candidate.Id.Equals(jobId, StringComparison.Ordinal));
            var now = DateTimeOffset.UtcNow.ToString("O");
            job.Status = status;
            job.Error = error;
            job.CompletedAt = now;
            job.UpdatedAt = now;
            job.Progress = new RenderQueueProgress(
                status == RenderQueueStatus.Completed
                    ? job.Summary.TotalFrames
                    : job.Progress.Current,
                job.Summary.TotalFrames,
                status switch
                {
                    RenderQueueStatus.Completed => "Completed",
                    RenderQueueStatus.Canceled => "Canceled",
                    _ => "Error",
                });
            job.CancelRequested = false;
            MaintainHistory();
            Save();
        }
        CleanupOrphanedStorage();
        NotifyChanged();
    }

    private void RecoverForShutdown(string jobId)
    {
        lock (_gate)
        {
            var job = _document.Jobs.SingleOrDefault((candidate) =>
                candidate.Id.Equals(jobId, StringComparison.Ordinal));
            if (job is null || RenderQueueStatus.IsTerminal(job.Status)) return;
            job.Status = RenderQueueStatus.Pending;
            job.StartedAt = null;
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
            job.CancelRequested = false;
            job.Progress = new RenderQueueProgress(
                0,
                job.Summary.TotalFrames,
                "Recovered after closing the app");
            Save();
        }
        NotifyChanged();
    }

    private void RecoverInterruptedJobs()
    {
        foreach (var job in _document.Jobs.Where((candidate) =>
            RenderQueueStatus.IsActive(candidate.Status)))
        {
            var snapshotAvailable = job.Snapshot is not null;
            job.Status = snapshotAvailable
                ? RenderQueueStatus.Pending
                : RenderQueueStatus.Failed;
            job.StartedAt = snapshotAvailable
                ? null
                : job.StartedAt;
            job.CompletedAt = snapshotAvailable
                ? null
                : DateTimeOffset.UtcNow.ToString("O");
            job.Error = snapshotAvailable
                ? null
                : "Snapshot preparation was interrupted before the render became immutable.";
            job.CancelRequested = false;
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
            job.Progress = new RenderQueueProgress(
                0,
                job.Summary.TotalFrames,
                snapshotAvailable
                    ? "Recovered after closing the app"
                    : "Snapshot interrupted");
        }
    }

    private void MaintainHistory()
    {
        var terminal = _document.Jobs
            .Where((job) => RenderQueueStatus.IsTerminal(job.Status))
            .ToList();
        if (terminal.Count > MaximumTerminalJobs)
        {
            var remove = terminal
                .Take(terminal.Count - MaximumTerminalJobs)
                .Select((job) => job.Id)
                .ToHashSet(StringComparer.Ordinal);
            _document.Jobs.RemoveAll((job) => remove.Contains(job.Id));
        }
        foreach (var job in _document.Jobs.Where((candidate) =>
            candidate.Status == RenderQueueStatus.Completed))
        {
            job.Snapshot = null;
        }
        var retryable = _document.Jobs
            .Where((job) =>
                job.Snapshot is not null
                && job.Status is RenderQueueStatus.Failed
                    or RenderQueueStatus.Canceled)
            .ToList();
        foreach (var job in retryable.Take(
            Math.Max(0, retryable.Count - MaximumRetrySnapshots)))
        {
            job.Snapshot = null;
        }
    }

    private RenderQueueDocument Load()
    {
        if (!File.Exists(_path)) return new RenderQueueDocument();
        var document = JsonSerializer.Deserialize<RenderQueueDocument>(
            File.ReadAllText(_path))
            ?? throw new InvalidOperationException(
                "The render queue document is empty.");
        if (document.Schema != "mockups_render_queue"
            || document.Version != 1
            || document.Jobs is null
            || document.LastRouteByProject is null)
        {
            throw new InvalidOperationException(
                "The render queue document uses an unsupported contract.");
        }
        foreach (var job in document.Jobs)
        {
            if (string.IsNullOrWhiteSpace(job.Id)
                || string.IsNullOrWhiteSpace(job.BatchId)
                || !RenderQueueStatus.IsKnown(job.Status)
                || job.Summary.TotalFrames <= 0
                || (job.Status == RenderQueueStatus.Pending
                    && job.Snapshot is null)
                || (job.Status is RenderQueueStatus.Rendering
                        or RenderQueueStatus.Encoding
                    && job.Snapshot is null)
                || (job.Snapshot is not null
                    && job.Snapshot.Schema
                        != RenderJobSnapshot.CurrentSchema))
            {
                throw new InvalidOperationException(
                    "The render queue contains an incomplete job.");
            }
            if (job.Snapshot is not null)
            {
                job.Snapshot.Validate();
                RenderSnapshotStore.RequireContainedBatchRoot(
                    job.Snapshot.FrameStore.BatchRootPath,
                    _storageRoot);
            }
        }
        return document;
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException(
                "The render queue has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(
                    _document,
                    new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void RequireAvailable()
    {
        if (!string.IsNullOrWhiteSpace(InitializationError))
        {
            throw new InvalidOperationException(InitializationError);
        }
    }

    private static void ValidateSummaries(
        IReadOnlyList<RenderJobSummary> summaries)
    {
        if (summaries.Select((summary) => summary.Appearance)
                .Distinct(StringComparer.Ordinal)
                .Count() != summaries.Count)
        {
            throw new InvalidOperationException(
                "A render batch contains duplicate appearances.");
        }
        foreach (var summary in summaries)
        {
            if (string.IsNullOrWhiteSpace(
                    summary.Context.ProjectId)
                || string.IsNullOrWhiteSpace(
                    summary.Context.ShotId)
                || string.IsNullOrWhiteSpace(
                    summary.Context.ActorId)
                || string.IsNullOrWhiteSpace(
                    summary.DeviceName)
                || string.IsNullOrWhiteSpace(
                    summary.ThemeName)
                || summary.Appearance is not RenderQueueAppearance.Light
                    and not RenderQueueAppearance.Dark
                || summary.TotalFrames <= 0
                || summary.Output.Appearance
                    != summary.Appearance)
            {
                throw new InvalidOperationException(
                    "A preparing render summary is incomplete.");
            }
            _ = RenderOutputModes.Require(
                summary.Output.OutputModeId);
            RenderOutputPathSecurity.RequireOutputTarget(
                summary.Output);
        }
    }

    private void CleanupOrphanedStorage()
    {
        try
        {
            if (!Directory.Exists(_storageRoot)) return;
            HashSet<string> referenced;
            HashSet<string> preparing;
            lock (_gate)
            {
                referenced = _document.Jobs
                    .Where((job) => job.Snapshot is not null)
                    .Select((job) => Path.GetFullPath(
                        job.Snapshot!.FrameStore.BatchRootPath))
                    .ToHashSet(PathComparer());
                preparing = _preparationCancellations.Keys
                    .Select((batchId) => RenderSnapshotStore.BatchRoot(
                        _storageRoot,
                        batchId))
                    .ToHashSet(PathComparer());
            }
            foreach (var directory in Directory.GetDirectories(
                         _storageRoot,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                var fullPath = Path.GetFullPath(directory);
                if (referenced.Contains(fullPath)
                    || preparing.Contains(fullPath)
                    || !Guid.TryParse(
                        Path.GetFileName(fullPath),
                        out _))
                {
                    continue;
                }
                try
                {
                    RenderSnapshotStore.DeleteBatchRoot(
                        fullPath,
                        _storageRoot);
                }
                catch
                {
                    // A later queue cleanup can retry a busy local store.
                }
            }
        }
        catch
        {
            // Queue execution remains available if stale-file cleanup fails.
        }
    }

    private static RenderJobSummary Summary(RenderJobSnapshot snapshot) =>
        new(
            snapshot.Context,
            snapshot.DeviceName,
            snapshot.ThemeName,
            snapshot.RequestedAppearance,
            snapshot.FrameStore.TotalFrames,
            snapshot.Output);

    private static RenderQueueJobView ToView(RenderQueueJob job) =>
        new(
            job.Id,
            job.BatchId,
            job.CreatedAt,
            job.Status,
            job.Progress,
            job.Snapshot is not null,
            job.Summary,
            job.Error);

    private static string DefaultPath()
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
        return Path.Combine(root, "MOCKUPS", "render-queue.json");
    }

    private static string DisplayAppearance(string appearance) =>
        appearance == RenderQueueAppearance.Light
            ? "Light"
            : "Dark";

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer() =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private void NotifyChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch
        {
            // UI observers cannot break queue persistence or execution.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0) return;
        _shutdown.Cancel();
        Task[] tasks;
        lock (_gate)
        {
            _activeCancellation?.Cancel();
            foreach (var cancellation in
                     _preparationCancellations.Values)
            {
                cancellation.Cancel();
            }
            tasks = _preparationTasks.Values
                .Concat(_workerTask is null
                    ? []
                    : [_workerTask])
                .ToArray();
        }
        if (tasks.Length == 0)
        {
            _executor.Dispose();
            _shutdown.Dispose();
            return;
        }
        _ = Task.WhenAll(tasks).ContinueWith(
            _ =>
            {
                _executor.Dispose();
                _shutdown.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed class SynchronousProgress<T>(
        Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
