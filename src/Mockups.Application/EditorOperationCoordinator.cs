using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed class EditorOperationCoordinator : IDisposable
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    public async Task<T> ExecuteAsync<T>(
        Func<T> operation,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(operation);

        using var operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                _lifetime.Token,
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
