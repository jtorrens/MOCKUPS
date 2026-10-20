using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed class EditorOperationCoordinator : IDisposable
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _stateGate = new();
    private CancellationTokenSource _lifetime = new();
    private bool _stopping;
    private bool _disposed;
    private long _nextActivityId;

    public event Action<EditorOperationActivity>? ActivityChanged;
    public event Action<EditorOperationMeasurement>? OperationMeasured;

    public IDisposable BeginActivity(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var activity = new EditorOperationActivity(Interlocked.Increment(ref _nextActivityId), message, true);
        ActivityChanged?.Invoke(activity);
        return new ActivityScope(this, activity);
    }

    private sealed class ActivityScope(EditorOperationCoordinator owner, EditorOperationActivity activity) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.ActivityChanged?.Invoke(activity with { IsActive = false });
        }
    }

    public Task<T> ExecuteAsync<T>(
        Func<T> operation,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string operationName = "")
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ExecuteAsync(_ => Task.FromResult(operation()), cancellationToken, operationName);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string operationName = "")
    {
        ArgumentNullException.ThrowIfNull(operation);
        CancellationToken lifetimeToken;
        lock (_stateGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stopping)
            {
                throw new InvalidOperationException(
                    "The editor operation queue is stopping for application close.");
            }
            lifetimeToken = _lifetime.Token;
        }

        using var operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                lifetimeToken,
                cancellationToken);
        var token = operationCancellation.Token;
        var timer = Stopwatch.StartNew();
        await _operationGate.WaitAsync(token).ConfigureAwait(false);
        var queueMs = timer.Elapsed.TotalMilliseconds;
        var succeeded = false;
        try
        {
            token.ThrowIfCancellationRequested();
            var result = await Task.Run(() => operation(token), token).ConfigureAwait(false);
            succeeded = true;
            return result;
        }
        finally
        {
            _operationGate.Release();
            OperationMeasured?.Invoke(new EditorOperationMeasurement(
                operationName, queueMs, timer.Elapsed.TotalMilliseconds - queueMs, succeeded));
        }
    }

    public Task ExecuteAsync(
        Action operation,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string operationName = "") =>
        ExecuteAsync(
            () =>
            {
                operation();
                return true;
            },
            cancellationToken, operationName);

    public async Task<T> ExecuteWithActivityAsync<T>(
        string message,
        Func<T> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(operation);

        using var activity = BeginActivity(message);
        return await ExecuteAsync(operation, cancellationToken, message).ConfigureAwait(false);
    }

    public async Task<T> ExecuteShutdownAsync<T>(
        Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        CancellationTokenSource stoppedLifetime;
        lock (_stateGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stopping)
            {
                throw new InvalidOperationException(
                    "The editor operation queue is already stopping.");
            }
            _stopping = true;
            stoppedLifetime = _lifetime;
        }

        stoppedLifetime.Cancel();
        // Cancellation does not interrupt a synchronous transaction. Keep the
        // caller asynchronous, but never publish the final snapshot until the
        // active operation (including its cleanup) has released the gate.
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(operation).ConfigureAwait(false);
        }
        catch
        {
            ResumeAfterFailedShutdown(stoppedLifetime);
            throw;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        CancellationTokenSource lifetime;
        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lifetime = _lifetime;
        }

        lifetime.Cancel();
        lifetime.Dispose();
    }

    private void ResumeAfterFailedShutdown(
        CancellationTokenSource stoppedLifetime)
    {
        lock (_stateGate)
        {
            if (_disposed
                || !ReferenceEquals(
                    _lifetime,
                    stoppedLifetime))
            {
                return;
            }

            _lifetime = new CancellationTokenSource();
            _stopping = false;
            stoppedLifetime.Dispose();
        }
    }
}

public readonly record struct EditorOperationActivity(
    long Id,
    string Message,
    bool IsActive);

public readonly record struct EditorOperationMeasurement(
    string Name, double QueueMs, double WorkMs, bool Succeeded);
