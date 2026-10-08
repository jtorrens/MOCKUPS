using Mockups.DesktopEditorShell.EditorShell;
using System.Diagnostics;

internal static class EditorOperationShutdownTests
{
    public static void WaitForActiveWrite() => WaitForActiveWriteAsync().GetAwaiter().GetResult();

    private static async Task WaitForActiveWriteAsync()
    {
        using var operations = new EditorOperationCoordinator();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var written = false;
        var queuedRan = false;
        var writer = operations.ExecuteAsync(() =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Writer was not released.");
            written = true;
        });
        Task<bool>? shutdown = null;
        Require(entered.Wait(TimeSpan.FromSeconds(10)), "Writer did not start.");
        var queued = operations.ExecuteAsync(() => queuedRan = true);
        try
        {
            var callerThread = Environment.CurrentManagedThreadId;
            var timer = Stopwatch.StartNew();
            shutdown = operations.ExecuteShutdownAsync(() =>
            {
                Require(Environment.CurrentManagedThreadId != callerThread, "Shutdown must run on a worker.");
                return written;
            });
            Require(timer.Elapsed < TimeSpan.FromSeconds(1), "Starting shutdown blocked its caller.");
            await Expect<OperationCanceledException>(() => queued);
            await Expect<InvalidOperationException>(() => operations.ExecuteAsync(() => true));
            await Expect<InvalidOperationException>(() => operations.ExecuteShutdownAsync(() => true));
            Require(await Task.WhenAny(shutdown, Task.Delay(3500)) != shutdown,
                "Shutdown bypassed a still-active write after the old timeout.");
            Require(!queuedRan, "Queued work executed during shutdown.");
            release.Set();
            await writer;
            Require(await shutdown.WaitAsync(TimeSpan.FromSeconds(10)), "Shutdown did not observe the completed write.");
        }
        finally
        {
            release.Set();
            await writer;
            if (shutdown is not null) await shutdown.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    public static void WaitForCancellationCleanup() => WaitForCancellationCleanupAsync().GetAwaiter().GetResult();

    private static async Task WaitForCancellationCleanupAsync()
    {
        using var operations = new EditorOperationCoordinator();
        var entered = Signal();
        var cleaning = Signal();
        var release = Signal();
        var cleaned = false;
        var work = operations.ExecuteAsync(async token =>
        {
            entered.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally
            {
                cleaning.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
                cleaned = true;
            }
            return true;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var shutdown = operations.ExecuteShutdownAsync(() => cleaned);
        try
        {
            await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Require(!shutdown.IsCompleted, "Shutdown overtook cancellation cleanup.");
        }
        finally { release.SetResult(); }
        await Expect<OperationCanceledException>(() => work);
        Require(await shutdown.WaitAsync(TimeSpan.FromSeconds(10)), "Shutdown did not observe cleanup completion.");
    }

    public static void FailedShutdownCanRetry() => FailedShutdownCanRetryAsync().GetAwaiter().GetResult();

    private static async Task FailedShutdownCanRetryAsync()
    {
        using var operations = new EditorOperationCoordinator();
        await Expect<IOException>(() => operations.ExecuteShutdownAsync<bool>(() => throw new IOException("Snapshot failed")));
        var edited = await operations.ExecuteAsync(async token =>
        {
            token.ThrowIfCancellationRequested();
            await Task.Yield();
            return 42;
        }).WaitAsync(TimeSpan.FromSeconds(10));
        Require(await operations.ExecuteShutdownAsync(() => edited).WaitAsync(TimeSpan.FromSeconds(10)) == 42,
            "Retry did not observe subsequent edits.");
        await Expect<InvalidOperationException>(() => operations.ExecuteAsync(() => true));
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static async Task Expect<T>(Func<Task> action) where T : Exception
    {
        try { await action().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
}
