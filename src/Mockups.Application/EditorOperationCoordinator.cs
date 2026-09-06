using System;
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

    public async Task<T> ExecuteAsync<T>(
        Func<T> operation,
        CancellationToken cancellationToken = default)
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
        await _operationGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            return await Task.Run(operation, token).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public Task ExecuteAsync(
        Action operation,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            () =>
            {
                operation();
                return true;
            },
            cancellationToken);

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
        var ownsGate = await _operationGate.WaitAsync(
                TimeSpan.FromSeconds(3))
            .ConfigureAwait(false);
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
            if (ownsGate)
            {
                _operationGate.Release();
            }
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
