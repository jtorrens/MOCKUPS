using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Mockups.DesktopEditorShell;

internal sealed class DesktopVisualInstanceLease : IDisposable
{
    private Mutex? _mutex;

    private DesktopVisualInstanceLease(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static DesktopVisualInstanceLease? TryAcquire(
        string? identity = null)
    {
        var mutex = new Mutex(
            initiallyOwned: false,
            MutexName(identity));
        var acquired = false;
        try
        {
            acquired = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }
        if (!acquired)
        {
            mutex.Dispose();
            return null;
        }
        return new DesktopVisualInstanceLease(mutex);
    }

    public void Dispose()
    {
        var mutex = Interlocked.Exchange(
            ref _mutex,
            null);
        if (mutex is null)
        {
            return;
        }
        mutex.ReleaseMutex();
        mutex.Dispose();
    }

    private static string MutexName(string? identity)
    {
        var source = string.IsNullOrWhiteSpace(identity)
            ? "MOCKUPS.Desktop.VisualEditor.v1"
            : identity;
        var hash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(source)))
            .ToLowerInvariant();
        return $"mockups-desktop-visual-{hash}";
    }
}
