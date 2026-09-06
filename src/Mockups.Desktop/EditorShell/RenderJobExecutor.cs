using Mockups.DesktopEditorShell.Common;
using System;
using System.Buffers.Binary;
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
        RenderOutputPathSecurity.EnsureOutputDirectory(snapshot.Output);
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
                snapshot.FrameStore.TotalFrames,
                snapshot.FrameStore.TotalFrames,
                "Encoding MOV",
                RenderQueueStatus.Encoding));
            var sourceDimensions = RenderRasterDimensions.Resolve(
                snapshot.Metrics);
            var profileArgs = RenderMovEncodingProfiles.Arguments(
                mode.EncodingProfile,
                sourceDimensions.Width,
                sourceDimensions.Height);
            await RunFfmpegAsync(
            [
                "-n",
                "-framerate", snapshot.Fps.ToString(),
                "-start_number", "0",
                "-i", Path.Combine(frameDirectory, "frame_%08d.png"),
                .. profileArgs,
                .. RenderColorMetadata.MovFfmpegArguments,
                "-an",
                temporaryOutput,
            ],
                cancellationToken);
            if (mode.PreservesAlpha)
            {
                QuickTimeAlphaAssociation.WritePremultipliedBlack(
                    temporaryOutput);
            }
            cancellationToken.ThrowIfCancellationRequested();
            RenderOutputPathSecurity.RequireOutputTarget(snapshot.Output);
            RenderOutputPublisher.PublishFile(
                temporaryOutput,
                snapshot.Output);
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
                    snapshot.FrameStore.TotalFrames,
                    snapshot.FrameStore.TotalFrames,
                    "Writing premultiplied PNG sequence",
                    RenderQueueStatus.Encoding));
                await RunFfmpegAsync(
                [
                    "-n",
                    "-start_number", "0",
                    "-i", Path.Combine(source, "frame_%08d.png"),
                    "-vf", RenderAlphaPremultiplication.Filter,
                    "-c:v", "png",
                    .. RenderColorMetadata.FfmpegArguments,
                    "-start_number", "1",
                    Path.Combine(
                        converted,
                        $"{stem}_%0{snapshot.Output.FramePadding}d.png"),
                ],
                    cancellationToken);
                moveSource = converted;
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
                    snapshot.FrameStore.TotalFrames,
                    snapshot.FrameStore.TotalFrames,
                    "Writing EXR sequence",
                    RenderQueueStatus.Encoding));
                await RunFfmpegAsync(
                [
                    "-n",
                    "-start_number", "0",
                    "-i", Path.Combine(source, "frame_%08d.png"),
                    "-vf", RenderAlphaPremultiplication.Filter,
                    "-c:v", "exr",
                    "-compression", "zip1",
                    "-format", "half",
                    "-pix_fmt", "gbrapf32le",
                    .. RenderColorMetadata.FfmpegArguments,
                    "-start_number", "1",
                    Path.Combine(
                        converted,
                        $"{stem}_%0{snapshot.Output.FramePadding}d.exr"),
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
            RenderOutputPublisher.PublishDirectory(
                moveSource,
                snapshot.Output);
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
        var dimensions = RenderRasterDimensions.Resolve(snapshot.Metrics);
        var store = new RenderSnapshotStore(
            snapshot.FrameStore.BatchRootPath,
            create: false);
        var renderedDocuments = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var index = 0;
        foreach (var frame in RenderSnapshotStore.ReadFrames(
                     snapshot.FrameStore))
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new RenderQueueExecutionProgress(
                index,
                snapshot.FrameStore.TotalFrames,
                $"Rendering {index + 1} / {snapshot.FrameStore.TotalFrames}",
                RenderQueueStatus.Rendering));
            var outputPath = Path.Combine(
                directory,
                fileName(index));
            if (renderedDocuments.TryGetValue(
                    frame.DocumentKey,
                    out var existingRaster))
            {
                File.Copy(existingRaster, outputPath);
            }
            else
            {
                var html = store.ReadDocument(
                    frame.DocumentKey);
                await _rasterizer.RasterizeAsync(
                    html,
                    dimensions.Width,
                    dimensions.Height,
                    outputPath,
                    "png",
                    quality: 100,
                    captureScale: 1,
                    cancellationToken,
                    assetResolver: store.ReadAsset);
                renderedDocuments[frame.DocumentKey] =
                    outputPath;
            }
            index++;
            progress.Report(new RenderQueueExecutionProgress(
                index,
                snapshot.FrameStore.TotalFrames,
                $"Rendering {index} / {snapshot.FrameStore.TotalFrames}",
                RenderQueueStatus.Rendering));
        }
        if (index != snapshot.FrameStore.TotalFrames)
        {
            throw new InvalidOperationException(
                "The frozen render frame store ended unexpectedly.");
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

    internal static string ResolveFfmpegExecutable()
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

internal static class RenderRasterDimensions
{
    public static (int Width, int Height) Resolve(
        DevicePreviewMetrics metrics) =>
        (
            Math.Max(1, (int)Math.Ceiling(metrics.CanvasWidth)),
            Math.Max(1, (int)Math.Ceiling(metrics.CanvasHeight))
        );
}

internal static class RenderOutputPublisher
{
    public static void PublishFile(
        string temporaryPath,
        RenderOutputTarget output)
    {
        RenderOutputPathSecurity.RequireOutputTarget(output);
        File.Move(
            temporaryPath,
            output.OutputPath,
            output.OverwriteExisting);
    }

    public static void PublishDirectory(
        string temporaryPath,
        RenderOutputTarget output)
    {
        RenderOutputPathSecurity.RequireOutputTarget(output);
        if (Directory.Exists(output.OutputPath))
        {
            if (!output.OverwriteExisting)
            {
                throw new IOException(
                    "The queued output already exists and was not approved for replacement.");
            }
            Directory.Delete(output.OutputPath, recursive: true);
        }
        Directory.Move(temporaryPath, output.OutputPath);
    }
}

internal static class RenderAlphaPremultiplication
{
    public const string Filter =
        "premultiply=inplace=1,setparams=alpha_mode=straight";
}

internal static class RenderColorMetadata
{
    // Preview is rasterized from the CSS sRGB working space. MOV outputs keep
    // that transfer while declaring the Rec.709 primaries shared by sRGB.
    public static readonly IReadOnlyList<string> FfmpegArguments =
    [
        "-color_primaries", "bt709",
        "-color_trc", "iec61966-2-1",
        "-colorspace", "bt709",
    ];

    public static readonly IReadOnlyList<string> MovFfmpegArguments =
    [
        "-color_range", "tv",
        .. FfmpegArguments,
    ];

    public const string LimitedSrgbBt709FrameFilter =
        "setparams=range=limited:color_primaries=bt709:color_trc=iec61966-2-1:colorspace=bt709";

    public const string LimitedSrgbBt709PremultipliedFrameFilter =
        LimitedSrgbBt709FrameFilter + ":alpha_mode=premultiplied";
}

internal static class RenderMovEncodingProfiles
{
    public static IReadOnlyList<string> Arguments(
        string encodingProfile,
        int sourceWidth,
        int sourceHeight)
    {
        var videoFilter = VideoFilter(
            encodingProfile,
            sourceWidth,
            sourceHeight);
        return encodingProfile switch
        {
            "prores_422_hq" =>
            [
                "-vf", videoFilter,
                "-c:v", "prores_ks",
                "-profile:v", "3",
                "-pix_fmt", "yuv422p10le",
                "-vendor", "apl0",
                "-movflags", "+write_colr",
            ],
            "prores_4444" =>
            [
                "-vf", videoFilter,
                "-c:v", "prores_ks",
                "-profile:v", "4",
                "-pix_fmt", "yuva444p10le",
                "-alpha_bits", "16",
                "-vendor", "apl0",
                "-movflags", "+write_colr",
            ],
            "h264_light" =>
            [
                "-vf", videoFilter,
                "-c:v", "libx264",
                "-preset", "medium",
                "-b:v", "8M",
                "-maxrate", "10M",
                "-bufsize", "16M",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart+write_colr",
            ],
            "h264_standard" =>
            [
                "-vf", videoFilter,
                "-c:v", "libx264",
                "-preset", "medium",
                "-b:v", "20M",
                "-maxrate", "25M",
                "-bufsize", "40M",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart+write_colr",
            ],
            "h264_high" =>
            [
                "-vf", videoFilter,
                "-c:v", "libx264",
                "-preset", "slow",
                "-b:v", "40M",
                "-maxrate", "50M",
                "-bufsize", "80M",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart+write_colr",
            ],
            _ => throw new InvalidOperationException(
                $"Unsupported MOV encoding profile '{encodingProfile}'."),
        };
    }

    public static (int Width, int Height) OutputDimensions(
        string encodingProfile,
        int sourceWidth,
        int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new InvalidOperationException(
                "MOV source dimensions must be positive.");
        }
        if (!encodingProfile.StartsWith("h264_", StringComparison.Ordinal)
            || (sourceWidth % 2 == 0 && sourceHeight % 2 == 0))
        {
            return (sourceWidth, sourceHeight);
        }

        var scale = Math.Max(
            sourceWidth % 2 == 0
                ? 1d
                : (sourceWidth + 1d) / sourceWidth,
            sourceHeight % 2 == 0
                ? 1d
                : (sourceHeight + 1d) / sourceHeight);
        return (
            NearestEven(sourceWidth * scale),
            NearestEven(sourceHeight * scale));
    }

    private static string VideoFilter(
        string encodingProfile,
        int sourceWidth,
        int sourceHeight)
    {
        var output = OutputDimensions(
            encodingProfile,
            sourceWidth,
            sourceHeight);
        var filters = new List<string>
        {
            RenderAlphaPremultiplication.Filter,
        };
        if (output != (sourceWidth, sourceHeight))
        {
            filters.Add(
                $"scale={output.Width}:{output.Height}:flags=lanczos");
        }
        filters.Add(encodingProfile == "prores_4444"
            ? RenderColorMetadata
                .LimitedSrgbBt709PremultipliedFrameFilter
            : RenderColorMetadata.LimitedSrgbBt709FrameFilter);
        return string.Join(',', filters);
    }

    private static int NearestEven(double value) =>
        Math.Max(
            2,
            2 * (int)Math.Round(
                value / 2d,
                MidpointRounding.AwayFromZero));
}

internal static class QuickTimeAlphaAssociation
{
    public const ushort Copy = 0x0000;
    public const ushort PremultipliedBlack = 0x0102;

    private static readonly HashSet<string> ContainerAtoms =
    [
        "moov",
        "trak",
        "mdia",
        "minf",
    ];

    public static void WritePremultipliedBlack(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        var atom = RequireVideoMediaHeader(stream);
        var current = ReadGraphicsMode(stream, atom);
        if (current is not Copy and not PremultipliedBlack)
        {
            throw new InvalidOperationException(
                $"QuickTime video media header uses unsupported graphics mode 0x{current:X4}.");
        }
        Span<byte> value = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(
            value,
            PremultipliedBlack);
        stream.Position = atom.Offset + 12;
        stream.Write(value);
        stream.Flush(flushToDisk: true);
    }

    public static ushort ReadGraphicsMode(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return ReadGraphicsMode(
            stream,
            RequireVideoMediaHeader(stream));
    }

    private static ushort ReadGraphicsMode(
        FileStream stream,
        QuickTimeAtom atom)
    {
        Span<byte> header = stackalloc byte[6];
        stream.Position = atom.Offset + 8;
        stream.ReadExactly(header);
        if (BinaryPrimitives.ReadUInt32BigEndian(header) != 1)
        {
            throw new InvalidOperationException(
                "QuickTime video media header must use version 0 and flag 1.");
        }
        return BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
    }

    private static QuickTimeAtom RequireVideoMediaHeader(
        FileStream stream)
    {
        var atoms = new List<QuickTimeAtom>();
        ReadAtoms(stream, 0, stream.Length, atoms);
        if (atoms.Count != 1)
        {
            throw new InvalidOperationException(
                $"QuickTime MOV requires exactly one video media header; found {atoms.Count}.");
        }
        return atoms[0];
    }

    private static void ReadAtoms(
        FileStream stream,
        long start,
        long end,
        List<QuickTimeAtom> videoMediaHeaders)
    {
        var offset = start;
        Span<byte> header = stackalloc byte[16];
        while (offset < end)
        {
            if (end - offset < 8)
            {
                throw new InvalidOperationException(
                    "QuickTime atom header is truncated.");
            }
            stream.Position = offset;
            stream.ReadExactly(header[..8]);
            var compactSize = BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = System.Text.Encoding.ASCII.GetString(header[4..8]);
            var headerSize = 8L;
            long size;
            if (compactSize == 1)
            {
                stream.ReadExactly(header[8..16]);
                size = checked((long)BinaryPrimitives.ReadUInt64BigEndian(
                    header[8..16]));
                headerSize = 16;
            }
            else
            {
                size = compactSize == 0
                    ? end - offset
                    : compactSize;
            }
            if (size < headerSize || size > end - offset)
            {
                throw new InvalidOperationException(
                    $"QuickTime atom '{type}' has invalid size {size}.");
            }
            var atom = new QuickTimeAtom(offset, size, type);
            if (type == "vmhd")
            {
                if (size < 20)
                {
                    throw new InvalidOperationException(
                        "QuickTime video media header is incomplete.");
                }
                videoMediaHeaders.Add(atom);
            }
            else if (ContainerAtoms.Contains(type))
            {
                ReadAtoms(
                    stream,
                    offset + headerSize,
                    offset + size,
                    videoMediaHeaders);
            }
            offset += size;
        }
    }

    private sealed record QuickTimeAtom(
        long Offset,
        long Size,
        string Type);
}
