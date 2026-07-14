using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class WebDesignPreviewRenderer
{
    private const int DefaultFrameCacheCapacity = 180;
    private const int MaximumFrameCacheCapacity = 4096;
    private static readonly object CacheGate = new();
    private static readonly Dictionary<string, LinkedListNode<FrameCacheEntry>> FrameCache = new(StringComparer.Ordinal);
    private static readonly LinkedList<FrameCacheEntry> FrameCacheOrder = [];
    private static readonly PersistentPreviewRenderer PersistentRenderer = new();
    private static readonly PersistentPreviewRenderer PrewarmPersistentRenderer = new();
    private static readonly Dictionary<long, int> FrameCacheReservations = [];
    private static int _frameCacheCapacity = DefaultFrameCacheCapacity;
    private static long _nextFrameCacheReservationId;

    public static async Task<string> RenderBodyAsync(
        SpikeDatabase.DevicePreviewMetrics metrics,
        string themeMode,
        bool showMarks,
        DesignPreviewPayload payload)
    {
        return await RenderBodyAsync(metrics, themeMode, showMarks, payload, PersistentRenderer, "interactive");
    }

    public static async Task<string> RenderPrewarmBodyAsync(
        SpikeDatabase.DevicePreviewMetrics metrics,
        string themeMode,
        bool showMarks,
        DesignPreviewPayload payload)
    {
        return await RenderBodyAsync(metrics, themeMode, showMarks, payload, PrewarmPersistentRenderer, "prewarm");
    }

    private static async Task<string> RenderBodyAsync(
        SpikeDatabase.DevicePreviewMetrics metrics,
        string themeMode,
        bool showMarks,
        DesignPreviewPayload payload,
        PersistentPreviewRenderer persistentRenderer,
        string lane)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = CreateRequest(metrics, themeMode, showMarks, payload);
        var requestJson = JsonSerializer.Serialize(request);
        var renderer = ResolveRendererCommand();
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestJson)));
        var frameCacheKey = $"{renderer.Version}:{requestHash}";
        if (TryGetCachedFrame(frameCacheKey, out var cachedHtml))
        {
            PreviewDebugLog.Write(
                "preview.render.body",
                ("route", "cache-hit"),
                ("lane", lane),
                ("component", payload.ComponentType),
                ("name", payload.Name),
                ("themeMode", themeMode),
                ("showMarks", showMarks),
                ("ms", stopwatch.Elapsed.TotalMilliseconds),
                ("htmlChars", cachedHtml.Length),
                ("cacheSize", CacheSize()));
            return cachedHtml;
        }

        var originalHtml = await persistentRenderer.RenderAsync(renderer, requestJson);
        var html = PreviewAssetRegistry.Compact(originalHtml);
        CacheFrame(frameCacheKey, html);
        PreviewDebugLog.Write(
            "preview.render.body",
            ("route", "rendered"),
            ("lane", lane),
            ("component", payload.ComponentType),
            ("name", payload.Name),
            ("themeMode", themeMode),
            ("showMarks", showMarks),
            ("ms", stopwatch.Elapsed.TotalMilliseconds),
            ("htmlChars", html.Length),
            ("originalHtmlChars", originalHtml.Length),
            ("cacheSize", CacheSize()));
        return html;
    }

    public static async Task PrewarmBodyAsync(
        SpikeDatabase.DevicePreviewMetrics metrics,
        string themeMode,
        bool showMarks,
        DesignPreviewPayload payload)
    {
        _ = await RenderPrewarmBodyAsync(metrics, themeMode, showMarks, payload);
    }

    public static IDisposable ReserveFrameCacheCapacity(int frameCount)
    {
        var reservationId = Interlocked.Increment(ref _nextFrameCacheReservationId);
        lock (CacheGate)
        {
            FrameCacheReservations[reservationId] = Math.Clamp(
                frameCount,
                DefaultFrameCacheCapacity,
                MaximumFrameCacheCapacity);
            UpdateFrameCacheCapacityLocked();
            PreviewDebugLog.Write(
                "preview.cache.reserve",
                ("requestedFrames", frameCount),
                ("capacity", _frameCacheCapacity),
                ("reservations", FrameCacheReservations.Count));
        }

        return new FrameCacheReservation(reservationId);
    }

    private static void ReleaseFrameCacheCapacity(long reservationId)
    {
        lock (CacheGate)
        {
            if (!FrameCacheReservations.Remove(reservationId)) return;
            UpdateFrameCacheCapacityLocked();
            TrimFrameCacheLocked();
            PreviewDebugLog.Write(
                "preview.cache.release",
                ("capacity", _frameCacheCapacity),
                ("cacheSize", FrameCache.Count),
                ("reservations", FrameCacheReservations.Count));
        }
    }

    private static void UpdateFrameCacheCapacityLocked()
    {
        _frameCacheCapacity = FrameCacheReservations.Count == 0
            ? DefaultFrameCacheCapacity
            : Math.Max(DefaultFrameCacheCapacity, FrameCacheReservations.Values.Max());
    }

    private static object CreateRequest(
        SpikeDatabase.DevicePreviewMetrics metrics,
        string themeMode,
        bool showMarks,
        DesignPreviewPayload payload)
    {
        return new
        {
            kind = payload.Kind,
            componentType = payload.ComponentType,
            configJson = payload.ConfigJson,
            designPreviewJson = payload.DesignPreviewJson,
            showMarks,
            themeMode = payload.ThemeMode is "dark" ? "dark" : themeMode is "dark" ? "dark" : "light",
            themeTokensJson = payload.ThemeTokensJson,
            themeStatusBarPresetId = payload.ThemeStatusBarPresetId,
            themeNavigationBarPresetId = payload.ThemeNavigationBarPresetId,
            paletteColors = payload.PaletteColors,
            paletteNeutralColors = payload.PaletteNeutralColors,
            projectMediaRoot = payload.ProjectMediaRoot,
            fontFaces = payload.FontFaces.Select((face) => new
            {
                fontId = face.FontId,
                family = face.FamilyName,
                category = face.Category,
                relativePath = face.RelativePath,
                weight = face.Weight,
                style = face.Style,
            }),
            iconAssetRoot = payload.IconAssetRoot,
            iconMappingJson = payload.IconMappingJson,
            componentBaseConfigsJson = payload.ComponentBaseConfigsJson,
            appConfigJson = payload.AppConfigJson,
            instanceJson = payload.InstanceJson,
            frameRate = payload.FrameRate,
            localFrame = payload.LocalFrame,
            previewFrame = new
            {
                canvasWidth = metrics.CanvasWidth,
                canvasHeight = metrics.CanvasHeight,
                screenX = metrics.ScreenX,
                screenY = metrics.ScreenY,
                screenWidth = metrics.ScreenWidth,
                screenHeight = metrics.ScreenHeight,
                scaleToPixels = metrics.ScaleToPixels,
            },
        };
    }

    private static async Task<string> RenderBodyOneShotAsync(string requestJson)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = Path.Combine(Path.GetTempPath(), $"mockups-design-preview-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(requestPath, requestJson);

            var html = await RunRendererAsync(requestPath);
            PreviewDebugLog.Write(
                "preview.renderer.oneshot",
                ("ms", stopwatch.Elapsed.TotalMilliseconds),
                ("htmlChars", html.Length));
            return html;
        }
        finally
        {
            try
            {
                if (File.Exists(requestPath))
                {
                    File.Delete(requestPath);
                }
            }
            catch
            {
                // Temporary payload cleanup must not block preview rendering.
            }
        }
    }

    private static async Task<string> RunRendererAsync(string requestPath)
    {
        var renderer = ResolveRendererCommand();
        var startInfo = DesktopChildProcess.CreateHiddenStartInfo(renderer.Executable, renderer.WorkingDirectory);
        startInfo.ArgumentList.Add(renderer.Script);
        startInfo.ArgumentList.Add(requestPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start design preview renderer.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Design preview renderer failed with exit code {process.ExitCode}: {stderr}");
        }

        return stdout;
    }

    private static RendererCommand ResolveRendererCommand()
    {
        var packagedRenderer = Path.Combine(
            AppContext.BaseDirectory,
            "desktop-preview",
            "renderDesignPreviewHtml.cjs");
        var packagedServer = Path.Combine(
            AppContext.BaseDirectory,
            "desktop-preview",
            "renderDesignPreviewHtmlServer.cjs");
        if (File.Exists(packagedRenderer))
        {
            return new RendererCommand(
                ResolveNodeExecutable(),
                AppContext.BaseDirectory,
                packagedRenderer,
                File.Exists(packagedServer) ? packagedServer : "",
                RendererVersion(packagedRenderer, packagedServer));
        }

        var root = FindRepositoryRoot();
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "tsx.cmd"
            : "tsx";
        var executable = Path.Combine(root, "node_modules", ".bin", executableName);
        var script = Path.Combine(root, "src", "desktop-preview", "renderDesignPreviewHtml.tsx");
        var serverScript = Path.Combine(root, "src", "desktop-preview", "renderDesignPreviewHtmlServer.ts");

        return new RendererCommand(executable, root, script, serverScript, RendererVersion(script, serverScript));
    }

    private static string RendererVersion(string script, string serverScript)
    {
        static long Ticks(string path) => File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
        return $"{Ticks(script)}:{Ticks(serverScript)}";
    }

    private sealed record RendererCommand(string Executable, string WorkingDirectory, string Script, string ServerScript, string Version);

    private sealed record FrameCacheEntry(string Key, string Html);

    private static bool TryGetCachedFrame(string key, out string html)
    {
        lock (CacheGate)
        {
            if (FrameCache.TryGetValue(key, out var node))
            {
                FrameCacheOrder.Remove(node);
                FrameCacheOrder.AddFirst(node);
                html = node.Value.Html;
                return true;
            }
        }

        html = "";
        return false;
    }

    private static void CacheFrame(string key, string html)
    {
        lock (CacheGate)
        {
            if (FrameCache.TryGetValue(key, out var existing))
            {
                existing.Value = existing.Value with { Html = html };
                FrameCacheOrder.Remove(existing);
                FrameCacheOrder.AddFirst(existing);
                return;
            }

            var node = new LinkedListNode<FrameCacheEntry>(new FrameCacheEntry(key, html));
            FrameCacheOrder.AddFirst(node);
            FrameCache[key] = node;
            TrimFrameCacheLocked();
        }
    }

    private static void TrimFrameCacheLocked()
    {
        while (FrameCache.Count > _frameCacheCapacity && FrameCacheOrder.Last is not null)
        {
            var last = FrameCacheOrder.Last;
            FrameCacheOrder.RemoveLast();
            FrameCache.Remove(last.Value.Key);
        }
    }

    private static int CacheSize()
    {
        lock (CacheGate)
        {
            return FrameCache.Count;
        }
    }

    private sealed class FrameCacheReservation(long reservationId) : IDisposable
    {
        private long _reservationId = reservationId;

        public void Dispose()
        {
            var id = Interlocked.Exchange(ref _reservationId, 0);
            if (id != 0) ReleaseFrameCacheCapacity(id);
        }
    }

    private sealed class PersistentPreviewRenderer
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private Process? _process;
        private RendererCommand? _command;
        private readonly StringBuilder _stderr = new();
        private int _nextId;

        public async Task<string> RenderAsync(RendererCommand command, string requestJson)
        {
            if (string.IsNullOrWhiteSpace(command.ServerScript) || !File.Exists(command.ServerScript))
            {
                PreviewDebugLog.Write(
                    "preview.renderer.route",
                    ("route", "oneshot"),
                    ("reason", "server-script-missing"),
                    ("script", command.Script));
                return await RenderBodyOneShotAsync(requestJson);
            }

            await _gate.WaitAsync();
            try
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    EnsureStarted(command);
                    var process = _process
                        ?? throw new InvalidOperationException("Persistent design preview renderer did not start.");
                    var id = (++_nextId).ToString();
                    await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        id,
                        payload = JsonSerializer.Deserialize<JsonElement>(requestJson),
                    }));
                    await process.StandardInput.FlushAsync();

                    var line = await process.StandardOutput.ReadLineAsync();
                    if (line is null)
                    {
                        PreviewDebugLog.Write(
                            "preview.renderer.route",
                            ("route", "oneshot"),
                            ("reason", "server-output-ended"),
                            ("script", command.Script));
                        Restart();
                        return await RenderBodyOneShotAsync(requestJson);
                    }

                    var response = JsonSerializer.Deserialize<RendererResponse>(line)
                        ?? throw new InvalidOperationException("Design preview renderer returned an empty response.");
                    if (!response.Ok)
                    {
                        throw new InvalidOperationException(response.Error ?? "Design preview renderer failed.");
                    }

                    foreach (var asset in response.Assets ?? [])
                    {
                        PreviewAssetRegistry.Register(asset.Key, asset.Uri);
                    }

                    var html = response.Html ?? "";
                    PreviewDebugLog.Write(
                        "preview.renderer.persistent",
                        ("id", id),
                        ("ms", stopwatch.Elapsed.TotalMilliseconds),
                        ("htmlChars", html.Length),
                        ("newAssets", response.Assets?.Count ?? 0));
                    return html;
                }
                catch (Exception error)
                {
                    PreviewDebugLog.Write(
                        "preview.renderer.error",
                        ("message", error.Message));
                    Restart();
                    throw;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private void EnsureStarted(RendererCommand command)
        {
            if (_process is not null
                && !_process.HasExited
                && _command == command)
            {
                return;
            }

            Restart();
            _command = command;
            _stderr.Clear();
            var startInfo = DesktopChildProcess.CreateHiddenStartInfo(command.Executable, command.WorkingDirectory);
            startInfo.Environment["MOCKUPS_PREVIEW_DEBUG_LOG"] = PreviewDebugLog.FilePath;
            startInfo.ArgumentList.Add(command.ServerScript);
            startInfo.RedirectStandardInput = true;
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start persistent design preview renderer.");
            PreviewDebugLog.Write(
                "preview.renderer.start",
                ("executable", command.Executable),
                ("script", command.ServerScript),
                ("workingDirectory", command.WorkingDirectory));
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    _stderr.AppendLine(args.Data);
                }
            };
            process.BeginErrorReadLine();
            _process = process;
        }

        private void Restart()
        {
            if (_process is null)
            {
                return;
            }

            try
            {
                if (!_process.HasExited)
                {
                    PreviewDebugLog.Write("preview.renderer.restart");
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Renderer restart is best-effort; next render can fall back to one-shot.
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }
    }

    private sealed record RendererResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("html")] string? Html,
        [property: JsonPropertyName("assets")] IReadOnlyList<RendererAsset>? Assets,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record RendererAsset(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("uri")] string Uri);

    private static string ResolveNodeExecutable()
    {
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "node.exe"
            : "node";
        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[]
            {
                Path.Combine(AppContext.BaseDirectory, "node", executableName),
                executableName,
            }
            : new[]
            {
                Path.Combine(AppContext.BaseDirectory, "node", "bin", executableName),
                "/opt/homebrew/bin/node",
                "/usr/local/bin/node",
                "/usr/bin/node",
                executableName,
            };

        return candidates.FirstOrDefault(File.Exists) ?? executableName;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "package.json"))
                && Directory.Exists(Path.Combine(directory.FullName, "src", "visual")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate MOCKUPS repository root.");
    }
}
