using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Mockups.DesktopEditorShell;

internal sealed class DesktopVisualInstanceLease : IDisposable
{
    private FileStream? _lockFile;

    private DesktopVisualInstanceLease(
        FileStream lockFile)
    {
        _lockFile = lockFile;
    }

    public static DesktopVisualInstanceLease? TryAcquire(
        string? identity = null)
    {
        var path = LockFilePath(identity);
        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!);
        try
        {
            var lockFile = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return new DesktopVisualInstanceLease(
                lockFile);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static bool TryRun(
        Action visualLifetime,
        string? identity = null)
    {
        ArgumentNullException.ThrowIfNull(visualLifetime);
        using var lease = TryAcquire(identity);
        if (lease is null)
        {
            return false;
        }

        visualLifetime();
        return true;
    }

    public void Dispose()
    {
        var lockFile = Interlocked.Exchange(
            ref _lockFile,
            null);
        lockFile?.Dispose();
    }

    private static string LockFilePath(
        string? identity)
    {
        var source = string.IsNullOrWhiteSpace(identity)
            ? "MOCKUPS.Desktop.VisualEditor.v1"
            : identity;
        var hash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(source)))
            .ToLowerInvariant();
        return Path.Combine(
            Path.GetTempPath(),
            "mockups-desktop",
            $"visual-{hash}.lock");
    }
}
