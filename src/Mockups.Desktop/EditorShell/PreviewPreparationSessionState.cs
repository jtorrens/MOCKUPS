using System;
using System.Threading;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class PreviewPreparationCancellation : IDisposable
{
    private CancellationTokenSource? _current;

    public CancellationTokenSource Begin()
    {
        _current?.Cancel();
        var operation = new CancellationTokenSource();
        _current = operation;
        return operation;
    }

    public void Cancel()
    {
        _current?.Cancel();
    }

    public bool IsCurrent(CancellationTokenSource operation)
    {
        return ReferenceEquals(_current, operation);
    }

    public bool Complete(CancellationTokenSource operation)
    {
        var wasCurrent = IsCurrent(operation);
        if (wasCurrent)
        {
            _current = null;
        }

        operation.Dispose();
        return wasCurrent;
    }

    public void Dispose()
    {
        var operation = _current;
        _current = null;
        operation?.Cancel();
        operation?.Dispose();
    }
}

internal enum PreparedPlaybackReuse
{
    None,
    Frames,
    Complete,
}

internal static class PreparedPlaybackReusePolicy
{
    public static PreparedPlaybackReuse Decide(
        string? preparedSignature,
        string requestSignature,
        bool hasFrameCacheReservation)
    {
        if (string.IsNullOrEmpty(preparedSignature)
            || !preparedSignature.Equals(requestSignature, StringComparison.Ordinal))
        {
            return PreparedPlaybackReuse.None;
        }

        return hasFrameCacheReservation
            ? PreparedPlaybackReuse.Complete
            : PreparedPlaybackReuse.Frames;
    }
}
