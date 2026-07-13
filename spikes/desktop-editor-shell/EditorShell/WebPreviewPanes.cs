using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal abstract class WebPreviewPane : Grid
{
    protected readonly NativeWebView WebView;
    private readonly Image _nativeRasterFrame = new()
    {
        Stretch = Stretch.Fill,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        IsVisible = false,
    };
    private Bitmap? _nativeRasterBitmap;
    private bool _nativeRasterBitmapIsBuffered;
    private readonly object _rasterBufferGate = new();
    private readonly Dictionary<string, Bitmap> _rasterBuffer = new(StringComparer.Ordinal);
    private IReadOnlyList<string> _rasterPlaybackPaths = Array.Empty<string>();
    private Dictionary<string, int> _rasterPlaybackIndexes = new(StringComparer.Ordinal);
    private Task? _rasterBufferTask;
    private CancellationTokenSource? _rasterBufferCancellation;
    private const int RasterBufferAhead = 18;
    private const int RasterBufferBehind = 2;

    protected WebPreviewPane()
    {
        WebView = new NativeWebView
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };

        Children.Add(WebView);
        Children.Add(_nativeRasterFrame);
    }

    public string NativeHostLifecycleState()
    {
        return $"pane={Bounds.X:0.##},{Bounds.Y:0.##},{Bounds.Width:0.##},{Bounds.Height:0.##};"
            + $"webview={WebView.Bounds.X:0.##},{WebView.Bounds.Y:0.##},{WebView.Bounds.Width:0.##},{WebView.Bounds.Height:0.##};"
            + $"webVisible={WebView.IsVisible};rasterVisible={_nativeRasterFrame.IsVisible}";
    }

    public void ShowRasterFrame(string rasterId)
    {
        if (!File.Exists(rasterId)) return;
        Bitmap bitmap;
        var isBuffered = false;
        lock (_rasterBufferGate)
        {
            isBuffered = _rasterBuffer.TryGetValue(rasterId, out bitmap!);
        }
        bitmap ??= new Bitmap(rasterId);
        var previous = _nativeRasterBitmap;
        var disposePrevious = previous is not null && !_nativeRasterBitmapIsBuffered;
        _nativeRasterBitmap = bitmap;
        _nativeRasterBitmapIsBuffered = isBuffered;
        _nativeRasterFrame.Source = bitmap;
        _nativeRasterFrame.IsVisible = true;
        if (disposePrevious) previous?.Dispose();
        if (_rasterPlaybackIndexes.TryGetValue(rasterId, out var frameIndex)) ScheduleRasterBuffer(frameIndex);
    }

    public void HideRasterFrame()
    {
        _nativeRasterFrame.IsVisible = false;
        WebView.IsVisible = true;
    }

    public void PlayRasterFrames(IReadOnlyList<string> rasterIds, int framesPerSecond)
    {
        if (rasterIds.Count == 0) return;
        ShowRasterFrame(rasterIds[0]);
        // NativeWebView owns a platform airspace surface and always paints above
        // Avalonia siblings. Geometry is sampled before this point; hide only the
        // native surface while the native bitmap player is active.
        WebView.IsVisible = false;
    }

    public async Task SyncRasterViewportAsync()
    {
        try
        {
            var result = await WebView.InvokeScript("""
                (() => {
                  const viewport = document.getElementById("previewViewport");
                  if (!viewport) return "";
                  const bounds = viewport.getBoundingClientRect();
                  return JSON.stringify({ x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height });
                })();
                """);
            var json = result?.ToString();
            if (string.IsNullOrWhiteSpace(json)) return;
            var bounds = JsonNode.Parse(json)?.AsObject();
            if (bounds is null) return;
            var x = bounds["x"]?.GetValue<double>() ?? 0;
            var y = bounds["y"]?.GetValue<double>() ?? 0;
            var width = bounds["width"]?.GetValue<double>() ?? 0;
            var height = bounds["height"]?.GetValue<double>() ?? 0;
            if (width <= 0 || height <= 0) return;
            _nativeRasterFrame.Width = width;
            _nativeRasterFrame.Height = height;
            _nativeRasterFrame.Margin = new Avalonia.Thickness(x, y, 0, 0);
        }
        catch (Exception error)
        {
            PreviewDebugLog.Write("preview.raster.viewport.error", ("error", error.Message));
        }
    }

    public async Task PrepareRasterPlaybackAsync(IReadOnlyList<string> rasterIds, CancellationToken cancellationToken)
    {
        ResetRasterBuffer();
        _rasterPlaybackPaths = rasterIds.ToArray();
        _rasterPlaybackIndexes = rasterIds
            .Select((path, index) => (path, index))
            .ToDictionary((entry) => entry.path, (entry) => entry.index, StringComparer.Ordinal);
        _rasterBufferCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await DecodeRasterRangeAsync(0, Math.Min(RasterBufferAhead, rasterIds.Count), _rasterBufferCancellation.Token);
    }

    private void ScheduleRasterBuffer(int frameIndex)
    {
        if (_rasterBufferCancellation is null || (_rasterBufferTask is not null && !_rasterBufferTask.IsCompleted)) return;
        var cancellationToken = _rasterBufferCancellation.Token;
        _rasterBufferTask = DecodeRasterRangeAsync(
            frameIndex + 1,
            Math.Min(_rasterPlaybackPaths.Count, frameIndex + 1 + RasterBufferAhead),
            cancellationToken);
        var oldestIndex = Math.Max(0, frameIndex - RasterBufferBehind);
        lock (_rasterBufferGate)
        {
            foreach (var path in _rasterBuffer.Keys
                         .Where((path) => _rasterPlaybackIndexes.TryGetValue(path, out var index) && index < oldestIndex)
                         .ToArray())
            {
                if (ReferenceEquals(_rasterBuffer[path], _nativeRasterBitmap)) continue;
                _rasterBuffer.Remove(path, out var expired);
                expired?.Dispose();
            }
        }
    }

    private async Task DecodeRasterRangeAsync(int startIndex, int endIndex, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            for (var index = startIndex; index < endIndex; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = _rasterPlaybackPaths[index];
                lock (_rasterBufferGate)
                {
                    if (_rasterBuffer.ContainsKey(path)) continue;
                }
                var bitmap = new Bitmap(path);
                lock (_rasterBufferGate)
                {
                    if (!_rasterBuffer.TryAdd(path, bitmap)) bitmap.Dispose();
                }
            }
        }, cancellationToken);
    }

    private void ResetRasterBuffer()
    {
        _rasterBufferCancellation?.Cancel();
        _rasterBufferCancellation?.Dispose();
        _rasterBufferCancellation = null;
        lock (_rasterBufferGate)
        {
            foreach (var bitmap in _rasterBuffer.Values)
            {
                if (!ReferenceEquals(bitmap, _nativeRasterBitmap)) bitmap.Dispose();
            }
            _rasterBuffer.Clear();
        }
        _nativeRasterBitmapIsBuffered = false;
    }

    protected void LoadHtml(string html)
    {
        WebView.NavigateToString(PreviewAssetRegistry.Expand(html), new Uri("https://mockups.local/"));
    }

    protected async Task<bool> ReplacePreviewBodyAsync(string bodyContent, bool waitForCommit = false)
    {
        var stopwatch = Stopwatch.StartNew();
        var assetKeys = PreviewAssetRegistry.Keys(bodyContent);
        var bodyJson = JsonSerializer.Serialize(bodyContent);
        try
        {
            var assetKeysJson = JsonSerializer.Serialize(assetKeys);
            var missingResult = await WebView.InvokeScript($$"""
                (() => {
                  if (typeof window.mockupsMissingPreviewAssets !== "function") return JSON.stringify({{assetKeysJson}});
                  return window.mockupsMissingPreviewAssets({{assetKeysJson}});
                })();
                """);
            var missingJson = missingResult?.ToString() ?? "[]";
            var missingKeys = JsonNode.Parse(string.IsNullOrWhiteSpace(missingJson) ? "[]" : missingJson) is JsonArray missingArray
                ? missingArray.Select((node) => node?.GetValue<string>() ?? "").Where((key) => key.Length > 0).ToHashSet(StringComparer.Ordinal)
                : assetKeys.ToHashSet(StringComparer.Ordinal);
            await RegisterPreviewAssetsAsync(assetKeys.Where(missingKeys.Contains));
            var result = await WebView.InvokeScript($$"""
                (() => {
                  if (typeof window.mockupsSetPreviewBody !== "function") return false;
                  return window.mockupsSetPreviewBody({{bodyJson}});
                })();
                """);
            var patchId = PatchId(result);
            PreviewDebugLog.Write(
                "preview.webview.dom-patch",
                ("success", true),
                ("ms", stopwatch.Elapsed.TotalMilliseconds),
                ("bodyChars", bodyContent.Length),
                ("originalBodyChars", bodyContent.Length),
                ("assets", assetKeys.Count),
                ("newAssets", missingKeys.Count),
                ("patch", patchId),
                ("wait", waitForCommit));
            if (waitForCommit)
            {
                return await WaitForPatchCommitAsync(patchId);
            }

            _ = DrainPreviewPatchEventsAsync();
            return true;
        }
        catch (Exception error)
        {
            PreviewDebugLog.Write(
                "preview.webview.dom-patch",
                ("success", false),
                ("ms", stopwatch.Elapsed.TotalMilliseconds),
                ("bodyChars", bodyContent.Length),
                ("error", error.Message));
            return false;
        }
    }

    protected async Task<bool> ReplacePreviewFontStylesAsync(string fontStyleHtml)
    {
        var fontStylesJson = JsonSerializer.Serialize(InlineFontFileUris(fontStyleHtml));
        try
        {
            var result = await WebView.InvokeScript($$"""
                (() => {
                  if (typeof window.mockupsSetPreviewFontStyles !== "function") return false;
                  return window.mockupsSetPreviewFontStyles({{fontStylesJson}});
                })();
                """);
            return result is not null && !string.Equals(result.ToString(), "false", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error)
        {
            PreviewDebugLog.Write(
                "preview.webview.font-styles",
                ("success", false),
                ("fontStyleChars", fontStyleHtml.Length),
                ("error", error.Message));
            return false;
        }
    }

    private async Task RegisterPreviewAssetsAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys.Distinct(StringComparer.Ordinal))
        {
            if (!PreviewAssetRegistry.TryResolve(key, out var uri))
            {
                throw new InvalidOperationException($"Preview asset registry has no value for '{key}'.");
            }
            var keyJson = JsonSerializer.Serialize(key);
            var uriJson = JsonSerializer.Serialize(uri);
            await WebView.InvokeScript($$"""
                (() => {
                  if (typeof window.mockupsRegisterPreviewAsset !== "function") return false;
                  return window.mockupsRegisterPreviewAsset({{keyJson}}, {{uriJson}});
                })();
                """);
            PreviewDebugLog.Write(
                "preview.webview.asset.register",
                ("hash", key),
                ("mimeType", AssetMimeType(uri)),
                ("uriChars", uri.Length));
        }
    }

    private static string AssetMimeType(string dataUri)
    {
        var separator = dataUri.IndexOfAny([';', ',']);
        return separator > "data:".Length
            ? dataUri["data:".Length..separator]
            : "";
    }

    protected async Task<int> PreloadPreviewImagesAsync(
        IReadOnlyCollection<string> imageSources,
        CancellationToken cancellationToken)
    {
        if (imageSources.Count == 0) return 0;

        var stopwatch = Stopwatch.StartNew();
        var loadedTotal = 0;
        var requestedTotal = 0;
        try
        {
            var preloadAssetKeys = imageSources
                .Where((source) => source.StartsWith("mockups-asset:", StringComparison.Ordinal))
                .Select((source) => source["mockups-asset:".Length..])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (preloadAssetKeys.Length > 0)
            {
                var keysJson = JsonSerializer.Serialize(preloadAssetKeys);
                var missingResult = await WebView.InvokeScript($$"""
                    (() => typeof window.mockupsMissingPreviewAssets === "function"
                      ? window.mockupsMissingPreviewAssets({{keysJson}})
                      : JSON.stringify({{keysJson}}))();
                    """);
                var missingJson = missingResult?.ToString() ?? "[]";
                var missingKeys = JsonNode.Parse(string.IsNullOrWhiteSpace(missingJson) ? "[]" : missingJson) is JsonArray missingArray
                    ? missingArray.Select((node) => node?.GetValue<string>() ?? "").Where((key) => key.Length > 0)
                    : preloadAssetKeys;
                await RegisterPreviewAssetsAsync(missingKeys);
            }
            foreach (var batch in imageSources.Chunk(10))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batchStopwatch = Stopwatch.StartNew();
                var sourcesJson = JsonSerializer.Serialize(batch);
                requestedTotal += batch.Length;
                var result = await WebView.InvokeScript($$"""
                    (() => {
                      if (typeof window.mockupsPreloadPreviewImages !== "function") return "";
                      return window.mockupsPreloadPreviewImages({{sourcesJson}});
                    })();
                    """);
                var requestId = result?.ToString() ?? "";
                var loaded = await WaitForPreviewImagePreloadAsync(requestId, cancellationToken);
                loadedTotal += loaded;
                PreviewDebugLog.Write(
                    "preview.webview.preload-images.batch",
                    ("requested", batch.Length),
                    ("loaded", loaded),
                    ("ms", batchStopwatch.Elapsed.TotalMilliseconds),
                    ("totalRequested", requestedTotal),
                    ("totalLoaded", loadedTotal));
            }

            PreviewDebugLog.Write(
                "preview.webview.preload-images",
                ("requested", requestedTotal),
                ("loaded", loadedTotal),
                ("ms", stopwatch.Elapsed.TotalMilliseconds));
            return loadedTotal;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            PreviewDebugLog.Write(
                "preview.webview.preload-images.error",
                ("requested", requestedTotal),
                ("ms", stopwatch.Elapsed.TotalMilliseconds),
                ("error", error.Message));
            return 0;
        }
    }

    private async Task<int> WaitForPreviewImagePreloadAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return 0;

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 5000)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(16, cancellationToken);
            var requestJson = JsonSerializer.Serialize(requestId);
            var result = await WebView.InvokeScript($$"""
                (() => {
                  if (typeof window.mockupsPreviewImagePreloadResult !== "function") return "";
                  return window.mockupsPreviewImagePreloadResult({{requestJson}});
                })();
                """);
            var resultJson = result?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(resultJson)) continue;
            if (JsonNode.Parse(resultJson) is not JsonObject state
                || state["done"]?.GetValue<bool>() != true)
            {
                continue;
            }

            return state["loaded"]?.GetValue<int>() ?? 0;
        }

        PreviewDebugLog.Write(
            "preview.webview.preload-images.timeout",
            ("requestId", requestId),
            ("ms", stopwatch.Elapsed.TotalMilliseconds));
        return 0;
    }

    protected static IEnumerable<string> PreviewImageSources(string html)
    {
        var matches = Regex.Matches(
            html,
            "<img\\b[^>]*\\bsrc=(?<quote>[\"'])(?<src>.*?)\\k<quote>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match match in matches)
        {
            var source = WebUtility.HtmlDecode(match.Groups["src"].Value);
            if (!string.IsNullOrWhiteSpace(source))
            {
                yield return source;
            }
        }
    }

    private async Task<bool> WaitForPatchCommitAsync(int patchId)
    {
        if (patchId <= 0) return false;

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 5000)
        {
            await Task.Delay(16);
            string status;
            try
            {
                var result = await WebView.InvokeScript($$"""
                    (() => typeof window.mockupsPreviewPatchStatus === "function"
                      ? window.mockupsPreviewPatchStatus({{patchId}})
                      : "")();
                    """);
                status = result?.ToString() ?? "";
            }
            catch
            {
                status = "";
            }

            _ = await DrainPreviewPatchEventsOnceAsync();
            if (status == "commit")
            {
                PreviewDebugLog.Write(
                    "preview.webview.dom-patch.wait",
                    ("patch", patchId),
                    ("result", "commit"),
                    ("ms", stopwatch.Elapsed.TotalMilliseconds));
                return true;
            }

            if (status is "skip" or "stale")
            {
                PreviewDebugLog.Write(
                    "preview.webview.dom-patch.wait",
                    ("patch", patchId),
                    ("result", status),
                    ("ms", stopwatch.Elapsed.TotalMilliseconds));
                return false;
            }
        }

        PreviewDebugLog.Write(
            "preview.webview.dom-patch.wait",
            ("patch", patchId),
            ("result", "timeout"),
            ("ms", stopwatch.Elapsed.TotalMilliseconds));
        return false;
    }

    private async Task DrainPreviewPatchEventsAsync()
    {
        foreach (var delay in new[] { 20, 90, 560 })
        {
            await Task.Delay(delay);
            _ = await DrainPreviewPatchEventsOnceAsync();
        }
    }

    private async Task<IReadOnlyList<JsonObject>> DrainPreviewPatchEventsOnceAsync()
    {
        try
        {
            var result = await WebView.InvokeScript("""
                (() => {
                  if (typeof window.mockupsDrainPreviewPatchEvents !== "function") return "[]";
                  return window.mockupsDrainPreviewPatchEvents();
                })();
                """);
            var json = result?.ToString() ?? "[]";
            if (JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json) is not JsonArray events)
            {
                return [];
            }

            var parsed = events.OfType<JsonObject>().ToList();
            foreach (var node in parsed)
            {
                PreviewDebugLog.Write(
                    "preview.webview.patch-event",
                    ("event", JsonText(node, "event")),
                    ("patch", JsonText(node, "patch")),
                    ("images", JsonText(node, "images")),
                    ("status", JsonText(node, "status")),
                    ("ms", JsonText(node, "ms")),
                    ("srcChars", JsonText(node, "srcChars")),
                    ("renderableId", JsonText(node, "renderableId")),
                    ("assetHash", JsonText(node, "assetHash")),
                    ("srcKind", JsonText(node, "srcKind")),
                    ("source", JsonText(node, "source")),
                    ("naturalWidth", JsonText(node, "naturalWidth")),
                    ("naturalHeight", JsonText(node, "naturalHeight")));
            }

            return parsed;
        }
        catch (Exception error)
        {
            PreviewDebugLog.Write(
                "preview.webview.patch-event.error",
                ("error", error.Message));
            return [];
        }
    }

    private static int PatchId(object? value)
    {
        return int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var patchId)
            ? patchId
            : 0;
    }

    private static string JsonText(JsonObject owner, string key)
    {
        return owner.TryGetPropertyValue(key, out var value) ? value?.ToJsonString() ?? "" : "";
    }

    protected static string DeviceHtml(
        SpikeDatabase.DevicePreviewMetrics metrics,
        bool isDark,
        string themeName,
        string themeMode,
        string scaleMode,
        string previewMode,
        bool showDesignMarks,
        bool showDeviceFrame,
        string bodyContent,
        string fontStyleHtml = "",
        PreviewReferenceOverlay? reference = null,
        PreviewContextState? initialContextState = null)
    {
        reference ??= new PreviewReferenceOverlay("", "preview", 0.5, 1, 0);
        var width = Math.Max(1, metrics.CanvasWidth);
        var height = Math.Max(1, metrics.CanvasHeight);
        var cornerRadius = Math.Max(0, metrics.CornerRadius);
        var panelText = isDark ? "#E8EEF8" : "#172033";
        var mutedText = isDark ? "#9FB1CA" : "#64748B";
        var panelBackground = isDark
            ? "linear-gradient(155deg, rgba(18,30,48,.88), rgba(34,51,73,.72))"
            : "linear-gradient(155deg, rgba(241,246,252,.96), rgba(222,232,244,.82))";
        var frameBorder = isDark ? "#111827" : "#263142";
        var screenBackground = themeMode == "dark" ? "#101827" : "#F7F9FC";
        var initialContextVisible = initialContextState is not null;
        var initialActionVisible = initialContextState?.HasAction == true;
        var initialAction = initialActionVisible
            ? $"invokeCSharpAction('mockups-preview-action:{Html(initialContextState!.ActionTargetId)}')"
            : "";

        return $$"""
            <!doctype html>
            <html>
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <style>
                :root {
                  --preview-render-width: {{Number(width)}};
                  --preview-render-height: {{Number(height)}};
                  --preview-frame-radius: 0px;
                  --preview-frame-border: 10px;
                  --preview-frame-shadow-y: 10px;
                  --preview-frame-shadow-blur: 28px;
                  --preview-frame-border-color: {{frameBorder}};
                  --preview-screen-background: {{screenBackground}};
                }

                * {
                  box-sizing: border-box;
                }

                html,
                body {
                  width: 100%;
                  height: 100%;
                  margin: 0;
                  overflow: hidden;
                  background: transparent;
                  color: {{panelText}};
                  font-family: Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
                }

                .preview-viewport-host {
                  position: relative;
                  width: 100vw;
                  height: 100vh;
                  display: grid;
                  place-items: center;
                  overflow: hidden;
                  padding: 18px;
                  background: {{panelBackground}};
                }

                .preview-viewport {
                  position: relative;
                  overflow: hidden;
                  flex: 0 0 auto;
                  width: 1px;
                  height: 1px;
                  border: 0;
                  border-radius: 0;
                  background: transparent;
                  box-shadow: none;
                  touch-action: none;
                }

                .preview-viewport.is-draggable {
                  cursor: grab;
                }

                .preview-viewport.is-dragging {
                  cursor: grabbing;
                }

                .preview-scale {
                  position: absolute;
                  left: 0;
                  top: 0;
                  width: {{Number(width)}}px;
                  height: {{Number(height)}}px;
                  transform-origin: top left;
                  overflow: hidden;
                  background: var(--preview-screen-background);
                }

                .preview-phone-frame {
                  position: absolute;
                  inset: 0;
                  z-index: 20;
                  pointer-events: none;
                  border: var(--preview-frame-border) solid var(--preview-frame-border-color);
                  border-radius: var(--preview-frame-radius);
                  box-shadow: 0 var(--preview-frame-shadow-y) var(--preview-frame-shadow-blur) rgba(15, 23, 42, 0.28);
                }

                .preview-reference-layer {
                  position: absolute;
                  inset: 0;
                  z-index: 15;
                  overflow: hidden;
                  pointer-events: none;
                  background: var(--preview-screen-background);
                  opacity: 0;
                }

                .preview-reference-layer.is-split {
                  opacity: 1;
                  clip-path: polygon(0 0, 50% 0, 50% 100%, 0 100%);
                }

                .preview-reference-layer img {
                  width: 100%;
                  height: 100%;
                  display: block;
                  object-fit: fill;
                }

                .preview-design-marks {
                  position: absolute;
                  inset: 0;
                  z-index: 25;
                  pointer-events: none;
                  overflow: hidden;
                }

                .preview-guide {
                  position: absolute;
                  background: rgba(255, 0, 255, .5);
                }

                .preview-guide.is-vertical {
                  top: 0;
                  width: 1px;
                  height: 100%;
                }

                .preview-guide.is-horizontal {
                  left: 0;
                  width: 100%;
                  height: 1px;
                }

                .preview-guide.is-dashed {
                  background: none;
                  background-image: linear-gradient(to bottom, rgba(255, 0, 255, .5) 0 50%, transparent 50% 100%);
                  background-size: 1px 12px;
                }

                .preview-guide.is-horizontal.is-dashed {
                  background-image: linear-gradient(to right, rgba(255, 0, 255, .5) 0 50%, transparent 50% 100%);
                  background-size: 12px 1px;
                }

                .preview-design-safe-margin {
                  position: absolute;
                  z-index: 1;
                  box-sizing: border-box;
                  pointer-events: none;
                  border: 1px solid rgba(48, 151, 255, .84);
                  box-shadow: 0 0 0 1px rgba(48, 151, 255, .16);
                }

                .preview-placeholder {
                  width: 100%;
                  height: 100%;
                  display: grid;
                  place-items: center;
                  padding: 28px;
                  text-align: center;
                }

                .preview-placeholder-card {
                  max-width: 72%;
                  padding: 16px 18px;
                  border: 1px solid rgba(148, 163, 184, .34);
                  border-radius: 18px;
                  background: {{(themeMode == "dark" ? "rgba(15,23,42,.72)" : "rgba(255,255,255,.78)")}};
                  box-shadow: 0 12px 28px rgba(15, 23, 42, .12);
                }

                .preview-placeholder-title {
                  margin: 0 0 6px;
                  font-size: 15px;
                  font-weight: 760;
                  color: {{(themeMode == "dark" ? "#E6E6E6" : "#1A1A1A")}};
                }

                .preview-placeholder-text {
                  margin: 0;
                  font-size: 12px;
                  line-height: 1.45;
                  color: {{(themeMode == "dark" ? "#CBD5E1" : "#475569")}};
                }

                .preview-meta {
                  position: absolute;
                  left: 50%;
                  bottom: 6px;
                  z-index: 30;
                  transform: translateX(-50%);
                  padding: 4px 8px;
                  border-radius: 999px;
                  background: rgba(15, 23, 42, .58);
                  color: #F8FAFC;
                  font-size: 10px;
                  letter-spacing: .04em;
                  white-space: nowrap;
                }

                .preview-meta strong {
                  font-weight: 760;
                }

                .preview-meta span {
                  color: {{mutedText}};
                }

                .preview-context-state {
                  position: absolute;
                  right: 14px;
                  bottom: 14px;
                  z-index: 2000;
                  display: none;
                  max-width: min(380px, calc(100% - 28px));
                  gap: 8px;
                  align-items: center;
                  padding: 10px 12px;
                  border: 1px solid rgba(148, 163, 184, .42);
                  border-radius: 12px;
                  background: {{(isDark ? "rgba(20,27,37,.94)" : "rgba(255,255,255,.96)")}};
                  color: {{panelText}};
                  box-shadow: 0 10px 26px rgba(15, 23, 42, .22);
                  pointer-events: none;
                }

                .preview-context-state.is-visible { display: grid; grid-template-columns: auto 1fr; }
                .preview-context-state.is-error { border-color: rgba(239, 68, 68, .58); }
                .preview-context-state.is-error { pointer-events: auto; }
                .preview-context-state-dot { width: 9px; height: 9px; border-radius: 50%; background: #2F80ED; }
                .preview-context-state.is-loading .preview-context-state-dot { animation: mockups-context-pulse 1s ease-in-out infinite alternate; }
                .preview-context-state.is-error .preview-context-state-dot { background: #EF4444; }
                .preview-context-state-title { display:block; font-size: 12px; font-weight: 760; }
                .preview-context-state-message { display:block; margin-top: 2px; color: {{mutedText}}; font-size: 12px; line-height: 1.35; }
                .preview-context-state-retry { display:none; grid-column:2; justify-self:start; min-height:30px; padding:4px 10px; border:1px solid currentColor; border-radius:8px; background:transparent; color:inherit; font:700 12px inherit; cursor:pointer; }
                .preview-context-state.is-error .preview-context-state-retry { display:block; }
                @keyframes mockups-context-pulse { from { opacity: .4; transform: scale(.8); } to { opacity: 1; transform: scale(1); } }

                .preview-non-renderable-state {
                  position: absolute;
                  inset: 0;
                  z-index: 2100;
                  display: none;
                  place-items: center;
                  padding: 28px;
                  background: {{(isDark ? "#171C24" : "#EEF2F7")}};
                  text-align: center;
                }
                .preview-non-renderable-state.is-visible { display: grid; }
                .preview-non-renderable-state.is-loading .preview-non-renderable-activity { display:inline-block; }
                .preview-non-renderable-card { width:min(520px,100%); padding:28px; border-radius:18px; background:{{(isDark ? "#252D38" : "#FFFFFF")}}; box-shadow:0 16px 36px rgba(0,0,0,.16); }
                .preview-non-renderable-activity { display:none; width:10px; height:10px; margin-bottom:12px; border-radius:50%; background:#2F80ED; animation:mockups-context-pulse 1s ease-in-out infinite alternate; }
                .preview-non-renderable-title { margin:0 0 10px; font-size:20px; font-weight:760; }
                .preview-non-renderable-message { margin:0; color:{{mutedText}}; font-size:14px; line-height:1.5; }
                .preview-non-renderable-action { display:none; margin-top:20px; min-height:40px; padding:8px 16px; border:1px solid #D89A16; border-radius:10px; background:#F0B429; color:#17120A; font:700 14px inherit; cursor:pointer; }
                .preview-non-renderable-action.is-visible { display:inline-grid; place-items:center; }
              </style>
              <style id="mockupsProductionFontStyles">{{FontStylesCss(fontStyleHtml)}}</style>
            </head>
            <body>
              <main class="preview-viewport-host">
                <section class="preview-viewport" id="previewViewport">
                  <div class="preview-scale" id="previewScale">
                    {{bodyContent}}
                  </div>
                  <div aria-hidden="true" class="preview-reference-layer" id="previewReferenceLayer"><img id="previewReferenceImage" alt=""></div>
                  {{DesignMarksHtml(showDesignMarks, width, height, metrics.DesignSafeMarginCoefficient)}}
                  {{(showDeviceFrame ? "<div aria-hidden=\"true\" class=\"preview-phone-frame\" id=\"previewPhoneFrame\"></div>" : "")}}
                  {{PreviewMetaHtml(showDesignMarks, previewMode, metrics.Name, themeName, themeMode)}}
                </section>
                <div class="preview-context-state" id="previewContextState" role="status" aria-live="polite">
                  <span class="preview-context-state-dot" aria-hidden="true"></span>
                  <span><span class="preview-context-state-title" id="previewContextStateTitle"></span><span class="preview-context-state-message" id="previewContextStateMessage"></span></span>
                  <button class="preview-context-state-retry" type="button" onclick="invokeCSharpAction('mockups-preview-action:__preview_retry__')">Reintentar</button>
                </div>
                <div class="preview-non-renderable-state{{(initialContextVisible ? " is-visible" : "")}}{{(initialContextState?.Kind == PreviewContextStateKind.Loading ? " is-loading" : "")}}" id="previewNonRenderableState">
                  <div class="preview-non-renderable-card" role="status">
                    <span class="preview-non-renderable-activity" aria-hidden="true"></span>
                    <h1 class="preview-non-renderable-title" id="previewNonRenderableTitle">{{Html(initialContextState?.Title ?? "")}}</h1>
                    <p class="preview-non-renderable-message" id="previewNonRenderableMessage">{{Html(initialContextState?.Message ?? "")}}</p>
                    <button class="preview-non-renderable-action{{(initialActionVisible ? " is-visible" : "")}}" id="previewNonRenderableAction" type="button"{{(initialActionVisible ? $" onclick=\"{initialAction}\"" : "")}}>{{Html(initialContextState?.ActionLabel ?? "")}}</button>
                  </div>
                </div>
              </main>
              <script>
                const host = document.querySelector(".preview-viewport-host");
                const viewport = document.getElementById("previewViewport");
                const scaleLayer = document.getElementById("previewScale");
                const frame = document.getElementById("previewPhoneFrame");
                const referenceLayer = document.getElementById("previewReferenceLayer");
                const referenceImage = document.getElementById("previewReferenceImage");
                const designMarks = document.querySelector(".preview-design-marks");
                const previewMeta = document.querySelector(".preview-meta");
                const productionFontStyles = document.getElementById("mockupsProductionFontStyles");
                const contextualState = document.getElementById("previewContextState");
                const contextualStateTitle = document.getElementById("previewContextStateTitle");
                const contextualStateMessage = document.getElementById("previewContextStateMessage");
                const nonRenderableState = document.getElementById("previewNonRenderableState");
                const nonRenderableTitle = document.getElementById("previewNonRenderableTitle");
                const nonRenderableMessage = document.getElementById("previewNonRenderableMessage");
                const nonRenderableAction = document.getElementById("previewNonRenderableAction");
                let contextualStateTimer = 0;
                const renderWidth = {{Number(width)}};
                const renderHeight = {{Number(height)}};
                const cornerRadius = {{Number(cornerRadius)}};
                const scaleMode = "{{Html(PreviewScaleMode.WebMode(scaleMode))}}";
                const previewStateKey = `mockups.preview.{{Html(previewMode)}}.${renderWidth}x${renderHeight}.center`;
                let translateX = 0;
                let translateY = 0;
                let dragStartX = 0;
                let dragStartY = 0;
                let startTranslateX = 0;
                let startTranslateY = 0;
                let isDragging = false;

                window.mockupsSetContextualPreviewState = (kind, title, message) => {
                  window.clearTimeout(contextualStateTimer);
                  contextualStateTimer = 0;
                  if (!contextualState) return false;
                  const show = () => {
                    contextualState.className = `preview-context-state is-visible is-${kind}`;
                    contextualStateTitle.textContent = String(title ?? "");
                    contextualStateMessage.textContent = String(message ?? "");
                  };
                  if (!kind) {
                    contextualState.className = "preview-context-state";
                    contextualStateTitle.textContent = "";
                    contextualStateMessage.textContent = "";
                    return true;
                  }
                  if (kind === "loading") contextualStateTimer = window.setTimeout(show, 140);
                  else show();
                  return true;
                };

                window.mockupsSetNonRenderablePreviewState = (title, message, actionLabel, actionTarget, kind = "non-renderable") => {
                  if (!nonRenderableState || !nonRenderableAction) return false;
                  const visible = Boolean(title || message);
                  nonRenderableState.classList.toggle("is-visible", visible);
                  nonRenderableState.classList.toggle("is-loading", kind === "loading");
                  nonRenderableTitle.textContent = String(title ?? "");
                  nonRenderableMessage.textContent = String(message ?? "");
                  nonRenderableAction.textContent = String(actionLabel ?? "");
                  nonRenderableAction.classList.toggle("is-visible", Boolean(actionLabel && actionTarget));
                  nonRenderableAction.onclick = actionLabel && actionTarget
                    ? () => invokeCSharpAction(`mockups-preview-action:${actionTarget}`)
                    : null;
                  return true;
                };

                function applyReferenceOverlay(state) {
                  if (!referenceLayer || !referenceImage) return;
                  const mode = state?.viewMode === "split" ? "split" : "preview";
                  const rawSwipe = Number(state?.swipe);
                  const rawOpacity = Number(state?.opacity);
                  const rawAngle = Number(state?.angle);
                  const swipe = Math.max(0, Math.min(1, Number.isFinite(rawSwipe) ? rawSwipe : 0.5));
                  const opacity = Math.max(0, Math.min(1, Number.isFinite(rawOpacity) ? rawOpacity : 0));
                  const angle = Math.max(-80, Math.min(80, Number.isFinite(rawAngle) ? rawAngle : 0));
                  const slope = Math.tan(angle * Math.PI / 180) * 50;
                  const top = Math.max(-30, Math.min(130, swipe * 100 - slope));
                  const bottom = Math.max(-30, Math.min(130, swipe * 100 + slope));
                  referenceLayer.classList.toggle("is-split", mode === "split");
                  referenceLayer.style.opacity = mode === "split" ? String(opacity) : "0";
                  referenceLayer.style.clipPath = `polygon(0 0, ${top}% 0, ${bottom}% 100%, 0 100%)`;
                  referenceImage.src = state?.sourceUri || "";
                }

                window.mockupsSetReferenceOverlay = (state) => {
                  applyReferenceOverlay(state);
                  return true;
                };
                window.mockupsSetDesignMarks = (visible) => {
                  if (designMarks) designMarks.style.display = visible ? "block" : "none";
                  if (previewMeta) previewMeta.style.display = visible ? "block" : "none";
                  return true;
                };
                applyReferenceOverlay({
                  sourceUri: "{{Html(reference.SourceUri)}}",
                  viewMode: "{{Html(reference.ViewMode)}}",
                  swipe: {{Number(reference.Swipe)}},
                  opacity: {{Number(reference.Opacity)}},
                  angle: {{Number(reference.Angle)}},
                });

                function fixedScale() {
                  if (scaleMode === "fit") return null;
                  const parsed = Number.parseFloat(scaleMode);
                  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
                }

                function applyViewportTransform() {
                  viewport.style.transform = `translate(${translateX}px, ${translateY}px)`;
                }

                function savedCenter() {
                  try {
                    const parsed = JSON.parse(localStorage.getItem(previewStateKey) ?? "null");
                    if (
                      parsed &&
                      Number.isFinite(parsed.x) &&
                      Number.isFinite(parsed.y)
                    ) {
                      return parsed;
                    }
                  } catch {
                  }
                  return { x: renderWidth / 2, y: renderHeight / 2 };
                }

                function saveCenter(scale, displayWidth, displayHeight) {
                  if (fixedScale() === null) return;
                  const center = {
                    x: (displayWidth / 2 - translateX) / scale,
                    y: (displayHeight / 2 - translateY) / scale,
                  };
                  localStorage.setItem(previewStateKey, JSON.stringify(center));
                }

                function calculatePreviewFit() {
                  const availableWidth = Math.max(1, host.clientWidth - 36);
                  const availableHeight = Math.max(1, host.clientHeight - 36);
                  const explicitScale = fixedScale();
                  const scale = explicitScale ?? Math.min(availableWidth / renderWidth, availableHeight / renderHeight);
                  const displayWidth = Math.max(1, Math.round(renderWidth * scale));
                  const displayHeight = Math.max(1, Math.round(renderHeight * scale));
                  viewport.style.width = `${displayWidth}px`;
                  viewport.style.height = `${displayHeight}px`;
                  viewport.classList.toggle("is-draggable", explicitScale !== null);
                  scaleLayer.style.transform = `scale(${scale})`;
                  scaleLayer.style.borderRadius = `${cornerRadius}px`;
                  if (frame) {
                    frame.style.setProperty("--preview-frame-border", `${Math.max(1, 10 * scale)}px`);
                    frame.style.setProperty("--preview-frame-radius", `${cornerRadius * scale}px`);
                    frame.style.setProperty("--preview-frame-shadow-y", `${10 * scale}px`);
                    frame.style.setProperty("--preview-frame-shadow-blur", `${28 * scale}px`);
                  }
                  if (explicitScale === null) {
                    translateX = 0;
                    translateY = 0;
                  } else if (!isDragging) {
                    const center = savedCenter();
                    translateX = displayWidth / 2 - center.x * scale;
                    translateY = displayHeight / 2 - center.y * scale;
                  }
                  applyViewportTransform();
                }

                viewport.addEventListener("pointerdown", (event) => {
                  if (fixedScale() === null || event.button !== 0) return;
                  isDragging = true;
                  dragStartX = event.clientX;
                  dragStartY = event.clientY;
                  startTranslateX = translateX;
                  startTranslateY = translateY;
                  viewport.classList.add("is-dragging");
                  viewport.setPointerCapture(event.pointerId);
                  event.preventDefault();
                });

                viewport.addEventListener("pointermove", (event) => {
                  if (!isDragging) return;
                  translateX = startTranslateX + event.clientX - dragStartX;
                  translateY = startTranslateY + event.clientY - dragStartY;
                  applyViewportTransform();
                  const scale = fixedScale();
                  if (scale !== null) {
                    saveCenter(scale, renderWidth * scale, renderHeight * scale);
                  }
                  event.preventDefault();
                });

                function stopDragging(event) {
                  if (!isDragging) return;
                  isDragging = false;
                  viewport.classList.remove("is-dragging");
                  if (event.pointerId !== undefined && viewport.hasPointerCapture(event.pointerId)) {
                    viewport.releasePointerCapture(event.pointerId);
                  }
                }

                viewport.addEventListener("pointerup", stopDragging);
                viewport.addEventListener("pointercancel", stopDragging);

                const resizeObserver = new ResizeObserver(calculatePreviewFit);
                resizeObserver.observe(host);
                window.addEventListener("resize", calculatePreviewFit);
                let previewBodyPatchSequence = 0;
                window.mockupsPreviewPatchEvents = [];
                const previewPatchStatuses = new Map();
                window.mockupsPreviewPatchStatus = (patch) => previewPatchStatuses.get(patch) ?? "";
                window.mockupsDrainPreviewPatchEvents = () => {
                  const events = window.mockupsPreviewPatchEvents || [];
                  window.mockupsPreviewPatchEvents = [];
                  return JSON.stringify(events);
                };

                function recordPatchEvent(event, detail = {}) {
                  if ((event === "commit" || event === "skip" || event === "stale")
                    && Number.isFinite(detail.patch)) {
                    previewPatchStatuses.set(detail.patch, event);
                    while (previewPatchStatuses.size > 160) {
                      previewPatchStatuses.delete(previewPatchStatuses.keys().next().value);
                    }
                  }
                  const events = window.mockupsPreviewPatchEvents || [];
                  events.push({
                    event,
                    time: Math.round(performance.now() * 1000) / 1000,
                    ...detail,
                  });
                  window.mockupsPreviewPatchEvents = events.slice(-160);
                }

                function imageDescriptor(image) {
                  const owner = image?.closest?.("[data-renderable-id]");
                  const source = image?.getAttribute?.("src") ?? "";
                  return {
                    renderableId: owner?.getAttribute("data-renderable-id") ?? "",
                    assetHash: image?.dataset?.previewAssetHash ?? "",
                    srcKind: source.startsWith("data:")
                      ? "data"
                      : source.startsWith("blob:")
                        ? "blob"
                        : source.startsWith("mockups-asset:")
                          ? "unresolved"
                          : source.startsWith("http:") || source.startsWith("https:")
                            ? "http"
                            : source.startsWith("file:")
                              ? "file"
                              : source ? "other" : "empty",
                  };
                }

                function imageLoadPromise(image, patchId, startedAt) {
                  if (!image || !image.src || image.complete && image.naturalWidth > 0) {
                    recordPatchEvent("image", {
                      patch: patchId,
                      status: "ready",
                      ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                      srcChars: image?.src?.length ?? 0,
                      naturalWidth: image?.naturalWidth ?? 0,
                      naturalHeight: image?.naturalHeight ?? 0,
                      ...imageDescriptor(image),
                    });
                    return Promise.resolve("ready");
                  }

                  return new Promise((resolve) => {
                    let done = false;
                    const finish = (status) => {
                      if (done) return;
                      done = true;
                      recordPatchEvent("image", {
                        patch: patchId,
                        status,
                        ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                        srcChars: image.src.length,
                        naturalWidth: image.naturalWidth,
                        naturalHeight: image.naturalHeight,
                        ...imageDescriptor(image),
                      });
                      resolve(status);
                    };
                    image.addEventListener("load", () => finish("load"), { once: true });
                    image.addEventListener("error", () => finish("error"), { once: true });
                    if (typeof image.decode === "function") {
                      image.decode().then(
                        () => finish("decode"),
                        () => recordPatchEvent("image-decode-rejected", {
                          patch: patchId,
                          ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                          ...imageDescriptor(image),
                        }),
                      );
                    }
                    setTimeout(() => finish("timeout"), 1500);
                  });
                }

                let previewImagePreloadSequence = 0;
                const previewImagePreloadResults = new Map();
                window.mockupsPreloadPreviewImages = (sources) => {
                  const startedAt = performance.now();
                  const requestId = String(++previewImagePreloadSequence);
                  let pool = document.getElementById("mockupsPreviewImagePool");
                  if (!pool) {
                    pool = document.createElement("div");
                    pool.id = "mockupsPreviewImagePool";
                    pool.style.position = "absolute";
                    pool.style.left = "-100000px";
                    pool.style.top = "-100000px";
                    pool.style.width = "1px";
                    pool.style.height = "1px";
                    pool.style.overflow = "hidden";
                    pool.style.opacity = "0";
                    pool.style.pointerEvents = "none";
                    document.body.appendChild(pool);
                  }

                  pool.replaceChildren();
                  const uniqueSources = [...new Set(Array.isArray(sources) ? sources : [])]
                    .map((source) => {
                      const value = String(source);
                      return value.startsWith("mockups-asset:")
                        ? previewAssets.get(value.slice("mockups-asset:".length)) ?? value
                        : value;
                    });
                  const images = uniqueSources.map((source) => {
                    const image = new Image();
                    image.decoding = "sync";
                    image.loading = "eager";
                    image.src = source;
                    pool.appendChild(image);
                    return image;
                  });
                  previewImagePreloadResults.set(requestId, { done: false, loaded: 0 });
                  Promise.all(images.map((image) => imageLoadPromise(image, `preload:${requestId}`, startedAt))).then(() => {
                    const loaded = images.filter((image) => image.complete && image.naturalWidth > 0).length;
                    previewImagePreloadResults.set(requestId, { done: true, loaded });
                    recordPatchEvent("preload-images", {
                      patch: `preload:${requestId}`,
                      images: images.length,
                      loaded,
                      ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                    });
                  });
                  return requestId;
                };
                window.mockupsPreviewImagePreloadResult = (requestId) => {
                  const key = String(requestId);
                  const result = previewImagePreloadResults.get(key);
                  if (!result) return "";
                  if (result.done) previewImagePreloadResults.delete(key);
                  return JSON.stringify(result);
                };

                const previewRasterResults = new Map();
                const previewRasterFrames = new Map();
                let previewRasterSequence = 0;
                window.mockupsCapturePreviewRaster = () => {
                  const requestId = String(++previewRasterSequence);
                  previewRasterResults.set(requestId, { done: false });
                  Promise.resolve().then(async () => {
                    try {
                      if (document.fonts?.ready) await document.fonts.ready;
                      const root = scaleLayer.firstElementChild;
                      if (!root) throw new Error("Preview has no render root");
                      const css = [...document.querySelectorAll("style")]
                        .map((style) => style.textContent ?? "")
                        .join("\n");
                      const xhtmlNamespace = "http://www.w3.org/1999/xhtml";
                      const wrapper = document.createElementNS(xhtmlNamespace, "div");
                      wrapper.setAttribute("style", `position:relative;width:${renderWidth}px;height:${renderHeight}px;overflow:hidden`);
                      const style = document.createElementNS(xhtmlNamespace, "style");
                      style.textContent = css;
                      wrapper.appendChild(style);
                      wrapper.appendChild(root.cloneNode(true));
                      const content = new XMLSerializer().serializeToString(wrapper);
                      const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${renderWidth}" height="${renderHeight}"><foreignObject width="100%" height="100%">${content}</foreignObject></svg>`;
                      const blobUrl = URL.createObjectURL(new Blob([svg], { type: "image/svg+xml;charset=utf-8" }));
                      try {
                        const image = new Image();
                        await new Promise((resolve, reject) => {
                          image.onload = resolve;
                          image.onerror = () => reject(new Error("Raster SVG image failed to load"));
                          image.src = blobUrl;
                        });
                        image.style.position = "absolute";
                        image.style.inset = "0";
                        image.style.width = `${renderWidth}px`;
                        image.style.height = `${renderHeight}px`;
                        image.style.display = "none";
                        image.style.pointerEvents = "none";
                        previewRasterDeck.appendChild(image);
                        previewRasterFrames.set(requestId, { blobUrl, image });
                        previewRasterResults.set(requestId, { done: true, rasterId: requestId });
                      } catch (error) {
                        URL.revokeObjectURL(blobUrl);
                        throw error;
                      }
                    } catch (error) {
                      previewRasterResults.set(requestId, { done: true, error: String(error) });
                    }
                  });
                  return requestId;
                };
                window.mockupsPreviewRasterResult = (requestId) => {
                  const key = String(requestId);
                  const result = previewRasterResults.get(key);
                  if (!result) return "";
                  if (result.done) previewRasterResults.delete(key);
                  return JSON.stringify(result);
                };
                const previewRasterDeck = document.createElement("div");
                previewRasterDeck.id = "mockupsPreviewRasterPlayback";
                previewRasterDeck.style.position = "absolute";
                previewRasterDeck.style.inset = "0";
                previewRasterDeck.style.width = `${renderWidth}px`;
                previewRasterDeck.style.height = `${renderHeight}px`;
                previewRasterDeck.style.zIndex = "900";
                previewRasterDeck.style.display = "none";
                previewRasterDeck.style.pointerEvents = "none";
                scaleLayer.appendChild(previewRasterDeck);
                let visibleRasterFrame = null;
                window.mockupsShowPreviewRaster = (rasterId) => {
                  const frame = previewRasterFrames.get(String(rasterId));
                  if (!frame) return false;
                  if (visibleRasterFrame && visibleRasterFrame !== frame.image) {
                    visibleRasterFrame.style.display = "none";
                  }
                  frame.image.style.display = "block";
                  visibleRasterFrame = frame.image;
                  previewRasterDeck.style.display = "block";
                  return true;
                };
                window.mockupsHidePreviewRaster = () => {
                  previewRasterDeck.style.display = "none";
                  return true;
                };
                let previewRasterAnimation = 0;
                window.mockupsPlayPreviewRasters = (rasterIds, framesPerSecond) => {
                  cancelAnimationFrame(previewRasterAnimation);
                  const ids = Array.isArray(rasterIds) ? rasterIds.map(String) : [];
                  if (ids.length === 0) return false;
                  const fps = Math.max(1, Number(framesPerSecond) || 1);
                  const startedAt = performance.now();
                  let shownIndex = -1;
                  const present = (now) => {
                    const index = Math.min(ids.length - 1, Math.floor((now - startedAt) * fps / 1000));
                    if (index !== shownIndex) {
                      window.mockupsShowPreviewRaster(ids[index]);
                      shownIndex = index;
                    }
                    if (index < ids.length - 1) previewRasterAnimation = requestAnimationFrame(present);
                  };
                  previewRasterAnimation = requestAnimationFrame(present);
                  return true;
                };

                const previewRasterLoading = document.createElement("div");
                previewRasterLoading.style.position = "absolute";
                previewRasterLoading.style.inset = "0";
                previewRasterLoading.style.zIndex = "1000";
                previewRasterLoading.style.display = "none";
                previewRasterLoading.style.placeItems = "center";
                previewRasterLoading.style.background = "#0f172a";
                previewRasterLoading.style.color = "#f8fafc";
                previewRasterLoading.style.font = "600 14px sans-serif";
                const previewLoadingContent = document.createElement("div");
                previewLoadingContent.style.display = "grid";
                previewLoadingContent.style.justifyItems = "center";
                previewLoadingContent.style.gap = "12px";
                const previewLoadingSpinner = document.createElement("div");
                previewLoadingSpinner.style.width = "34px";
                previewLoadingSpinner.style.height = "34px";
                previewLoadingSpinner.style.border = "3px solid rgba(248,250,252,.24)";
                previewLoadingSpinner.style.borderTopColor = "#f8fafc";
                previewLoadingSpinner.style.borderRadius = "50%";
                previewLoadingSpinner.style.animation = "mockups-preview-spin .8s linear infinite";
                const previewLoadingLabel = document.createElement("div");
                const previewLoadingStyle = document.createElement("style");
                previewLoadingStyle.textContent = "@keyframes mockups-preview-spin { to { transform: rotate(360deg); } }";
                document.head.appendChild(previewLoadingStyle);
                previewLoadingContent.append(previewLoadingSpinner, previewLoadingLabel);
                previewRasterLoading.appendChild(previewLoadingContent);
                viewport.appendChild(previewRasterLoading);
                window.mockupsSetRasterLoading = (visible, message) => {
                  previewLoadingLabel.textContent = String(message ?? "Preparing playback…");
                  previewRasterLoading.style.display = visible ? "grid" : "none";
                  return true;
                };

                const previewAssets = new Map();
                function previewBlobUrl(uri) {
                  const value = String(uri);
                  if (!value.startsWith("data:")) return value;
                  const comma = value.indexOf(",");
                  if (comma < 0) return value;
                  const header = value.slice(0, comma);
                  const encoded = value.slice(comma + 1);
                  const mime = /^data:([^;,]+)/.exec(header)?.[1] ?? "application/octet-stream";
                  const bytes = header.includes(";base64")
                    ? Uint8Array.from(atob(encoded), (character) => character.charCodeAt(0))
                    : new TextEncoder().encode(decodeURIComponent(encoded));
                  return URL.createObjectURL(new Blob([bytes], { type: mime }));
                }
                window.mockupsRegisterPreviewAsset = (key, uri) => {
                  const normalizedKey = String(key);
                  if (!previewAssets.has(normalizedKey)) previewAssets.set(normalizedKey, previewBlobUrl(uri));
                  return true;
                };
                window.mockupsMissingPreviewAssets = (keys) => JSON.stringify(
                  (Array.isArray(keys) ? keys : []).filter((key) => !previewAssets.has(String(key))),
                );

                function hydratePreviewAssets(root) {
                  const missing = [];
                  for (const element of root.querySelectorAll("[src], [style]")) {
                    if (element.hasAttribute("src")) {
                      const source = element.getAttribute("src") ?? "";
                      if (source.startsWith("mockups-asset:")) {
                        const key = source.slice("mockups-asset:".length);
                        const uri = previewAssets.get(key);
                        if (uri) {
                          element.setAttribute("src", uri);
                          element.dataset.previewAssetHash = key;
                        } else {
                          missing.push({
                            renderableId: element.closest("[data-renderable-id]")?.getAttribute("data-renderable-id") ?? "",
                            assetHash: key,
                            source: "src",
                          });
                        }
                      }
                    }
                    const style = element.getAttribute("style") ?? "";
                    if (style.includes("mockups-asset:")) {
                      element.dataset.previewAssetStyle = style;
                      element.setAttribute("style", style.replace(/mockups-asset:([a-f0-9]{64})/g, (value, key) => {
                        const uri = previewAssets.get(key);
                        if (uri) return uri;
                        missing.push({
                          renderableId: element.closest("[data-renderable-id]")?.getAttribute("data-renderable-id") ?? "",
                          assetHash: key,
                          source: "style",
                        });
                        return value;
                      }));
                    }
                  }
                  return missing;
                }

                function syncElement(current, next) {
                  if (current.tagName !== next.tagName) return false;
                  const currentId = current.getAttribute("data-renderable-id");
                  const nextId = next.getAttribute("data-renderable-id");
                  if (currentId !== nextId) return false;
                  for (const attribute of [...current.attributes]) {
                    if (attribute.name === "data-preview-asset-hash" || attribute.name === "data-preview-asset-style") continue;
                    if (!next.hasAttribute(attribute.name)) current.removeAttribute(attribute.name);
                  }
                  for (const attribute of [...next.attributes]) {
                    if (attribute.name === "src" && attribute.value.startsWith("mockups-asset:")) {
                      const key = attribute.value.slice("mockups-asset:".length);
                      if (current.dataset.previewAssetHash === key) continue;
                      const uri = previewAssets.get(key);
                      if (!uri) return false;
                      current.setAttribute("src", uri);
                      current.dataset.previewAssetHash = key;
                      continue;
                    }
                    if (attribute.name === "style" && attribute.value.includes("mockups-asset:")) {
                      if (current.dataset.previewAssetStyle === attribute.value) continue;
                      let unresolved = false;
                      const resolvedStyle = attribute.value.replace(/mockups-asset:([a-f0-9]{64})/g, (value, key) => {
                        const uri = previewAssets.get(key);
                        if (!uri) unresolved = true;
                        return uri ?? value;
                      });
                      if (unresolved) return false;
                      current.setAttribute("style", resolvedStyle);
                      current.dataset.previewAssetStyle = attribute.value;
                      continue;
                    }
                    if (current.getAttribute(attribute.name) !== attribute.value) {
                      current.setAttribute(attribute.name, attribute.value);
                    }
                  }
                  const nextChildren = [...next.childNodes];
                  const hydratedClone = (node) => {
                    const clone = document.importNode(node, true);
                    if (clone.nodeType === Node.ELEMENT_NODE) {
                      const holder = document.createElement("div");
                      holder.appendChild(clone);
                      hydratePreviewAssets(holder);
                    }
                    return clone;
                  };
                  for (let index = 0; index < nextChildren.length; index += 1) {
                    const currentChild = current.childNodes[index];
                    const nextChild = nextChildren[index];
                    if (!currentChild) {
                      current.appendChild(hydratedClone(nextChild));
                      continue;
                    }
                    if (currentChild.nodeType !== nextChild.nodeType) {
                      current.replaceChild(hydratedClone(nextChild), currentChild);
                      continue;
                    }
                    if (currentChild.nodeType === Node.TEXT_NODE) {
                      if (currentChild.nodeValue !== nextChild.nodeValue) currentChild.nodeValue = nextChild.nodeValue;
                    } else if (currentChild.nodeType === Node.ELEMENT_NODE) {
                      if (!syncElement(currentChild, nextChild)) {
                        current.replaceChild(hydratedClone(nextChild), currentChild);
                      }
                    }
                  }
                  while (current.childNodes.length > nextChildren.length) {
                    current.lastChild?.remove();
                  }
                  return true;
                }

                window.mockupsSetPreviewFontStyles = (fontStyleHtml) => {
                  if (!productionFontStyles) return false;
                  const template = document.createElement("template");
                  template.innerHTML = fontStyleHtml ?? "";
                  const style = template.content.querySelector("style");
                  productionFontStyles.textContent = style?.textContent ?? fontStyleHtml ?? "";
                  return true;
                };

                window.mockupsSetPreviewBody = (html) => {
                  const sequence = ++previewBodyPatchSequence;
                  const startedAt = performance.now();
                  const nextLayer = document.createElement("div");
                  nextLayer.style.position = "absolute";
                  nextLayer.style.inset = "0";
                  nextLayer.style.width = "100%";
                  nextLayer.style.height = "100%";
                  nextLayer.style.opacity = "0";
                  nextLayer.style.pointerEvents = "none";
                  nextLayer.innerHTML = html;
                  const referencedAssetKeys = [...html.matchAll(/mockups-asset:([a-f0-9]{64})/g)].map((match) => match[1]);
                  const missingAssets = [...new Set(referencedAssetKeys)]
                    .filter((key) => !previewAssets.has(key))
                    .map((key) => ({ renderableId: "", assetHash: key, source: "html" }));
                  if (missingAssets.length > 0) {
                    for (const missing of missingAssets) {
                      recordPatchEvent("asset-missing", {
                        patch: sequence,
                        ...missing,
                      });
                    }
                    recordPatchEvent("skip", {
                      patch: sequence,
                      status: "asset-missing",
                      images: missingAssets.length,
                      ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                    });
                    return sequence;
                  }

                  const currentLayer = scaleLayer.firstElementChild;
                  const currentRoot = currentLayer?.hasAttribute("data-renderable-id")
                    ? currentLayer
                    : currentLayer?.querySelector("[data-renderable-id]");
                  const nextRoot = nextLayer.firstElementChild;
                  if (currentLayer
                    && currentRoot
                    && nextRoot
                    && syncElement(currentRoot, nextRoot)) {
                    calculatePreviewFit();
                    recordPatchEvent("commit", {
                      patch: sequence,
                      mode: "morph",
                      images: currentRoot.querySelectorAll("img").length,
                      ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                    });
                    return sequence;
                  }

                  hydratePreviewAssets(nextLayer);
                  scaleLayer.insertBefore(nextLayer, previewRasterDeck);

                  const images = [...nextLayer.querySelectorAll("img")];
                  recordPatchEvent("request", {
                    patch: sequence,
                    images: images.length,
                    srcChars: images.reduce((sum, image) => sum + (image.src?.length ?? 0), 0),
                  });
                  Promise.all(images.map((image) => imageLoadPromise(image, sequence, startedAt))).then((statuses) => {
                    if (sequence !== previewBodyPatchSequence) {
                      recordPatchEvent("stale", {
                        patch: sequence,
                        ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                      });
                      nextLayer.remove();
                      return;
                    }

                    const failed = statuses.some((status) =>
                      status === "error"
                      || status === "decode-error"
                      || status === "timeout");
                    if (failed) {
                      recordPatchEvent("skip", {
                        patch: sequence,
                        status: statuses.join(","),
                        ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                      });
                      nextLayer.remove();
                      return;
                    }

                    nextLayer.style.opacity = "1";
                    requestAnimationFrame(() => {
                      for (const child of [...scaleLayer.children]) {
                        if (child !== nextLayer && child !== previewRasterDeck) child.remove();
                      }
                      calculatePreviewFit();
                      recordPatchEvent("commit", {
                        patch: sequence,
                        mode: "replace",
                        images: images.length,
                        ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                      });
                    });
                  });
                  return sequence;
                };
                requestAnimationFrame(calculatePreviewFit);
              </script>
            </body>
            </html>
            """;
    }

    protected static string Placeholder(string title, string text)
    {
        return $$"""
            <div class="preview-placeholder">
              <div class="preview-placeholder-card">
                <p class="preview-placeholder-title">{{Html(title)}}</p>
                <p class="preview-placeholder-text">{{Html(text)}}</p>
              </div>
            </div>
            """;
    }

    private static string DesignMarksHtml(
        bool showDesignMarks,
        double width,
        double height,
        double safeMarginCoefficient)
    {
        var safeMargin = Math.Max(0, Math.Min(width * safeMarginCoefficient, Math.Min(width, height) * 0.5));
        var safeInsetX = width <= 0 ? 0 : safeMargin / width * 100;
        var safeInsetY = height <= 0 ? 0 : safeMargin / height * 100;
        var safeMarginGuide = safeMargin < 0.5
            ? ""
            : $$"""
                    <div aria-label="Design safe margin" class="preview-design-safe-margin" style="left:{{Number(safeInsetX)}}%; right:{{Number(safeInsetX)}}%; top:{{Number(safeInsetY)}}%; bottom:{{Number(safeInsetY)}}%"></div>
                """;

        return $$"""
                  <div aria-hidden="true" class="preview-design-marks" style="display:{{(showDesignMarks ? "block" : "none")}}">
                    <div class="preview-guide is-vertical is-dashed" style="left:25%"></div>
                    <div class="preview-guide is-vertical" style="left:50%"></div>
                    <div class="preview-guide is-vertical is-dashed" style="left:75%"></div>
                    <div class="preview-guide is-horizontal is-dashed" style="top:25%"></div>
                    <div class="preview-guide is-horizontal" style="top:50%"></div>
                    <div class="preview-guide is-horizontal is-dashed" style="top:75%"></div>
                    {{safeMarginGuide}}
                  </div>
              """;
    }

    private static string PreviewMetaHtml(
        bool showDesignMarks,
        string previewMode,
        string deviceName,
        string themeName,
        string themeMode)
    {
        return $$"""
                  <div class="preview-meta" style="display:{{(showDesignMarks ? "block" : "none")}}">
                    <strong>{{Html(previewMode)}}</strong>
                    <span> · {{Html(deviceName)}} · {{Html(themeName)}} · {{Html(themeMode)}}</span>
                  </div>
              """;
    }

    private static string FontStylesHtml(string fontStyleHtml)
    {
        return string.IsNullOrWhiteSpace(fontStyleHtml)
            ? ""
            : InlineFontFileUris(fontStyleHtml);
    }

    private static string FontStylesCss(string fontStyleHtml)
    {
        var inlined = FontStylesHtml(fontStyleHtml).Trim();
        if (!inlined.StartsWith("<style", StringComparison.OrdinalIgnoreCase)) return inlined;
        var openEnd = inlined.IndexOf('>');
        var closeStart = inlined.LastIndexOf("</style>", StringComparison.OrdinalIgnoreCase);
        return openEnd >= 0 && closeStart > openEnd
            ? inlined[(openEnd + 1)..closeStart]
            : inlined;
    }

    protected static string RasterDocumentHtml(
        SpikeDatabase.DevicePreviewMetrics metrics,
        string bodyContent,
        string fontStyleHtml)
    {
        var width = Math.Max(1, metrics.CanvasWidth);
        var height = Math.Max(1, metrics.CanvasHeight);
        return $$"""
            <!doctype html>
            <html>
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <style>
                *, *::before, *::after { box-sizing: border-box; }
                html, body {
                  width: {{Number(width)}}px;
                  height: {{Number(height)}}px;
                  margin: 0;
                  overflow: hidden;
                  background: transparent;
                }
                body { visibility: hidden; }
              </style>
              {{FontStylesHtml(fontStyleHtml)}}
            </head>
            <body>{{bodyContent}}</body>
            </html>
            """;
    }

    private static string InlineFontFileUris(string fontStyleHtml)
    {
        return Regex.Replace(
            fontStyleHtml,
            "url\\(\"(?<url>file:[^\"]+)\"\\)",
            (match) =>
            {
                try
                {
                    var fileUri = match.Groups["url"].Value;
                    var localPath = new Uri(fileUri).LocalPath;
                    if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                    {
                        return match.Value;
                    }

                    var data = Convert.ToBase64String(File.ReadAllBytes(localPath));
                    return $"url(\"data:{FontMimeType(localPath)};base64,{data}\")";
                }
                catch
                {
                    return match.Value;
                }
            },
            RegexOptions.IgnoreCase);
    }

    private static string FontMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".otf" => "font/otf",
            ".ttf" or ".ttc" => "font/ttf",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };
    }

    protected static string Html(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    protected static string Number(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

internal sealed class DesignWebPreviewPane : WebPreviewPane
{
    private int _referenceUpdateVersion;
    private DesignPreviewUpdate? _pendingUpdate;
    private DesignPreviewUpdate? _renderingUpdate;
    private DesignPreviewUpdate? _lastRenderedUpdate;
    private string _lastRenderedFontStyleHtml = "";
    private bool _hasResidentDocument;
    private long _latestUpdateSequence;
    private bool _isRendering;
    public event Action<DesignPreviewFrameStatus>? FrameStatusChanged;
    public event Action<string>? ContextActionRequested;

    public DesignWebPreviewPane()
    {
        WebView.WebMessageReceived += (_, args) =>
        {
            var message = args.Body?.ToString() ?? "";
            if (message.Length >= 2 && message[0] == '"')
            {
                try
                {
                    message = JsonSerializer.Deserialize<string>(message) ?? message;
                }
                catch (JsonException)
                {
                    return;
                }
            }
            const string prefix = "mockups-preview-action:";
            if (!message.StartsWith(prefix, StringComparison.Ordinal)) return;
            ContextActionRequested?.Invoke(message[prefix.Length..]);
        };
    }

    public Task<int> PreloadFrameImagesAsync(
        IReadOnlyCollection<string> imageSources,
        CancellationToken cancellationToken)
    {
        return PreloadPreviewImagesAsync(imageSources, cancellationToken);
    }

    public async Task<string> BuildRasterHtmlAsync(
        SpikeDatabase.DevicePreviewMetrics metrics,
        string themeMode,
        DesignPreviewPayload payload)
    {
        var bodyContent = await WebDesignPreviewRenderer.RenderBodyAsync(
            metrics,
            themeMode,
            showMarks: false,
            payload);
        var htmlParts = PreviewHtmlParts.Split(bodyContent);
        return RasterDocumentHtml(metrics, htmlParts.BodyHtml, htmlParts.FontStyleHtml);
    }

    public void SetRasterLoading(bool visible, string message)
    {
        var visibleJson = visible ? "true" : "false";
        var messageJson = JsonSerializer.Serialize(message);
        _ = WebView.InvokeScript($$"""
            (() => typeof window.mockupsSetRasterLoading === "function"
              ? window.mockupsSetRasterLoading({{visibleJson}}, {{messageJson}})
              : false)();
            """);
    }

    public static IReadOnlyList<string> ImageSourcesForPreload(string html)
    {
        return PreviewImageSources(PreviewHtmlParts.Split(html).BodyHtml).ToList();
    }

    public async Task<bool> PrewarmFrameAsync(
        SpikeDatabase.DevicePreviewMetrics metrics,
        string themeMode,
        bool showDesignMarks,
        DesignPreviewPayload payload)
    {
        var stopwatch = Stopwatch.StartNew();
        var bodyContent = await WebDesignPreviewRenderer.RenderBodyAsync(
            metrics,
            themeMode,
            showDesignMarks,
            payload);
        var htmlParts = PreviewHtmlParts.Split(bodyContent);
        var committed = await ReplacePreviewFontStylesAsync(htmlParts.FontStyleHtml)
            && await ReplacePreviewBodyAsync(htmlParts.BodyHtml, waitForCommit: true);
        PreviewDebugLog.Write(
            "preview.webview.prewarm-frame",
            ("component", payload.ComponentType),
            ("name", payload.Name),
            ("committed", committed),
            ("ms", stopwatch.Elapsed.TotalMilliseconds),
            ("bodyChars", htmlParts.BodyHtml.Length));
        return committed;
    }

    public void Update(
        SpikeDatabase.DevicePreviewMetrics metrics,
        bool isDark,
        string themeName,
        string themeMode,
        string scaleMode,
        bool showDesignMarks,
        bool showDeviceFrame,
        PreviewReferenceState reference,
        DesignPreviewPayload? payload,
        PreviewContextState contextState,
        bool isPlaybackUpdate,
        IEditorShellMessageSink messages)
    {
        var nextUpdate = new DesignPreviewUpdate(
            Interlocked.Increment(ref _latestUpdateSequence),
            metrics,
            isDark,
            themeName,
            themeMode,
            scaleMode,
            showDesignMarks,
            showDeviceFrame,
            reference,
            payload,
            contextState,
            isPlaybackUpdate,
            messages);

        if (_isRendering
            && _renderingUpdate is not null
            && nextUpdate.IsAnimationOnlyUpdateOf(_renderingUpdate))
        {
            _pendingUpdate = nextUpdate;
            return;
        }

        _pendingUpdate = nextUpdate;

        if (!_isRendering)
        {
            _ = ProcessPendingUpdatesAsync();
        }
    }

    private async Task ProcessPendingUpdatesAsync()
    {
        if (_isRendering) return;

        _isRendering = true;
        try
        {
            while (_pendingUpdate is not null)
            {
                var update = _pendingUpdate;
                _pendingUpdate = null;
                _renderingUpdate = update;
                await RenderUpdateAsync(update);
                _renderingUpdate = null;
            }
        }
        finally
        {
            _renderingUpdate = null;
            _isRendering = false;
            if (_pendingUpdate is not null)
            {
                _ = ProcessPendingUpdatesAsync();
            }
        }
    }

    private async Task RenderUpdateAsync(DesignPreviewUpdate update)
    {
        var stopwatch = Stopwatch.StartNew();
        var reference = PreviewReferenceOverlay.Resolve(update.Reference);
        if (update.ContextState.Kind is PreviewContextStateKind.NonRenderable or PreviewContextStateKind.Error)
        {
            if (_lastRenderedUpdate is null)
            {
                LoadContextState(update.ContextState, update);
                _lastRenderedFontStyleHtml = "";
            }
            else
            {
                await ShowResidentNonRenderableStateAsync(update.ContextState);
            }
            PreviewDebugLog.Write(
                "preview.context-state",
                ("kind", "non-renderable"),
                ("title", update.ContextState.Title),
                ("hasAction", update.ContextState.HasAction));
            return;
        }
        if (_lastRenderedUpdate is not null)
        {
            await HideResidentNonRenderableStateAsync();
        }
        if (update.Payload is null)
        {
            LoadHtml(DeviceHtml(
                update.Metrics,
                update.IsDark,
                update.ThemeName,
                update.ThemeMode,
                update.ScaleMode,
                "Design preview",
                update.ShowDesignMarks,
                update.ShowDeviceFrame,
                Placeholder(
                    "Design WebView host",
                    "Select a component variant to preview it through the desktop component route."),
                reference: reference));
            _lastRenderedUpdate = update;
            PreviewDebugLog.Write(
                "preview.webview.update",
                ("route", "full-load"),
                ("reason", "empty-payload"),
                ("ms", stopwatch.Elapsed.TotalMilliseconds));
            return;
        }

        string bodyContent;
        Exception? renderError = null;
        if (_lastRenderedUpdate is null)
        {
            var loadingState = new PreviewContextState(
                PreviewContextStateKind.Loading,
                "Preparando preview",
                $"Resolviendo {update.Payload.Name}…");
            if (_hasResidentDocument)
            {
                await ShowResidentNonRenderableStateAsync(loadingState);
            }
            else
            {
                LoadContextState(loadingState, update);
            }
        }
        else
        {
            ShowResidentContextState("loading", "Actualizando preview", update.Payload.Name);
        }
        try
        {
            bodyContent = await WebDesignPreviewRenderer.RenderBodyAsync(
                update.Metrics,
                update.ThemeMode,
                update.ShowDesignMarks,
                update.Payload);
        }
        catch (Exception error)
        {
            renderError = error;
            bodyContent = Placeholder(
                $"{update.Payload.Name} · {update.Payload.Kind}",
                "Preview unavailable. See Messages.");
        }

        if (renderError is not null)
        {
            update.Messages.Error("Design preview", renderError);
            if (_lastRenderedUpdate is not null)
            {
                ShowResidentContextState(
                    "error",
                    "Preview no actualizado",
                    "The last valid result is being retained. Check Messages for details.");
                PreviewDebugLog.Write(
                    "preview.webview.update",
                    ("route", "retain-last-good"),
                    ("reason", "render-error"),
                    ("component", update.Payload.ComponentType),
                    ("name", update.Payload.Name),
                    ("ms", stopwatch.Elapsed.TotalMilliseconds));
                FrameStatusChanged?.Invoke(new DesignPreviewFrameStatus(
                    stopwatch.Elapsed.TotalMilliseconds,
                    IsAnimationOnly: false,
                    UsedDomPatch: false,
                    RenderError: true));
                return;
            }

            LoadContextState(new PreviewContextState(
                PreviewContextStateKind.Error,
                "Preview no disponible",
                "The first result could not be generated. Check Messages and try again.",
                "Reintentar",
                "__preview_retry__"), update);
            return;
        }

        if (!update.IsPlaybackUpdate
            && update.Sequence != Volatile.Read(ref _latestUpdateSequence))
        {
            PreviewDebugLog.Write(
                "preview.webview.update",
                ("route", "discarded"),
                ("reason", "newer-pending"),
                ("component", update.Payload.ComponentType),
                ("name", update.Payload.Name),
                ("ms", stopwatch.Elapsed.TotalMilliseconds));
            return;
        }

        var htmlParts = PreviewHtmlParts.Split(bodyContent);
        var isAnimationOnlyUpdate = _lastRenderedUpdate is not null
            && (update.IsPlaybackUpdate || update.IsAnimationOnlyUpdateOf(_lastRenderedUpdate));
        var isMarksOnlyUpdate = _lastRenderedUpdate is not null
            && update.IsMarksOnlyUpdateOf(_lastRenderedUpdate);
        var isResidentCompatible = _lastRenderedUpdate is not null
            ? update.IsResidentShellCompatibleWith(_lastRenderedUpdate)
            : _hasResidentDocument;
        if (renderError is null && isResidentCompatible)
        {
            var fontsChanged = !string.Equals(
                htmlParts.FontStyleHtml,
                _lastRenderedFontStyleHtml,
                StringComparison.Ordinal);
            var fontsCommitted = !fontsChanged
                || await ReplacePreviewFontStylesAsync(htmlParts.FontStyleHtml);
            if (!update.IsPlaybackUpdate
                && update.Sequence != Volatile.Read(ref _latestUpdateSequence))
            {
                PreviewDebugLog.Write(
                    "preview.webview.update",
                    ("route", "discarded"),
                    ("reason", "newer-pending-after-fonts"),
                    ("component", update.Payload.ComponentType),
                    ("name", update.Payload.Name),
                    ("ms", stopwatch.Elapsed.TotalMilliseconds));
                return;
            }
            var bodyCommitted = fontsCommitted
                && await ReplacePreviewBodyAsync(htmlParts.BodyHtml, waitForCommit: !isAnimationOnlyUpdate);
            if (bodyCommitted)
            {
                HideResidentContextState();
                await HideResidentNonRenderableStateAsync();
                await UpdateReferenceOverlayAsync(reference);
                _lastRenderedUpdate = update;
                _lastRenderedFontStyleHtml = htmlParts.FontStyleHtml;
                PreviewDebugLog.Write(
                    "preview.webview.update",
                    ("route", "dom-patch"),
                    ("component", update.Payload.ComponentType),
                    ("name", update.Payload.Name),
                    ("animationOnly", isAnimationOnlyUpdate),
                    ("marksOnly", isMarksOnlyUpdate),
                    ("ms", stopwatch.Elapsed.TotalMilliseconds),
                    ("bodyChars", htmlParts.BodyHtml.Length),
                    ("fontStyleChars", htmlParts.FontStyleHtml.Length));
                FrameStatusChanged?.Invoke(new DesignPreviewFrameStatus(
                    stopwatch.Elapsed.TotalMilliseconds,
                    IsAnimationOnly: isAnimationOnlyUpdate,
                    UsedDomPatch: true,
                    RenderError: false));
                return;
            }

            var commitError = new InvalidOperationException("The resident preview update could not be committed; the last valid preview was retained.");
            update.Messages.Error("Design preview", commitError);
            ShowResidentContextState(
                "error",
                "Preview no actualizado",
                "The last valid result is being retained. Check Messages for details.");
            PreviewDebugLog.Write(
                "preview.webview.update",
                ("route", "retain-last-good"),
                ("reason", fontsCommitted ? "body-commit-failed" : "font-style-commit-failed"),
                ("component", update.Payload.ComponentType),
                ("name", update.Payload.Name),
                ("ms", stopwatch.Elapsed.TotalMilliseconds));
            FrameStatusChanged?.Invoke(new DesignPreviewFrameStatus(
                stopwatch.Elapsed.TotalMilliseconds,
                IsAnimationOnly: isAnimationOnlyUpdate,
                UsedDomPatch: false,
                RenderError: true));
            return;
        }

        LoadHtml(DeviceHtml(
            update.Metrics,
            update.IsDark,
            update.ThemeName,
            update.ThemeMode,
            update.ScaleMode,
            "Design preview",
            update.ShowDesignMarks,
            update.ShowDeviceFrame,
            htmlParts.BodyHtml,
            htmlParts.FontStyleHtml,
            reference));
        _hasResidentDocument = true;
        _lastRenderedUpdate = update;
        _lastRenderedFontStyleHtml = htmlParts.FontStyleHtml;
        PreviewDebugLog.Write(
            "preview.webview.update",
            ("route", "full-load"),
            ("component", update.Payload.ComponentType),
            ("name", update.Payload.Name),
            ("animationOnly", isAnimationOnlyUpdate),
            ("reason", _lastRenderedUpdate is null ? "initial-document" : "incompatible-shell"),
            ("renderError", renderError is not null),
            ("ms", stopwatch.Elapsed.TotalMilliseconds),
            ("bodyChars", htmlParts.BodyHtml.Length),
            ("fontStyleChars", htmlParts.FontStyleHtml.Length));
        FrameStatusChanged?.Invoke(new DesignPreviewFrameStatus(
            stopwatch.Elapsed.TotalMilliseconds,
            isAnimationOnlyUpdate,
            UsedDomPatch: false,
            renderError is not null));
    }

    private void LoadContextState(PreviewContextState state, DesignPreviewUpdate update)
    {
        LoadHtml(DeviceHtml(
            update.Metrics,
            update.IsDark,
            update.ThemeName,
            update.ThemeMode,
            update.ScaleMode,
            "Design preview",
            showDesignMarks: false,
            showDeviceFrame: false,
            bodyContent: "",
            reference: PreviewReferenceOverlay.Resolve(update.Reference),
            initialContextState: state));
        _hasResidentDocument = true;
    }

    private async Task ShowResidentNonRenderableStateAsync(PreviewContextState state)
    {
        var titleJson = JsonSerializer.Serialize(state.Title);
        var messageJson = JsonSerializer.Serialize(state.Message);
        var actionLabelJson = JsonSerializer.Serialize(state.ActionLabel);
        var actionTargetJson = JsonSerializer.Serialize(state.ActionTargetId);
        var kindJson = JsonSerializer.Serialize(state.Kind == PreviewContextStateKind.Loading ? "loading" : "non-renderable");
        await WebView.InvokeScript($$"""
            (() => typeof window.mockupsSetNonRenderablePreviewState === "function"
              ? window.mockupsSetNonRenderablePreviewState({{titleJson}}, {{messageJson}}, {{actionLabelJson}}, {{actionTargetJson}}, {{kindJson}})
              : false)();
            """);
    }

    private async Task HideResidentNonRenderableStateAsync()
    {
        try
        {
            await WebView.InvokeScript("(() => typeof window.mockupsSetNonRenderablePreviewState === 'function' ? window.mockupsSetNonRenderablePreviewState('', '', '', '') : false)();");
        }
        catch
        {
            // A first-load context-state document does not expose the resident
            // preview shell yet; the following full load replaces it normally.
        }
    }

    private void ShowResidentContextState(string kind, string title, string message)
    {
        var kindJson = JsonSerializer.Serialize(kind);
        var titleJson = JsonSerializer.Serialize(title);
        var messageJson = JsonSerializer.Serialize(message);
        _ = WebView.InvokeScript($$"""
            (() => typeof window.mockupsSetContextualPreviewState === "function"
              ? window.mockupsSetContextualPreviewState({{kindJson}}, {{titleJson}}, {{messageJson}})
              : false)();
            """);
    }

    private void HideResidentContextState()
    {
        _ = WebView.InvokeScript("(() => typeof window.mockupsSetContextualPreviewState === 'function' ? window.mockupsSetContextualPreviewState('', '', '') : false)();");
    }


    public sealed record DesignPreviewFrameStatus(
        double ElapsedMilliseconds,
        bool IsAnimationOnly,
        bool UsedDomPatch,
        bool RenderError);

    public async Task UpdateReferenceOverlayAsync(PreviewReferenceState state)
    {
        var version = Interlocked.Increment(ref _referenceUpdateVersion);
        var reference = await Task.Run(() => PreviewReferenceOverlay.Resolve(state));
        if (version != _referenceUpdateVersion) return;
        await UpdateReferenceOverlayAsync(reference);
    }

    public async Task SetDesignMarksAsync(bool visible)
    {
        try
        {
            await WebView.InvokeScript($$"""
                (() => typeof window.mockupsSetDesignMarks === "function"
                  ? window.mockupsSetDesignMarks({{(visible ? "true" : "false")}})
                  : false)();
                """);
        }
        catch
        {
        }
    }

    private async Task UpdateReferenceOverlayAsync(PreviewReferenceOverlay reference)
    {
        try
        {
            var json = JsonSerializer.Serialize(reference, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await WebView.InvokeScript($$"""
                (() => typeof window.mockupsSetReferenceOverlay === "function"
                  ? window.mockupsSetReferenceOverlay({{json}})
                  : false)();
                """);
        }
        catch
        {
        }
    }

    private sealed record PreviewHtmlParts(string BodyHtml, string FontStyleHtml)
    {
        public static PreviewHtmlParts Split(string html)
        {
            var trimmed = html.TrimStart();
            if (!trimmed.StartsWith("<style", StringComparison.OrdinalIgnoreCase))
            {
                return new PreviewHtmlParts(html, "");
            }

            var closeIndex = trimmed.IndexOf("</style>", StringComparison.OrdinalIgnoreCase);
            if (closeIndex < 0)
            {
                return new PreviewHtmlParts(html, "");
            }

            var endIndex = closeIndex + "</style>".Length;
            return new PreviewHtmlParts(
                trimmed[endIndex..],
                trimmed[..endIndex]);
        }
    }

    private sealed record DesignPreviewUpdate(
        long Sequence,
        SpikeDatabase.DevicePreviewMetrics Metrics,
        bool IsDark,
        string ThemeName,
        string ThemeMode,
        string ScaleMode,
        bool ShowDesignMarks,
        bool ShowDeviceFrame,
        PreviewReferenceState Reference,
        DesignPreviewPayload? Payload,
        PreviewContextState ContextState,
        bool IsPlaybackUpdate,
        IEditorShellMessageSink Messages)
    {
        public bool IsResidentShellCompatibleWith(DesignPreviewUpdate other)
        {
            return Metrics.Equals(other.Metrics)
                && IsDark == other.IsDark
                && ThemeName == other.ThemeName
                && ThemeMode == other.ThemeMode
                && ScaleMode == other.ScaleMode
                && ShowDeviceFrame == other.ShowDeviceFrame
                && Payload is not null
                && other.Payload is not null;
        }

        public bool IsAnimationOnlyUpdateOf(DesignPreviewUpdate other)
        {
            return Metrics.Equals(other.Metrics)
                && IsDark == other.IsDark
                && ThemeName == other.ThemeName
                && ThemeMode == other.ThemeMode
                && ScaleMode == other.ScaleMode
                && ShowDesignMarks == other.ShowDesignMarks
                && ShowDeviceFrame == other.ShowDeviceFrame
                && Reference with { PreviewFrame = other.Reference.PreviewFrame } == other.Reference
                && StablePayloadSignature(Payload) == StablePayloadSignature(other.Payload)
                && CurrentTimeSignature(Payload) != CurrentTimeSignature(other.Payload);
        }

        public bool IsMarksOnlyUpdateOf(DesignPreviewUpdate other)
        {
            return ShowDesignMarks != other.ShowDesignMarks
                && Metrics.Equals(other.Metrics)
                && IsDark == other.IsDark
                && ThemeName == other.ThemeName
                && ThemeMode == other.ThemeMode
                && ScaleMode == other.ScaleMode
                && ShowDeviceFrame == other.ShowDeviceFrame
                && Reference == other.Reference
                && StablePayloadSignature(Payload) == StablePayloadSignature(other.Payload)
                && CurrentTimeSignature(Payload) == CurrentTimeSignature(other.Payload);
        }

        private static string StablePayloadSignature(DesignPreviewPayload? payload)
        {
            if (payload is null) return "";

            return string.Join(
                "\u001f",
                payload.Kind,
                payload.ComponentType,
                payload.Name,
                payload.ConfigJson,
                payload.ThemeTokensJson,
                payload.ThemeStatusBarPresetId,
                payload.ThemeNavigationBarPresetId,
                payload.ComponentBaseConfigsJson,
                payload.AppConfigJson,
                StablePreviewJson(payload.DesignPreviewJson));
        }

        private static string StablePreviewJson(string json)
        {
            try
            {
                var preview = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();
                preview.Remove("currentTimeSeconds");
                preview.Remove("motionElapsedMs");
                foreach (var action in ComponentPreviewActions.ReadApplicable(preview))
                {
                    if (!string.IsNullOrWhiteSpace(action.TimeJsonKey))
                    {
                        ComponentPreviewActions.RemoveValue(preview, action, action.TimeJsonKey);
                    }
                    if (!string.IsNullOrWhiteSpace(action.PlayInputId))
                    {
                        ComponentPreviewActions.RemoveValue(preview, action, action.PlayInputId);
                    }
                }
                return preview.ToJsonString();
            }
            catch
            {
                return json;
            }
        }

        private static string CurrentTimeSignature(DesignPreviewPayload? payload)
        {
            if (payload is null) return "";

            try
            {
                var preview = JsonNode.Parse(string.IsNullOrWhiteSpace(payload.DesignPreviewJson) ? "{}" : payload.DesignPreviewJson) as JsonObject;
                if (preview is null)
                {
                    return "";
                }

                var actionTimes = ComponentPreviewActions.ReadApplicable(preview)
                    .Where((action) => !string.IsNullOrWhiteSpace(action.TimeJsonKey)
                        && ComponentPreviewActions.Value(preview, action, action.TimeJsonKey) is not null)
                    .Select((action) => $"{action.Id}:{ComponentPreviewActions.Value(preview, action, action.TimeJsonKey)?.ToJsonString() ?? ""}")
                    .ToList();
                if (actionTimes.Count > 0)
                {
                    return string.Join("|", actionTimes);
                }

                return preview["currentTimeSeconds"]?.ToJsonString()
                    ?? preview["motionElapsedMs"]?.ToJsonString()
                    ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
