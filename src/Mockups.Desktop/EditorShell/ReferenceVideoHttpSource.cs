using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ReferenceVideoHttpSource : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _serveTask;
    private string _sourcePath = "";
    private bool _disposed;

    public ReferenceVideoHttpSource()
    {
        var port = AvailableLoopbackPort();
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add(BaseUri.AbsoluteUri);
        _listener.Start();
        _serveTask = ServeAsync(_cancellation.Token);
    }

    public Uri BaseUri { get; }

    public string Use(string sourcePath)
    {
        _sourcePath = Path.GetFullPath(sourcePath);
        return new Uri(BaseUri, "reference-video").AbsoluteUri;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellation.Cancel();
        _listener.Close();
        _ = _serveTask.ContinueWith(
            _ => _cancellation.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync()
                    .WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _ = Task.Run(
                () => RespondAsync(context, cancellationToken),
                CancellationToken.None);
        }
    }

    private async Task RespondAsync(
        HttpListenerContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;
            if (request.Url?.AbsolutePath != "/reference-video"
                || request.HttpMethod is not ("GET" or "HEAD"))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Close();
                return;
            }

            var sourcePath = _sourcePath;
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Close();
                return;
            }

            await using var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                1024 * 128,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var length = source.Length;
            var (start, end, isPartial) = RequestedRange(
                request.Headers["Range"],
                length);
            var responseLength = end - start + 1;
            response.StatusCode = isPartial
                ? (int)HttpStatusCode.PartialContent
                : (int)HttpStatusCode.OK;
            response.ContentType = ContentType(sourcePath);
            response.ContentLength64 = responseLength;
            response.AddHeader("Accept-Ranges", "bytes");
            if (isPartial)
            {
                response.AddHeader(
                    "Content-Range",
                    $"bytes {start}-{end}/{length}");
            }
            if (request.HttpMethod == "HEAD")
            {
                response.Close();
                return;
            }

            source.Position = start;
            var remaining = responseLength;
            var buffer = new byte[1024 * 128];
            while (remaining > 0)
            {
                var read = await source.ReadAsync(
                    buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                    cancellationToken);
                if (read == 0) break;
                await response.OutputStream.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
                remaining -= read;
            }
            response.Close();
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            context.Response.Abort();
        }
        catch
        {
            context.Response.Abort();
        }
    }

    private static (long Start, long End, bool IsPartial) RequestedRange(
        string? header,
        long length)
    {
        if (string.IsNullOrWhiteSpace(header)
            || !header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return (0, Math.Max(0, length - 1), false);
        }

        var parts = header["bytes=".Length..].Split('-', 2);
        if (parts.Length != 2
            || !long.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var start)
            || start < 0
            || start >= length)
        {
            return (0, Math.Max(0, length - 1), false);
        }
        var end = string.IsNullOrWhiteSpace(parts[1])
            ? length - 1
            : long.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var requestedEnd)
                ? Math.Min(requestedEnd, length - 1)
                : length - 1;
        return end < start
            ? (0, Math.Max(0, length - 1), false)
            : (start, end, true);
    }

    private static string ContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mov" => "video/quicktime",
            ".webm" => "video/webm",
            _ => "video/mp4",
        };

    private static int AvailableLoopbackPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
