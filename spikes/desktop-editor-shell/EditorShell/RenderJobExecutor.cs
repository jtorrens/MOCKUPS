using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RenderJobExecutor : IRenderJobExecutor
{
    private readonly ChromiumPreviewRasterizer _rasterizer = new();

    public async Task ExecuteAsync(
        RenderJobSnapshot snapshot,
        IProgress<RenderQueueExecutionProgress> progress,
        CancellationToken cancellationToken)
    {
        snapshot.Validate();
        RenderOutputPathSecurity.RequireOutputTarget(snapshot.Output);
        foreach (var asset in snapshot.Assets)
        {
            PreviewAssetRegistry.Register(asset.Key, asset.Value);
        }
        var mode = RenderOutputModes.Require(snapshot.Output.OutputModeId);
        if (mode.Kind == "mov")
        {
            await RenderMovAsync(snapshot, mode, progress, cancellationToken);
            return;
        }
        await RenderImageSequenceAsync(
            snapshot,
            mode,
            progress,
            cancellationToken);
    }

    private async Task RenderMovAsync(
        RenderJobSnapshot snapshot,
        RenderOutputModeDefinition mode,
        IProgress<RenderQueueExecutionProgress> progress,
        CancellationToken cancellationToken)
    {
        var frameDirectory = Path.Combine(
            Path.GetTempPath(),
            $"mockups-render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(frameDirectory);
        var outputDirectory = Path.GetDirectoryName(snapshot.Output.OutputPath)
            ?? throw new InvalidOperationException(
                "The MOV output has no parent directory.");
        var temporaryOutput = Path.Combine(
            outputDirectory,
            $".{Path.GetFileNameWithoutExtension(snapshot.Output.OutputPath)}.mockups-{Guid.NewGuid():N}.mov");
        try
        {
            await RasterFramesAsync(
                snapshot,
                frameDirectory,
                (index) => $"frame_{index:D8}.png",
                progress,
                cancellationToken);
            progress.Report(new RenderQueueExecutionProgress(
                snapshot.Frames.Count,
                snapshot.Frames.Count,
                "Encoding MOV",
                RenderQueueStatus.Encoding));
            IReadOnlyList<string> profileArgs = mode.EncodingProfile switch
            {
                "prores_422_hq" =>
                [
                    "-c:v", "prores_ks",
                    "-profile:v", "3",
                    "-pix_fmt", "yuv422p10le",
                    "-vendor", "apl0",
                ],
                "prores_4444" =>
                [
                    "-c:v", "prores_ks",
                    "-profile:v", "4",
                    "-pix_fmt", "yuva444p10le",
                    "-alpha_bits", "16",
                    "-vendor", "apl0",
                ],
                _ => throw new InvalidOperationException(
                    $"Unsupported MOV encoding profile '{mode.EncodingProfile}'."),
            };
            await RunFfmpegAsync(
            [
                "-n",
                "-framerate", snapshot.Fps.ToString(),
                "-start_number", "0",
                "-i", Path.Combine(frameDirectory, "frame_%08d.png"),
                .. profileArgs,
                "-an",
                temporaryOutput,
            ],
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RenderOutputPathSecurity.RequireOutputTarget(snapshot.Output);
            File.Move(temporaryOutput, snapshot.Output.OutputPath);
        }
        finally
        {
            DeleteDirectory(frameDirectory);
            DeleteFile(temporaryOutput);
        }
    }

    private async Task RenderImageSequenceAsync(
        RenderJobSnapshot snapshot,
        RenderOutputModeDefinition mode,
        IProgress<RenderQueueExecutionProgress> progress,
        CancellationToken cancellationToken)
    {
        var outputParent = Path.GetDirectoryName(snapshot.Output.OutputPath)
            ?? throw new InvalidOperationException(
                "The image sequence output has no parent directory.");
        var stem = Path.GetFileName(snapshot.Output.OutputPath);
        var temporaryRoot = Path.Combine(
            outputParent,
            $".{stem}.mockups-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        var moveSource = temporaryRoot;
        try
        {
            if (mode.Extension == "png")
            {
                await RasterFramesAsync(
                    snapshot,
                    temporaryRoot,
                    (index) => $"{stem}_{index + 1:D8}.png",
                    progress,
                    cancellationToken);
            }
            else if (mode.Extension == "exr")
            {
                var source = Path.Combine(temporaryRoot, "source");
                var converted = Path.Combine(temporaryRoot, "converted");
                Directory.CreateDirectory(source);
                Directory.CreateDirectory(converted);
                await RasterFramesAsync(
                    snapshot,
                    source,
                    (index) => $"frame_{index:D8}.png",
                    progress,
                    cancellationToken);
                progress.Report(new RenderQueueExecutionProgress(
                    snapshot.Frames.Count,
                    snapshot.Frames.Count,
                    "Writing EXR sequence",
                    RenderQueueStatus.Encoding));
                await RunFfmpegAsync(
                [
                    "-n",
                    "-start_number", "0",
                    "-i", Path.Combine(source, "frame_%08d.png"),
                    "-c:v", "exr",
                    "-compression", "zip1",
                    "-format", "half",
                    "-pix_fmt", "gbrapf32le",
                    "-start_number", "1",
                    Path.Combine(converted, $"{stem}_%08d.exr"),
                ],
                    cancellationToken);
                moveSource = converted;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported image sequence format '{mode.Extension}'.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            RenderOutputPathSecurity.RequireOutputTarget(snapshot.Output);
            Directory.Move(moveSource, snapshot.Output.OutputPath);
            if (moveSource.Equals(temporaryRoot, StringComparison.Ordinal))
            {
                temporaryRoot = "";
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryRoot))
            {
                DeleteDirectory(temporaryRoot);
            }
        }
    }

    private async Task RasterFramesAsync(
        RenderJobSnapshot snapshot,
        string directory,
        Func<int, string> fileName,
        IProgress<RenderQueueExecutionProgress> progress,
        CancellationToken cancellationToken)
    {
        var width = Math.Max(
            1,
            (int)Math.Ceiling(snapshot.Metrics.CanvasWidth));
        var height = Math.Max(
            1,
            (int)Math.Ceiling(snapshot.Metrics.CanvasHeight));
        for (var index = 0; index < snapshot.Frames.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new RenderQueueExecutionProgress(
                index,
                snapshot.Frames.Count,
                $"Rendering {index + 1} / {snapshot.Frames.Count}",
                RenderQueueStatus.Rendering));
            await _rasterizer.RasterizeAsync(
                snapshot.Frames[index].Html,
                width,
                height,
                Path.Combine(directory, fileName(index)),
                "png",
                quality: 100,
                captureScale: 1,
                cancellationToken);
            progress.Report(new RenderQueueExecutionProgress(
                index + 1,
                snapshot.Frames.Count,
                $"Rendering {index + 1} / {snapshot.Frames.Count}",
                RenderQueueStatus.Rendering));
        }
    }

    private static async Task RunFfmpegAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var executable = ResolveFfmpegExecutable();
        var startInfo = DesktopChildProcess.CreateHiddenStartInfo(
            executable,
            Directory.GetCurrentDirectory());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "FFmpeg could not be started.");
        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Cancellation will be observed by the awaiting execution path.
            }
        });
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        var errorText = await stderr;
        _ = await stdout;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(errorText)
                    ? $"FFmpeg ended with exit code {process.ExitCode}."
                    : errorText[^Math.Min(4000, errorText.Length)..]);
        }
    }

    private static string ResolveFfmpegExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("MOCKUPS_FFMPEG");
        var executableName = OperatingSystem.IsWindows()
            ? "ffmpeg.exe"
            : "ffmpeg";
        var candidates = new[]
        {
            configured,
            Path.Combine(AppContext.BaseDirectory, "tools", executableName),
            OperatingSystem.IsMacOS() ? "/opt/homebrew/bin/ffmpeg" : null,
            OperatingSystem.IsMacOS() ? "/usr/local/bin/ffmpeg" : null,
            OperatingSystem.IsLinux() ? "/usr/bin/ffmpeg" : null,
        };
        return candidates.FirstOrDefault((candidate) =>
                !string.IsNullOrWhiteSpace(candidate)
                && File.Exists(candidate))
            ?? executableName;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Temporary cleanup must not replace the render result.
        }
    }

    private static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Temporary cleanup must not replace the render result.
        }
    }

    public void Dispose()
    {
        _rasterizer.Dispose();
    }
}
