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
    private readonly object _gate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly string _path;
    private readonly IRenderJobExecutor _executor;
    private readonly HashSet<string> _launchedJobIds = new(StringComparer.Ordinal);
    private RenderQueueDocument _document;
    private CancellationTokenSource? _activeCancellation;
    private IRenderJobPreparer? _launchedPreparer;
    private Task? _workerTask;
    private bool _workerScheduled;
    private string _activeJobId = "";
    private int _disposeRequested;

    public RenderQueueManager(string? path = null, IRenderJobExecutor? executor = null)
    {
        _path = Path.GetFullPath(path ?? DefaultPath());
        _executor = executor ?? new RenderJobExecutor();
        try
        {
            _document = Load();
            RecoverInterruptedJobs();
            MaintainHistory();
            Save();
        }
        catch (Exception exception)
        {
            _document = new RenderQueueDocument();
            InitializationError =
                $"The local render queue could not be opened: {exception.Message}";
        }
    }

    public event Action? Changed;
    public string? InitializationError { get; }

    public bool Paused
    {
        get { lock (_gate) return _document.Paused; }
    }

    public bool HasLaunchedBatch
    {
        get { lock (_gate) return _launchedJobIds.Count > 0; }
    }

    public bool CanRenderPending
    {
        get
        {
            lock (_gate)
            {
                return !_document.Paused
                    && _launchedJobIds.Count == 0
                    && _document.Jobs.Any((job) =>
                        job.Status == RenderQueueStatus.Pending);
            }
        }
    }

    public IReadOnlyList<RenderQueueJobView> Jobs()
    {
        lock (_gate) return _document.Jobs.Select(ToView).ToList();
    }

    public IReadOnlySet<string> ActiveOutputPaths()
    {
        lock (_gate)
        {
            return _document.Jobs
                .Where((job) => !RenderQueueStatus.IsTerminal(job.Status))
                .Select((job) => job.Summary.Output.OutputPath)
                .Where((path) => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .ToHashSet(PathComparer());
        }
    }

    public string? LastRoute(string projectId)
    {
        lock (_gate)
        {
            return _document.LastRouteByProject.TryGetValue(projectId, out var route)
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
        IReadOnlyList<RenderJobPlan> plans,
        IReadOnlyList<RenderJobSummary> summaries)
    {
        RequireAvailable();
        if (plans.Count == 0 || plans.Count != summaries.Count)
        {
            throw new InvalidOperationException(
                "A render batch requires one summary for each live plan.");
        }
        ValidatePlans(plans, summaries);
        var comparer = PathComparer();
        var newPaths = plans
            .Select((plan) => Path.GetFullPath(plan.Output.OutputPath))
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
                .Select((job) => Path.GetFullPath(job.Summary.Output.OutputPath))
                .ToHashSet(comparer);
            if (newPaths.Any(activePaths.Contains))
            {
                throw new InvalidOperationException(
                    "Another queued render already owns one of these output paths.");
            }
            var now = DateTimeOffset.UtcNow.ToString("O");
            var batchId = Guid.NewGuid().ToString();
            created = plans.Select((plan, index) => new RenderQueueJob
            {
                Id = Guid.NewGuid().ToString(),
                BatchId = batchId,
                CreatedAt = now,
                UpdatedAt = now,
                Status = RenderQueueStatus.Pending,
                Progress = new RenderQueueProgress(
                    0,
                    summaries[index].TotalFrames,
                    "Pending"),
                Plan = plan,
                Summary = summaries[index],
            }).ToList();
            _document.Jobs.AddRange(created);
            Save();
        }
        NotifyChanged();
        return created.Select(ToView).ToList();
    }

    public int RenderPending(IRenderJobPreparer preparer)
    {
        ArgumentNullException.ThrowIfNull(preparer);
        RequireAvailable();
        int count;
        lock (_gate)
        {
            if (_document.Paused || _launchedJobIds.Count > 0) return 0;
            var pendingIds = _document.Jobs
                .Where((job) => job.Status == RenderQueueStatus.Pending)
                .Select((job) => job.Id)
                .ToList();
            count = pendingIds.Count;
            foreach (var jobId in pendingIds) _launchedJobIds.Add(jobId);
            if (count > 0) _launchedPreparer = preparer;
        }
        if (count == 0) return 0;
        NotifyChanged();
        Kick();
        return count;
    }

    public bool Cancel(string jobId)
    {
        RequireAvailable();
        lock (_gate)
        {
            var job = _document.Jobs.SingleOrDefault((candidate) =>
                candidate.Id.Equals(jobId, StringComparison.Ordinal));
            if (job is null || RenderQueueStatus.IsTerminal(job.Status)) return false;
            job.CancelRequested = true;
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
            if (job.Status == RenderQueueStatus.Pending)
            {
                _launchedJobIds.Remove(job.Id);
                job.Status = RenderQueueStatus.Canceled;
                job.CompletedAt = job.UpdatedAt;
                job.Progress = job.Progress with { Phase = "Canceled" };
                CompleteLaunchedBatchIfEmpty();
                MaintainHistory();
            }
            else
            {
                job.Progress = job.Progress with { Phase = "Canceling" };
                if (job.Id.Equals(_activeJobId, StringComparison.Ordinal))
                {
                    _activeCancellation?.Cancel();
                }
            }
            Save();
        }
        NotifyChanged();
        return true;
    }

    public bool Retry(string jobId)
    {
        RequireAvailable();
        RenderJobPlan plan;
        RenderJobSummary summary;
        lock (_gate)
        {
            var job = _document.Jobs.SingleOrDefault((candidate) =>
                candidate.Id.Equals(jobId, StringComparison.Ordinal));
            if (job is null
                || job.Status is not RenderQueueStatus.Failed
                    and not RenderQueueStatus.Canceled)
            {
                return false;
            }
            plan = job.Plan;
            summary = job.Summary;
        }
        EnqueueBatch([plan], [summary]);
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
                || !RenderQueueStatus.IsTerminal(_document.Jobs[index].Status))
            {
                return false;
            }
            _document.Jobs.RemoveAt(index);
            _launchedJobIds.Remove(jobId);
            CompleteLaunchedBatchIfEmpty();
            Save();
        }
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
            _launchedJobIds.RemoveWhere((jobId) =>
                !_document.Jobs.Any((job) => job.Id == jobId));
            CompleteLaunchedBatchIfEmpty();
            if (removed > 0) Save();
        }
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
        if (!value && HasLaunchedBatch) Kick();
    }

    private void Kick()
    {
        if (_shutdown.IsCancellationRequested
            || !string.IsNullOrWhiteSpace(InitializationError)) return;
        lock (_gate)
        {
            if (_workerScheduled
                || _document.Paused
                || _launchedPreparer is null
                || !_document.Jobs.Any((job) =>
                    job.Status == RenderQueueStatus.Pending
                    && _launchedJobIds.Contains(job.Id))) return;
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
                IRenderJobPreparer? preparer;
                CancellationTokenSource cancellation;
                lock (_gate)
                {
                    if (_document.Paused)
                    {
                        _workerScheduled = false;
                        return;
                    }
                    _launchedJobIds.RemoveWhere((jobId) =>
                        !_document.Jobs.Any((candidate) =>
                            candidate.Id == jobId
                            && candidate.Status == RenderQueueStatus.Pending));
                    job = _document.Jobs.FirstOrDefault((candidate) =>
                        candidate.Status == RenderQueueStatus.Pending
                        && _launchedJobIds.Contains(candidate.Id));
                    preparer = _launchedPreparer;
                    if (job is null || preparer is null)
                    {
                        _launchedJobIds.Clear();
                        _launchedPreparer = null;
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
                        "Preparing current render");
                    _activeJobId = job.Id;
                    cancellation = CancellationTokenSource
                        .CreateLinkedTokenSource(_shutdown.Token);
                    _activeCancellation = cancellation;
                    Save();
                }
                NotifyChanged();

                var temporaryRoot = CreateTemporaryRoot();
                try
                {
                    var preparationProgress =
                        new SynchronousProgress<RenderSnapshotFreezeProgress>(
                            (value) => UpdatePreparationProgress(job.Id, value));
                    var snapshot = await preparer.PrepareAsync(
                        job.Plan,
                        temporaryRoot,
                        preparationProgress,
                        cancellation.Token);
                    snapshot.Validate();
                    ValidatePreparedSnapshot(job.Plan, snapshot);
                    UpdatePreparedSummary(job.Id, snapshot);

                    var executionProgress = new Progress<RenderQueueExecutionProgress>(
                        (value) => UpdateProgress(job.Id, value));
                    await _executor.ExecuteAsync(
                        snapshot,
                        executionProgress,
                        cancellation.Token);
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
                    Finish(job.Id, RenderQueueStatus.Failed, exception.Message);
                }
                finally
                {
                    DeleteTemporaryRoot(temporaryRoot);
                    lock (_gate)
                    {
                        cancellation.Dispose();
                        if (ReferenceEquals(_activeCancellation, cancellation))
                        {
                            _activeCancellation = null;
                            _activeJobId = "";
                        }
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
            lock (_gate) _workerScheduled = false;
        }
    }

    private void UpdatePreparationProgress(
        string jobId,
        RenderSnapshotFreezeProgress value)
    {
        lock (_gate)
        {
            var job = _document.Jobs.SingleOrDefault((candidate) =>
                candidate.Id == jobId
                && candidate.Status == RenderQueueStatus.Preparing);
            if (job is null) return;
            var total = Math.Max(0, value.Total);
            var current = Math.Clamp(value.Current, 0, total);
            if (total == job.Progress.Total && current < job.Progress.Current) return;
            job.Progress = new RenderQueueProgress(
                current,
                total,
                $"Preparing current {DisplayAppearance(value.Appearance)}"
                + (string.IsNullOrWhiteSpace(value.ScreenName)
                    ? ""
                    : $" · {value.ScreenName}"));
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        }
        NotifyChanged();
    }

    private void UpdatePreparedSummary(string jobId, RenderJobSnapshot snapshot)
    {
        lock (_gate)
        {
            var job = _document.Jobs.Single((candidate) => candidate.Id == jobId);
            job.Summary = Summary(snapshot);
            job.Progress = new RenderQueueProgress(
                0,
                snapshot.FrameStore.TotalFrames,
                "Prepared current render");
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
            Save();
        }
        NotifyChanged();
    }

    private void UpdateProgress(string jobId, RenderQueueExecutionProgress value)
    {
        lock (_gate)
        {
            var job = _document.Jobs.SingleOrDefault((candidate) =>
                candidate.Id == jobId);
            if (job is null || RenderQueueStatus.IsTerminal(job.Status)) return;
            var currentRank = ExecutionStatusRank(job.Status);
            var nextRank = ExecutionStatusRank(value.Status);
            if (nextRank < currentRank
                || (nextRank == currentRank
                    && value.Current < job.Progress.Current)) return;
            job.Status = value.Status;
            job.Progress = new RenderQueueProgress(
                Math.Max(0, value.Current),
                Math.Max(0, value.Total),
                value.Phase);
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        }
        NotifyChanged();
    }

    private static int ExecutionStatusRank(string status) => status switch
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
            var job = _document.Jobs.Single((candidate) => candidate.Id == jobId);
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
            _launchedJobIds.Remove(jobId);
            CompleteLaunchedBatchIfEmpty();
            MaintainHistory();
            Save();
        }
        NotifyChanged();
    }

    private void RecoverForShutdown(string jobId)
    {
        lock (_gate)
        {
            var job = _document.Jobs.SingleOrDefault((candidate) =>
                candidate.Id == jobId);
            if (job is null || RenderQueueStatus.IsTerminal(job.Status)) return;
            job.Status = RenderQueueStatus.Pending;
            job.StartedAt = null;
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
            job.CancelRequested = false;
            job.Progress = new RenderQueueProgress(
                0,
                job.Summary.TotalFrames,
                "Recovered after closing the app");
            _launchedJobIds.Remove(jobId);
            CompleteLaunchedBatchIfEmpty();
            Save();
        }
        NotifyChanged();
    }

    private void RecoverInterruptedJobs()
    {
        foreach (var job in _document.Jobs.Where((candidate) =>
            RenderQueueStatus.IsActive(candidate.Status)))
        {
            job.Status = RenderQueueStatus.Pending;
            job.StartedAt = null;
            job.CompletedAt = null;
            job.Error = null;
            job.CancelRequested = false;
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
            job.Progress = new RenderQueueProgress(
                0,
                job.Summary.TotalFrames,
                "Recovered after closing the app");
        }
    }

    private void MaintainHistory()
    {
        var terminal = _document.Jobs
            .Where((job) => RenderQueueStatus.IsTerminal(job.Status))
            .ToList();
        if (terminal.Count <= MaximumTerminalJobs) return;
        var remove = terminal
            .Take(terminal.Count - MaximumTerminalJobs)
            .Select((job) => job.Id)
            .ToHashSet(StringComparer.Ordinal);
        _document.Jobs.RemoveAll((job) => remove.Contains(job.Id));
    }

    private RenderQueueDocument Load()
    {
        if (!File.Exists(_path)) return new RenderQueueDocument();
        var document = JsonSerializer.Deserialize<RenderQueueDocument>(
            File.ReadAllText(_path))
            ?? throw new InvalidOperationException("The render queue document is empty.");
        if (document.Schema != "mockups_render_queue"
            || document.Version != 2
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
                || job.Summary.TotalFrames <= 0)
            {
                throw new InvalidOperationException(
                    "The render queue contains an incomplete job.");
            }
            job.Plan.Validate();
            RenderOutputPathSecurity.RequireOutputTargetContract(
                job.Plan.Output);
            ValidateSummaryContract(job.Summary);
            if (!job.Plan.Output.OutputPath.Equals(
                    job.Summary.Output.OutputPath,
                    PathComparison())
                || job.Plan.RequestedAppearance != job.Summary.Appearance)
            {
                throw new InvalidOperationException(
                    "The render queue job plan and summary disagree.");
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

    private static void ValidatePlans(
        IReadOnlyList<RenderJobPlan> plans,
        IReadOnlyList<RenderJobSummary> summaries)
    {
        if (plans.Select((plan) => plan.RequestedAppearance)
                .Distinct(StringComparer.Ordinal).Count() != plans.Count)
        {
            throw new InvalidOperationException(
                "A render batch contains duplicate appearances.");
        }
        for (var index = 0; index < plans.Count; index++)
        {
            var plan = plans[index];
            var summary = summaries[index];
            plan.Validate();
            RenderOutputPathSecurity.RequireOutputTarget(plan.Output);
            ValidateLiveSummary(summary);
            if (plan.ProjectId != summary.Context.ProjectId
                || plan.ShotId != summary.Context.ShotId
                || plan.RequestedAppearance != summary.Appearance
                || !plan.Output.OutputPath.Equals(
                    summary.Output.OutputPath,
                    PathComparison()))
            {
                throw new InvalidOperationException(
                    "A live render plan and its display summary disagree.");
            }
        }
    }

    private static void ValidateSummaryContract(RenderJobSummary summary)
    {
        if (string.IsNullOrWhiteSpace(summary.Context.ProjectId)
            || string.IsNullOrWhiteSpace(summary.Context.ShotId)
            || string.IsNullOrWhiteSpace(summary.Context.ActorId)
            || string.IsNullOrWhiteSpace(summary.DeviceName)
            || string.IsNullOrWhiteSpace(summary.ThemeName)
            || summary.Appearance is not RenderQueueAppearance.Light
                and not RenderQueueAppearance.Dark
            || summary.TotalFrames <= 0
            || summary.Output.Appearance != summary.Appearance)
        {
            throw new InvalidOperationException("A render summary is incomplete.");
        }
        _ = RenderOutputModes.Require(summary.Output.OutputModeId);
        RenderOutputPathSecurity.RequireOutputTargetContract(summary.Output);
    }

    private static void ValidateLiveSummary(RenderJobSummary summary)
    {
        ValidateSummaryContract(summary);
        RenderOutputPathSecurity.RequireOutputTarget(summary.Output);
    }

    private static void ValidatePreparedSnapshot(
        RenderJobPlan plan,
        RenderJobSnapshot snapshot)
    {
        if (snapshot.Context.ProjectId != plan.ProjectId
            || snapshot.Context.ShotId != plan.ShotId
            || snapshot.RequestedAppearance != plan.RequestedAppearance
            || snapshot.Output != plan.Output)
        {
            throw new InvalidOperationException(
                "Live render preparation changed the queued identity or output.");
        }
    }

    private void CompleteLaunchedBatchIfEmpty()
    {
        if (_launchedJobIds.Count == 0) _launchedPreparer = null;
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MOCKUPS-render-queue-runtime",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTemporaryRoot(string root)
    {
        try
        {
            var parent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "MOCKUPS-render-queue-runtime"));
            var fullRoot = Path.GetFullPath(root);
            var prefix = parent.EndsWith(Path.DirectorySeparatorChar)
                ? parent
                : parent + Path.DirectorySeparatorChar;
            if (!fullRoot.StartsWith(prefix, PathComparison())) return;
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
        }
        catch
        {
            // A later workstation temp cleanup can retry a busy runtime folder.
        }
    }

    private static RenderJobSummary Summary(RenderJobSnapshot snapshot) => new(
        snapshot.Context,
        snapshot.DeviceName,
        snapshot.ThemeName,
        snapshot.RequestedAppearance,
        snapshot.FrameStore.TotalFrames,
        snapshot.Output);

    private static RenderQueueJobView ToView(RenderQueueJob job) => new(
        job.Id,
        job.BatchId,
        job.CreatedAt,
        job.Status,
        job.Progress,
        true,
        job.Summary,
        job.Error);

    private void RequireAvailable()
    {
        if (!string.IsNullOrWhiteSpace(InitializationError))
        {
            throw new InvalidOperationException(InitializationError);
        }
    }

    private static string DefaultPath()
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
        return Path.Combine(root, "MOCKUPS", "render-queue.json");
    }

    private static string DisplayAppearance(string appearance) =>
        appearance == RenderQueueAppearance.Light ? "Light" : "Dark";

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
        try { Changed?.Invoke(); }
        catch { /* UI observers cannot break queue persistence or execution. */ }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0) return;
        _shutdown.Cancel();
        Task? worker;
        lock (_gate)
        {
            _activeCancellation?.Cancel();
            worker = _workerTask;
        }
        if (worker is null)
        {
            _executor.Dispose();
            _shutdown.Dispose();
            return;
        }
        _ = worker.ContinueWith(
            _ =>
            {
                _executor.Dispose();
                _shutdown.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
