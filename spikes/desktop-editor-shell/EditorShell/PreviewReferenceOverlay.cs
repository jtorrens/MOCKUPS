using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record PreviewReferenceState(
    string SourcePath,
    string ViewMode,
    double Swipe,
    double Opacity,
    double Angle,
    int PreviewFrame,
    int FrameRate,
    string ProjectMediaRoot)
{
    public static PreviewReferenceState Default { get; } = new("", "preview", 0.5, 1, 0, 0, 25, "");
}

internal sealed record PreviewReferenceOverlay(
    string SourceUri,
    string ViewMode,
    double Swipe,
    double Opacity,
    double Angle)
{
    private static readonly Dictionary<string, string> UriCache = new(StringComparer.Ordinal);

    public static PreviewReferenceOverlay Resolve(PreviewReferenceState state)
    {
        var viewMode = state.ViewMode == "split" ? "split" : "preview";
        var sourceUri = viewMode != "split" || string.IsNullOrWhiteSpace(state.SourcePath)
            ? ""
            : ResolveSourceUri(state);
        return new PreviewReferenceOverlay(
            sourceUri,
            viewMode,
            Math.Clamp(state.Swipe, 0, 1),
            Math.Clamp(state.Opacity, 0, 1),
            Math.Clamp(state.Angle, -80, 80));
    }

    private static string ResolveSourceUri(PreviewReferenceState state)
    {
        var sourcePath = ProjectPathService.ResolveLocalPath(state.SourcePath, state.ProjectMediaRoot);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return "";

        var isVideo = IsVideo(sourcePath);
        var timeSeconds = isVideo
            ? Math.Max(0, state.PreviewFrame) / Math.Max(1, state.FrameRate)
            : 0;
        var assetIdentity = AssetIdentity(sourcePath);
        var cacheKey = $"{assetIdentity}|{timeSeconds:F3}";
        lock (UriCache)
        {
            if (UriCache.TryGetValue(cacheKey, out var cached)) return cached;
        }

        var resolved = isVideo ? VideoFrameUri(sourcePath, assetIdentity, timeSeconds) : ImageDataUri(sourcePath);
        if (string.IsNullOrWhiteSpace(resolved)) return "";
        lock (UriCache)
        {
            UriCache[cacheKey] = resolved;
            while (UriCache.Count > 120)
            {
                var first = UriCache.Keys.GetEnumerator();
                if (!first.MoveNext()) break;
                UriCache.Remove(first.Current);
            }
        }
        return resolved;
    }

    private static string ImageDataUri(string path)
    {
        var mime = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "",
        };
        return string.IsNullOrWhiteSpace(mime)
            ? ""
            : $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
    }

    private static string VideoFrameUri(string sourcePath, string assetIdentity, double timeSeconds)
    {
        try
        {
            var key = $"{assetIdentity}|{timeSeconds:F3}";
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
            var framePath = Path.Combine(Path.GetTempPath(), "mockups-preview-reference", $"{hash}.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(framePath)!);
            if (!File.Exists(framePath) || new FileInfo(framePath).Length == 0)
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = FfmpegExecutable(),
                    Arguments = $"-hide_banner -loglevel error -ss {timeSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -i \"{sourcePath}\" -frames:v 1 -q:v 4 -y \"{framePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                process?.WaitForExit(15_000);
            }
            return File.Exists(framePath) ? ImageDataUri(framePath) : "";
        }
        catch
        {
            return "";
        }
    }

    private static bool IsVideo(string path) => Path.GetExtension(path).ToLowerInvariant() is ".mp4" or ".mov" or ".m4v" or ".webm";

    private static string AssetIdentity(string path)
    {
        var info = new FileInfo(path);
        return $"{Path.GetFullPath(path)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{info.CreationTimeUtc.Ticks}";
    }

    private static string FfmpegExecutable()
    {
        foreach (var candidate in new[] { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/usr/bin/ffmpeg", "ffmpeg" })
        {
            if (!Path.IsPathFullyQualified(candidate) || File.Exists(candidate)) return candidate;
        }
        return "ffmpeg";
    }
}
