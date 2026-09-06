using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ChromiumPreviewRasterizer : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _registeredAssets = new(StringComparer.Ordinal);
    private readonly object _stderrGate = new();
    private readonly StringBuilder _stderr = new();
    private Process? _process;
    private int _nextId;

    public async Task<RasterResult> RasterizeAsync(
        string html,
        int width,
        int height,
        string outputPath,
        string format,
        int quality,
        double captureScale,
        CancellationToken cancellationToken,
        Func<string, string>? assetResolver = null)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureStarted();
            var process = _process ?? throw new InvalidOperationException("Chromium raster worker did not start.");
            var assetKeys = PreviewAssetRegistry.Keys(html);
            _registeredAssets.IntersectWith(assetKeys);
            var newAssets = new List<object>();
            foreach (var key in assetKeys.Where((key) => !_registeredAssets.Contains(key)))
            {
                var uri = assetResolver is null
                    ? PreviewAssetRegistry.TryResolve(key, out var registeredUri)
                        ? registeredUri
                        : null
                    : assetResolver(key);
                if (string.IsNullOrWhiteSpace(uri))
                {
                    throw new InvalidOperationException($"Raster HTML references unknown preview asset '{key}'.");
                }
                newAssets.Add(new { key, uri });
                _registeredAssets.Add(key);
            }
            var id = (++_nextId).ToString();
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
            {
                id,
                html,
                width,
                height,
                outputPath,
                format,
                quality,
                captureScale,
                assetKeys,
                assets = newAssets,
            }));
            await process.StandardInput.FlushAsync();
            using var responseTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            responseTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            var line = await process.StandardOutput.ReadLineAsync(responseTimeout.Token)
                ?? throw new InvalidOperationException("Chromium raster worker output ended.");
            var response = JsonSerializer.Deserialize<RasterResponse>(line)
                ?? throw new InvalidOperationException("Chromium raster worker returned an empty response.");
            if (!response.Ok)
            {
                throw new InvalidOperationException(response.Error ?? "Chromium raster worker failed.");
            }
            PreviewDebugLog.Write(
                "preview.raster.chromium",
                ("format", format),
                ("width", response.Width),
                ("height", response.Height),
                ("renderMs", response.RenderMs),
                ("patchMs", response.PatchMs),
                ("assetsMs", response.AssetsMs),
                ("readyMs", response.ReadyMs),
                ("captureMs", response.CaptureMs),
                ("newAssets", newAssets.Count),
                ("outputChars", File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0));
            return new RasterResult(outputPath, response.RenderMs, response.CaptureMs);
        }
        catch (OperationCanceledException)
        {
            Restart();
            throw;
        }
        catch (Exception exception)
        {
            var message = FormatFailure(
                exception.Message,
                CaptureStandardError());
            Restart();
            throw new InvalidOperationException(message, exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureStarted()
    {
        if (_process is not null && !_process.HasExited) return;
        var script = ResolveScript();
        var workingDirectory = ResolveRepositoryRoot(script);
        var startInfo = DesktopChildProcess.CreateHiddenStartInfo(
            DesktopChildProcess.ResolveNodeExecutable(),
            workingDirectory,
            redirectStandardInput: true);
        var bundledBrowsers = ResolveBundledBrowserPath();
        if (bundledBrowsers is not null)
        {
            startInfo.Environment["PLAYWRIGHT_BROWSERS_PATH"] = bundledBrowsers;
        }
        startInfo.ArgumentList.Add(script);
        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Chromium raster worker.");
        lock (_stderrGate) _stderr.Clear();
        _process.ErrorDataReceived += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;
            lock (_stderrGate) _stderr.AppendLine(args.Data);
        };
        _process.BeginErrorReadLine();
        _registeredAssets.Clear();
        PreviewDebugLog.Write("preview.raster.chromium.start", ("script", script));
    }

    private static string? ResolveBundledBrowserPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "playwright-browsers"),
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "Resources",
                "playwright-browsers")),
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private string CaptureStandardError()
    {
        try
        {
            if (_process is { HasExited: true } process)
            {
                // Complete asynchronous ErrorDataReceived delivery before the
                // worker is disposed and its diagnostic output is lost.
                process.WaitForExit();
            }
        }
        catch
        {
            // The original worker failure remains authoritative.
        }

        lock (_stderrGate) return _stderr.ToString();
    }

    internal static string FormatFailure(
        string failure,
        string standardError)
    {
        var trimmedFailure = failure.Trim();
        var headline = trimmedFailure.StartsWith(
            "Chromium raster worker",
            StringComparison.Ordinal)
            ? trimmedFailure
            : $"Chromium raster worker failed: {trimmedFailure}";
        var diagnostics = standardError.Trim();
        if (diagnostics.Length == 0) return headline;
        if (diagnostics.Length > 4000)
        {
            diagnostics = diagnostics[^4000..];
        }
        return $"{headline}{Environment.NewLine}{diagnostics}";
    }

    private static string ResolveScript()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "dist", "desktop-preview", "renderPreviewRasterServer.cjs"),
            Path.Combine(AppContext.BaseDirectory, "desktop-preview", "renderPreviewRasterServer.cjs"),
        };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Chromium preview raster server is missing.");
    }

    private static string ResolveRepositoryRoot(string script)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(script) ?? Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "package.json"))) return directory.FullName;
            directory = directory.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private void Restart()
    {
        try
        {
            if (_process is { HasExited: false }) _process.Kill(entireProcessTree: true);
        }
        catch { }
        _process?.Dispose();
        _process = null;
        _registeredAssets.Clear();
    }

    public void Dispose()
    {
        Restart();
        _gate.Dispose();
    }

    public sealed record RasterResult(string Path, double RenderMilliseconds, double CaptureMilliseconds);

    private sealed record RasterResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("renderMs")] double RenderMs,
        [property: JsonPropertyName("patchMs")] double PatchMs,
        [property: JsonPropertyName("assetsMs")] double AssetsMs,
        [property: JsonPropertyName("readyMs")] double ReadyMs,
        [property: JsonPropertyName("captureMs")] double CaptureMs,
        [property: JsonPropertyName("width")] double Width,
        [property: JsonPropertyName("height")] double Height);
}
