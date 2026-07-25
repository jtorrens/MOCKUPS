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
    private readonly IRenderJobExecutor _executor;
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
        _path = path ?? DefaultPath();
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
                    snapshot.Frames.Count,
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

    public bool Cancel(string jobId)
    {
        RequireAvailable();
        var changed = false;
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
            if (job.Status == RenderQueueStatus.Pending)
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
            job.Status = value.Status;
            job.Progress = new RenderQueueProgress(
                Math.Max(0, value.Current),
                Math.Max(0, value.Total),
                value.Phase);
            job.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        }
        NotifyChanged();
    }

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
            job.Status = RenderQueueStatus.Pending;
            job.StartedAt = null;
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
                || (job.Snapshot is not null
                    && job.Snapshot.Schema
                        != RenderJobSnapshot.CurrentSchema))
            {
                throw new InvalidOperationException(
                    "The render queue contains an incomplete job.");
            }
            job.Snapshot?.Validate();
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

    private static RenderJobSummary Summary(RenderJobSnapshot snapshot) =>
        new(
            snapshot.Context,
            snapshot.DeviceName,
            snapshot.ThemeName,
            snapshot.RequestedAppearance,
            snapshot.Frames.Count,
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
        lock (_gate)
        {
            _activeCancellation?.Cancel();
        }
        var worker = _workerTask;
        var stopped = worker is null;
        try
        {
            stopped = worker?.Wait(TimeSpan.FromSeconds(3)) ?? true;
        }
        catch
        {
            // Process shutdown will reclaim remaining child resources.
        }
        if (stopped)
        {
            _executor.Dispose();
            _shutdown.Dispose();
            return;
        }
        _ = worker!.ContinueWith(
            _ =>
            {
                _executor.Dispose();
                _shutdown.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
