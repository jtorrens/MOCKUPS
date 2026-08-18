using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Mockups.DesktopEditorShell.EditorShell;

// The embedded web player consumes one browser-compatible playback document.
// The authored reference remains the source of truth; this is only its
// disposable, local playback representation.
internal static class ReferenceVideoPlaybackCache
{
    private static readonly object Gate = new();

    internal static string Resolve(string sourcePath)
    {
        if (!Path.IsPathFullyQualified(sourcePath))
        {
            throw new InvalidOperationException(
                "Reference video playback requires an absolute local source path.");
        }
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException(
                $"The associated reference video does not exist: {sourcePath}");
        }

        var source = new FileInfo(sourcePath);
        var identity = $"{source.FullName}|{source.Length}|{source.LastWriteTimeUtc.Ticks}";
        var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        var directory = Path.Combine(Path.GetTempPath(), "mockups-reference-video");
        var playbackPath = Path.Combine(directory, $"{hash}.mp4");

        lock (Gate)
        {
            if (File.Exists(playbackPath) && new FileInfo(playbackPath).Length > 0)
            {
                return playbackPath;
            }

            Directory.CreateDirectory(directory);
            var temporaryPath = $"{playbackPath}.{Guid.NewGuid():N}.partial.mp4";
            try
            {
                ConvertToBrowserPlayback(source.FullName, temporaryPath);
                if (!File.Exists(temporaryPath) || new FileInfo(temporaryPath).Length == 0)
                {
                    throw new InvalidOperationException(
                        "FFmpeg did not create a playable reference video.");
                }
                File.Move(temporaryPath, playbackPath, overwrite: true);
                return playbackPath;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }

    private static void ConvertToBrowserPlayback(string sourcePath, string destinationPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = RenderJobExecutor.ResolveFfmpegExecutable(),
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:v:0");
        startInfo.ArgumentList.Add("-an");
        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add("libx264");
        startInfo.ArgumentList.Add("-preset");
        startInfo.ArgumentList.Add("veryfast");
        startInfo.ArgumentList.Add("-crf");
        startInfo.ArgumentList.Add("18");
        startInfo.ArgumentList.Add("-pix_fmt");
        startInfo.ArgumentList.Add("yuv420p");
        startInfo.ArgumentList.Add("-movflags");
        startInfo.ArgumentList.Add("+faststart");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add(destinationPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("FFmpeg could not be started for the reference video.");
        var standardError = process.StandardError.ReadToEndAsync();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException(
                "Reference video conversion exceeded the two-minute limit.");
        }
        var error = standardError.GetAwaiter().GetResult();
        _ = standardOutput.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? $"FFmpeg failed to prepare the reference video (exit code {process.ExitCode})."
                    : $"FFmpeg failed to prepare the reference video: {error.Trim()}");
        }
    }
}
