using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RenderQueueStatus
{
    public const string Pending = "PENDING";
    public const string Preparing = "PREPARING";
    public const string Rendering = "RENDERING";
    public const string Encoding = "ENCODING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Canceled = "CANCELED";

    public static bool IsTerminal(string value) =>
        value is Completed or Failed or Canceled;

    public static bool IsActive(string value) =>
        value is Preparing or Rendering or Encoding;

    public static bool IsKnown(string value) =>
        value is Pending or Preparing or Rendering or Encoding
            or Completed or Failed or Canceled;
}

internal static class RenderQueueAppearance
{
    public const string Light = "light";
    public const string Dark = "dark";
    public const string Both = "both";

    public static IReadOnlyList<string> Expand(string value) => value switch
    {
        Light => [Light],
        Dark => [Dark],
        Both => [Light, Dark],
        _ => throw new InvalidOperationException(
            $"Unsupported render appearance '{value}'."),
    };
}

internal sealed record RenderOutputModeDefinition(
    string Id,
    string Label,
    string Kind,
    string Extension,
    string EncodingProfile,
    bool PreservesAlpha);

internal static class RenderOutputModes
{
    public const string MovProRes422Hq = "mov_prores_422_hq";
    public const string MovProRes4444 = "mov_prores_4444";
    public const string MovH264Light = "mov_h264_light";
    public const string MovH264Standard = "mov_h264_standard";
    public const string MovH264High = "mov_h264_high";
    public const string PngSequence = "png_sequence";
    public const string ExrSequence = "exr_sequence";

    public static readonly IReadOnlyList<RenderOutputModeDefinition> All =
    [
        new(
            MovProRes422Hq,
            "MOV · ProRes 422 HQ",
            "mov",
            "mov",
            "prores_422_hq",
            false),
        new(
            MovProRes4444,
            "MOV · ProRes 4444 (alpha)",
            "mov",
            "mov",
            "prores_4444",
            true),
        new(
            MovH264Light,
            "MOV · H.264 Light · 8 Mb/s",
            "mov",
            "mov",
            "h264_light",
            false),
        new(
            MovH264Standard,
            "MOV · H.264 Standard · 20 Mb/s",
            "mov",
            "mov",
            "h264_standard",
            false),
        new(
            MovH264High,
            "MOV · H.264 High · 40 Mb/s",
            "mov",
            "mov",
            "h264_high",
            false),
        new(
            PngSequence,
            "PNG sequence",
            "image_sequence",
            "png",
            "png",
            true),
        new(
            ExrSequence,
            "EXR sequence",
            "image_sequence",
            "exr",
            "exr",
            true),
    ];

    public static RenderOutputModeDefinition Require(string id) =>
        All.SingleOrDefault((mode) => mode.Id.Equals(id, StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"Unsupported render output mode '{id}'.");
}

internal sealed record RenderShotContext(
    string ProjectId,
    string ShotId,
    string ShotName,
    string ActorId,
    string ActorName);

internal sealed record RenderOutputTarget(
    string ProductionId,
    string StructureEntryId,
    string RootPath,
    string RelativeDirectory,
    string BaseName,
    string Appearance,
    int Version,
    int VersionPadding,
    string OutputModeId,
    string OutputPath);

internal sealed record RenderFrozenFrame(
    int LocalFrame,
    string Html);

internal sealed record RenderJobSnapshot(
    string Schema,
    int Version,
    RenderShotContext Context,
    string DeviceId,
    string DeviceName,
    string ThemeId,
    string ThemeName,
    string RequestedAppearance,
    DevicePreviewMetrics Metrics,
    int Fps,
    IReadOnlyList<RenderFrozenFrame> Frames,
    IReadOnlyDictionary<string, string> Assets,
    RenderOutputTarget Output)
{
    public const string CurrentSchema = "mockups_render_job_snapshot";
    public const int CurrentVersion = 1;

    public void Validate()
    {
        if (Schema != CurrentSchema
            || Version != CurrentVersion
            || string.IsNullOrWhiteSpace(Context.ProjectId)
            || string.IsNullOrWhiteSpace(Context.ShotId)
            || string.IsNullOrWhiteSpace(Context.ActorId)
            || string.IsNullOrWhiteSpace(DeviceId)
            || string.IsNullOrWhiteSpace(ThemeId)
            || RequestedAppearance is not RenderQueueAppearance.Light
                and not RenderQueueAppearance.Dark
            || Metrics.CanvasWidth <= 0
            || Metrics.CanvasHeight <= 0
            || Fps <= 0
            || Frames.Count == 0
            || Frames.Any((frame) =>
                frame.LocalFrame < 0
                || string.IsNullOrWhiteSpace(frame.Html))
            || Frames.Select((frame) => frame.LocalFrame)
                .Distinct().Count() != Frames.Count
            || Assets.Any((asset) =>
                asset.Key.Length != 64
                || string.IsNullOrWhiteSpace(asset.Value))
            || Output.Appearance != RequestedAppearance
            || Output.Version <= 0
            || Output.VersionPadding is < 1 or > 8
            || string.IsNullOrWhiteSpace(Output.OutputPath))
        {
            throw new InvalidOperationException(
                "The render job snapshot is incomplete or unsupported.");
        }
        _ = RenderOutputModes.Require(Output.OutputModeId);
    }
}

internal sealed record RenderJobSummary(
    RenderShotContext Context,
    string DeviceName,
    string ThemeName,
    string Appearance,
    int TotalFrames,
    RenderOutputTarget Output);

internal sealed record RenderQueueProgress(
    int Current,
    int Total,
    string Phase);

internal sealed class RenderQueueJob
{
    public string Id { get; init; } = "";
    public string BatchId { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string UpdatedAt { get; set; } = "";
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public string Status { get; set; } = RenderQueueStatus.Pending;
    public RenderQueueProgress Progress { get; set; } =
        new(0, 0, "Pending");
    public bool CancelRequested { get; set; }
    public RenderJobSnapshot? Snapshot { get; set; }
    public RenderJobSummary Summary { get; set; } =
        new(
            new RenderShotContext("", "", "", "", ""),
            "",
            "",
            "",
            0,
            new RenderOutputTarget("", "", "", "", "", "", 0, 3, "", ""));
    public string? Error { get; set; }
}

internal sealed class RenderQueueDocument
{
    public string Schema { get; init; } = "mockups_render_queue";
    public int Version { get; init; } = 1;
    public bool Paused { get; set; }
    public List<RenderQueueJob> Jobs { get; init; } = [];
    public Dictionary<string, string> LastRouteByProject { get; init; } =
        new(StringComparer.Ordinal);
}

internal sealed record RenderQueueJobView(
    string Id,
    string BatchId,
    string CreatedAt,
    string Status,
    RenderQueueProgress Progress,
    bool SnapshotAvailable,
    RenderJobSummary Summary,
    string? Error);

internal sealed record RenderQueueExecutionProgress(
    int Current,
    int Total,
    string Phase,
    string Status);

internal interface IRenderJobExecutor : IDisposable
{
    System.Threading.Tasks.Task ExecuteAsync(
        RenderJobSnapshot snapshot,
        IProgress<RenderQueueExecutionProgress> progress,
        System.Threading.CancellationToken cancellationToken);
}
