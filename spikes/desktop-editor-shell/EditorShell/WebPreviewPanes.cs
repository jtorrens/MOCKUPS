using Avalonia.Controls;
using Avalonia.Media;
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
    private readonly NativeWebView _webView;

    protected WebPreviewPane()
    {
        _webView = new NativeWebView
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };

        Children.Add(_webView);
    }

    protected void LoadHtml(string html)
    {
        _webView.NavigateToString(html, new Uri("https://mockups.local/"));
    }

    protected async Task<bool> ReplacePreviewBodyAsync(string bodyContent, bool waitForCommit = false)
    {
        var stopwatch = Stopwatch.StartNew();
        var bodyJson = JsonSerializer.Serialize(bodyContent);
        try
        {
            var result = await _webView.InvokeScript($$"""
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
            foreach (var batch in imageSources.Chunk(10))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batchStopwatch = Stopwatch.StartNew();
                var sourcesJson = JsonSerializer.Serialize(batch);
                var result = await _webView.InvokeScript($$"""
                    (() => {
                      if (typeof window.mockupsPreloadPreviewImages !== "function") return 0;
                      return window.mockupsPreloadPreviewImages({{sourcesJson}});
                    })();
                    """);
                var loaded = int.TryParse(result?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                    ? count
                    : 0;
                requestedTotal += batch.Length;
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
            var events = await DrainPreviewPatchEventsOnceAsync();
            foreach (var patchEvent in events)
            {
                if (!PatchMatches(patchEvent, patchId))
                {
                    continue;
                }

                var eventName = JsonString(patchEvent, "event");
                if (eventName == "commit")
                {
                    PreviewDebugLog.Write(
                        "preview.webview.dom-patch.wait",
                        ("patch", patchId),
                        ("result", "commit"),
                        ("ms", stopwatch.Elapsed.TotalMilliseconds));
                    return true;
                }

                if (eventName is "skip" or "stale")
                {
                    PreviewDebugLog.Write(
                        "preview.webview.dom-patch.wait",
                        ("patch", patchId),
                        ("result", eventName),
                        ("ms", stopwatch.Elapsed.TotalMilliseconds));
                    return false;
                }
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
            var result = await _webView.InvokeScript("""
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
                    ("srcChars", JsonText(node, "srcChars")));
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

    private static bool PatchMatches(JsonObject node, int patchId)
    {
        return node.TryGetPropertyValue("patch", out var value)
            && value is JsonValue jsonValue
            && jsonValue.TryGetValue<int>(out var currentPatchId)
            && currentPatchId == patchId;
    }

    private static string JsonString(JsonObject owner, string key)
    {
        return owner.TryGetPropertyValue(key, out var value)
            && value is JsonValue jsonValue
            && jsonValue.TryGetValue<string>(out var text)
            ? text
            : "";
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
        string bodyContent,
        string fontStyleHtml = "")
    {
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
                  color: {{(themeMode == "dark" ? "#F8FAFC" : "#111827")}};
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
              </style>
              {{FontStylesHtml(fontStyleHtml)}}
            </head>
            <body>
              <main class="preview-viewport-host">
                <section class="preview-viewport" id="previewViewport">
                  <div class="preview-scale" id="previewScale">
                    {{bodyContent}}
                  </div>
                  {{DesignMarksHtml(showDesignMarks)}}
                  <div aria-hidden="true" class="preview-phone-frame" id="previewPhoneFrame"></div>
                  {{PreviewMetaHtml(showDesignMarks, previewMode, metrics.Name, themeName, themeMode)}}
                </section>
              </main>
              <script>
                const host = document.querySelector(".preview-viewport-host");
                const viewport = document.getElementById("previewViewport");
                const scaleLayer = document.getElementById("previewScale");
                const frame = document.getElementById("previewPhoneFrame");
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
                  frame.style.setProperty("--preview-frame-border", `${Math.max(1, 10 * scale)}px`);
                  frame.style.setProperty("--preview-frame-radius", `${cornerRadius * scale}px`);
                  frame.style.setProperty("--preview-frame-shadow-y", `${10 * scale}px`);
                  frame.style.setProperty("--preview-frame-shadow-blur", `${28 * scale}px`);
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
                window.mockupsDrainPreviewPatchEvents = () => {
                  const events = window.mockupsPreviewPatchEvents || [];
                  window.mockupsPreviewPatchEvents = [];
                  return JSON.stringify(events);
                };

                function recordPatchEvent(event, detail = {}) {
                  const events = window.mockupsPreviewPatchEvents || [];
                  events.push({
                    event,
                    time: Math.round(performance.now() * 1000) / 1000,
                    ...detail,
                  });
                  window.mockupsPreviewPatchEvents = events.slice(-160);
                }

                function imageLoadPromise(image, patchId, startedAt) {
                  if (!image || !image.src || image.complete && image.naturalWidth > 0) {
                    recordPatchEvent("image", {
                      patch: patchId,
                      status: "ready",
                      ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                      srcChars: image?.src?.length ?? 0,
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
                      });
                      resolve(status);
                    };
                    image.addEventListener("load", () => finish("load"), { once: true });
                    image.addEventListener("error", () => finish("error"), { once: true });
                    if (typeof image.decode === "function") {
                      image.decode().then(() => finish("decode"), () => finish("decode-error"));
                    }
                    setTimeout(() => finish("timeout"), 500);
                  });
                }

                window.mockupsPreloadPreviewImages = async (sources) => {
                  const startedAt = performance.now();
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
                  const uniqueSources = [...new Set(Array.isArray(sources) ? sources : [])];
                  const images = uniqueSources.map((source) => {
                    const image = new Image();
                    image.decoding = "sync";
                    image.loading = "eager";
                    image.src = source;
                    pool.appendChild(image);
                    return image;
                  });
                  await Promise.all(images.map((image) => imageLoadPromise(image, "preload", startedAt)));
                  recordPatchEvent("preload-images", {
                    patch: "preload",
                    images: images.length,
                    ms: Math.round((performance.now() - startedAt) * 1000) / 1000,
                  });
                  return images.filter((image) => image.complete && image.naturalWidth > 0).length;
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
                  scaleLayer.appendChild(nextLayer);

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
                        if (child !== nextLayer) child.remove();
                      }
                      calculatePreviewFit();
                      recordPatchEvent("commit", {
                        patch: sequence,
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

    private static string DesignMarksHtml(bool showDesignMarks)
    {
        return !showDesignMarks
            ? ""
            : """
                  <div aria-hidden="true" class="preview-design-marks">
                    <div class="preview-guide is-vertical is-dashed" style="left:25%"></div>
                    <div class="preview-guide is-vertical" style="left:50%"></div>
                    <div class="preview-guide is-vertical is-dashed" style="left:75%"></div>
                    <div class="preview-guide is-horizontal is-dashed" style="top:25%"></div>
                    <div class="preview-guide is-horizontal" style="top:50%"></div>
                    <div class="preview-guide is-horizontal is-dashed" style="top:75%"></div>
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
        return !showDesignMarks
            ? ""
            : $$"""
                  <div class="preview-meta">
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

internal sealed class RuntimeWebPreviewPane : WebPreviewPane
{
    public void Update(
        SpikeDatabase.DevicePreviewMetrics metrics,
        bool isDark,
        string themeName,
        string themeMode,
        string scaleMode)
    {
        LoadHtml(DeviceHtml(
            metrics,
            isDark,
            themeName,
            themeMode,
            scaleMode,
            "Runtime preview",
            false,
            Placeholder(
                "Runtime WebView host",
                "Next step: load the existing React preview runtime and feed it the resolver payload. No Avalonia duplicate renderer is used here.")));
    }
}

internal sealed class DesignWebPreviewPane : WebPreviewPane
{
    private const int MaxQueuedAnimationFrames = 240;
    private DesignPreviewUpdate? _pendingUpdate;
    private DesignPreviewUpdate? _renderingUpdate;
    private DesignPreviewUpdate? _lastRenderedUpdate;
    private readonly Queue<DesignPreviewUpdate> _queuedAnimationUpdates = new();
    private bool _isRendering;
    public event Action<DesignPreviewFrameStatus>? FrameStatusChanged;

    public Task<int> PreloadFrameImagesAsync(
        IReadOnlyCollection<string> imageSources,
        CancellationToken cancellationToken)
    {
        return PreloadPreviewImagesAsync(imageSources, cancellationToken);
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
        var committed = await ReplacePreviewBodyAsync(htmlParts.BodyHtml, waitForCommit: true);
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
        DesignPreviewPayload? payload,
        IEditorShellMessageSink messages)
    {
        var nextUpdate = new DesignPreviewUpdate(
            metrics,
            isDark,
            themeName,
            themeMode,
            scaleMode,
            showDesignMarks,
            payload,
            messages);

        if (_isRendering
            && _renderingUpdate is not null
            && nextUpdate.IsAnimationOnlyUpdateOf(_renderingUpdate))
        {
            _queuedAnimationUpdates.Enqueue(nextUpdate);
            while (_queuedAnimationUpdates.Count > MaxQueuedAnimationFrames)
            {
                _queuedAnimationUpdates.Dequeue();
            }
            return;
        }

        _queuedAnimationUpdates.Clear();
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
                if (_queuedAnimationUpdates.Count > 0)
                {
                    _pendingUpdate = _queuedAnimationUpdates.Dequeue();
                }
            }
        }
        finally
        {
            _renderingUpdate = null;
            _isRendering = false;
            if (_pendingUpdate is not null || _queuedAnimationUpdates.Count > 0)
            {
                if (_pendingUpdate is null && _queuedAnimationUpdates.Count > 0)
                {
                    _pendingUpdate = _queuedAnimationUpdates.Dequeue();
                }
                _ = ProcessPendingUpdatesAsync();
            }
        }
    }

    private async Task RenderUpdateAsync(DesignPreviewUpdate update)
    {
        var stopwatch = Stopwatch.StartNew();
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
                Placeholder(
                    "Design WebView host",
                    "Select a component variant to preview it through the desktop component route.")));
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
        }

        var htmlParts = PreviewHtmlParts.Split(bodyContent);
        var isAnimationOnlyUpdate = _lastRenderedUpdate is not null
            && update.IsAnimationOnlyUpdateOf(_lastRenderedUpdate);
        if (renderError is null
            && isAnimationOnlyUpdate
            && await ReplacePreviewBodyAsync(htmlParts.BodyHtml))
        {
            _lastRenderedUpdate = update;
            PreviewDebugLog.Write(
                "preview.webview.update",
                ("route", "dom-patch"),
                ("component", update.Payload.ComponentType),
                ("name", update.Payload.Name),
                ("ms", stopwatch.Elapsed.TotalMilliseconds),
                ("bodyChars", htmlParts.BodyHtml.Length),
                ("fontStyleChars", htmlParts.FontStyleHtml.Length));
            FrameStatusChanged?.Invoke(new DesignPreviewFrameStatus(
                stopwatch.Elapsed.TotalMilliseconds,
                IsAnimationOnly: true,
                UsedDomPatch: true,
                RenderError: false));
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
            htmlParts.BodyHtml,
            htmlParts.FontStyleHtml));
        _lastRenderedUpdate = update;
        PreviewDebugLog.Write(
            "preview.webview.update",
            ("route", "full-load"),
            ("component", update.Payload.ComponentType),
            ("name", update.Payload.Name),
            ("animationOnly", isAnimationOnlyUpdate),
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

    public sealed record DesignPreviewFrameStatus(
        double ElapsedMilliseconds,
        bool IsAnimationOnly,
        bool UsedDomPatch,
        bool RenderError);

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
        SpikeDatabase.DevicePreviewMetrics Metrics,
        bool IsDark,
        string ThemeName,
        string ThemeMode,
        string ScaleMode,
        bool ShowDesignMarks,
        DesignPreviewPayload? Payload,
        IEditorShellMessageSink Messages)
    {
        public bool IsAnimationOnlyUpdateOf(DesignPreviewUpdate other)
        {
            return Metrics.Equals(other.Metrics)
                && IsDark == other.IsDark
                && ThemeName == other.ThemeName
                && ThemeMode == other.ThemeMode
                && ScaleMode == other.ScaleMode
                && ShowDesignMarks == other.ShowDesignMarks
                && StablePayloadSignature(Payload) == StablePayloadSignature(other.Payload)
                && CurrentTimeSignature(Payload) != CurrentTimeSignature(other.Payload);
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
                preview.Remove("motionTimeSeconds");
                foreach (var action in ComponentPreviewActions.Read(preview))
                {
                    if (!string.IsNullOrWhiteSpace(action.TimeJsonKey))
                    {
                        preview.Remove(action.TimeJsonKey);
                    }
                    if (!string.IsNullOrWhiteSpace(action.PlayInputId))
                    {
                        preview.Remove(action.PlayInputId);
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

                var actionTimes = ComponentPreviewActions.Read(preview)
                    .Where((action) => !string.IsNullOrWhiteSpace(action.TimeJsonKey)
                        && preview[action.TimeJsonKey] is not null)
                    .Select((action) => $"{action.Id}:{preview[action.TimeJsonKey]?.ToJsonString() ?? ""}")
                    .ToList();
                if (actionTimes.Count > 0)
                {
                    return string.Join("|", actionTimes);
                }

                return preview["currentTimeSeconds"]?.ToJsonString()
                    ?? preview["motionTimeSeconds"]?.ToJsonString()
                    ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
