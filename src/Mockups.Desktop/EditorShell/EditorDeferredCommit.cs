using Avalonia.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

/// <summary>
/// Owns the standard editor draft boundary: free-form input remains local
/// until the user pauses, confirms, or leaves the control.
/// </summary>
internal sealed class EditorDeferredCommit
{
    public static readonly TimeSpan StandardDelay =
        TimeSpan.FromMilliseconds(300);

    private readonly Action _commit;
    private readonly Func<bool> _canCommit;
    private CancellationTokenSource? _pending;

    public EditorDeferredCommit(
        Action commit,
        Func<bool>? canCommit = null)
    {
        _commit = commit ?? throw new ArgumentNullException(nameof(commit));
        _canCommit = canCommit ?? (() => true);
    }

    public void Schedule()
    {
        Cancel();
        var pending = new CancellationTokenSource();
        _pending = pending;
        _ = CommitAfterPauseAsync(pending);
    }

    public void CommitNow()
    {
        Cancel();
        if (_canCommit())
        {
            _commit();
        }
    }

    public void Cancel()
    {
        var pending = _pending;
        _pending = null;
        pending?.Cancel();
    }

    private async Task CommitAfterPauseAsync(
        CancellationTokenSource pending)
    {
        try
        {
            await Task.Delay(StandardDelay, pending.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            pending.Dispose();
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_pending, pending)
                || pending.IsCancellationRequested)
            {
                pending.Dispose();
                return;
            }

            _pending = null;
            pending.Dispose();
            if (_canCommit())
            {
                _commit();
            }
        });
    }
}
