using System;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Mockups.DesktopEditorShell;

internal static class WorkstationUpdateMaintenance
{
    internal const string LockFileName =
        ".mockups-update-maintenance.json";
    internal const string ApplicationLockFileName =
        ".mockups-application-active.json";

    public static string LockFilePath(
        string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            databasePath);
        var currentDatabasePath = Path.GetFullPath(
            databasePath);
        return Path.Combine(
            Path.GetDirectoryName(currentDatabasePath)!,
            LockFileName);
    }

    public static bool IsActive(
        string databasePath) =>
        File.Exists(LockFilePath(databasePath));

    public static WorkstationApplicationAccessLease?
        TryAcquireApplicationAccess(
            string databasePath)
    {
        var directory = Path.GetDirectoryName(
            Path.GetFullPath(databasePath))!;
        Directory.CreateDirectory(directory);
        var applicationLockPath = Path.Combine(
            directory,
            ApplicationLockFileName);
        FileStream? stream = null;
        try
        {
            try
            {
                stream = CreateApplicationLock(
                    applicationLockPath);
            }
            catch (IOException)
            {
                File.Delete(applicationLockPath);
                stream = CreateApplicationLock(
                    applicationLockPath);
            }

            if (IsActive(databasePath))
            {
                stream.Dispose();
                stream = null;
                File.Delete(applicationLockPath);
                return null;
            }

            return new WorkstationApplicationAccessLease(
                stream,
                applicationLockPath);
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    private static FileStream CreateApplicationLock(
        string path)
    {
        var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read);
        JsonSerializer.Serialize(
            stream,
            new
            {
                schemaVersion = 1,
                state = "active",
                processId = Environment.ProcessId,
                startedAt = DateTimeOffset.UtcNow,
            });
        stream.Flush(flushToDisk: true);
        return stream;
    }
}

internal sealed class WorkstationApplicationAccessLease(
    FileStream stream,
    string path) : IDisposable
{
    private FileStream? _stream = stream;

    public void Dispose()
    {
        var current = Interlocked.Exchange(
            ref _stream,
            null);
        if (current is null)
        {
            return;
        }

        current.Dispose();
        File.Delete(path);
    }
}
