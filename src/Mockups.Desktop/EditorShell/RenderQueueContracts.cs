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

internal static class RenderThemeStrategy
{
    public const string Screen = "screen";
    public const string Forced = "forced";

    public static bool IsValid(string strategy, string themeId) =>
        strategy switch
        {
            Screen => string.IsNullOrEmpty(themeId),
            Forced => !string.IsNullOrWhiteSpace(themeId),
            _ => false,
        };
}

internal sealed record RenderOutputModeDefinition(
    string Id,
    string Label,
    string Kind,
    string Extension,
    string EncodingProfile,
    string ColorRange,
    string AlphaMode)
{
    public bool PreservesAlpha =>
        !AlphaMode.Equals(RenderOutputAlphaModes.None, StringComparison.Ordinal);
}

internal static class RenderOutputColorRanges
{
    public const string Full = "full";
    public const string Legal = "legal";
}

internal static class RenderOutputAlphaModes
{
    public const string None = "none";
    public const string PremultipliedBlack = "premultiplied_black";
}

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
            RenderOutputColorRanges.Legal,
            RenderOutputAlphaModes.None),
        new(
            MovProRes4444,
            "MOV · ProRes 4444 (alpha)",
            "mov",
            "mov",
            "prores_4444",
            RenderOutputColorRanges.Legal,
            RenderOutputAlphaModes.PremultipliedBlack),
        new(
            MovH264Light,
            "MOV · H.264 Light · 8 Mb/s",
            "mov",
            "mov",
            "h264_light",
            RenderOutputColorRanges.Legal,
            RenderOutputAlphaModes.None),
        new(
            MovH264Standard,
            "MOV · H.264 Standard · 20 Mb/s",
            "mov",
            "mov",
            "h264_standard",
            RenderOutputColorRanges.Legal,
            RenderOutputAlphaModes.None),
        new(
            MovH264High,
            "MOV · H.264 High · 40 Mb/s",
            "mov",
            "mov",
            "h264_high",
            RenderOutputColorRanges.Legal,
            RenderOutputAlphaModes.None),
        new(
            PngSequence,
            "PNG sequence",
            "image_sequence",
            "png",
            "png",
            RenderOutputColorRanges.Full,
            RenderOutputAlphaModes.PremultipliedBlack),
        new(
            ExrSequence,
            "EXR sequence",
            "image_sequence",
            "exr",
            "exr",
            RenderOutputColorRanges.Full,
            RenderOutputAlphaModes.PremultipliedBlack),
    ];

    public static RenderOutputModeDefinition Require(string id) =>
        All.SingleOrDefault((mode) => mode.Id.Equals(id, StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"Unsupported render output mode '{id}'.");

    public static string TechnicalSummary(string id)
    {
        var mode = Require(id);
        var range = mode.ColorRange switch
        {
            RenderOutputColorRanges.Full => "Full",
            RenderOutputColorRanges.Legal => "Legal",
            _ => throw new InvalidOperationException(
                $"Unsupported render color range '{mode.ColorRange}'."),
        };
        var alpha = mode.AlphaMode switch
        {
            RenderOutputAlphaModes.None => "",
            RenderOutputAlphaModes.PremultipliedBlack =>
                " · Alpha: Premultiplied black",
            _ => throw new InvalidOperationException(
                $"Unsupported render alpha mode '{mode.AlphaMode}'."),
        };
        return $"Color range: {range}{alpha}";
    }
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
    string OutputPath,
    int FramePadding = 8,
    bool OverwriteExisting = false);

internal sealed record RenderFrameStoreReference(
    string BatchRootPath,
    string ManifestFileName,
    string Appearance,
    int TotalFrames);

internal sealed record RenderJobSnapshot(
    string Schema,
    int Version,
    RenderShotContext Context,
    string DeviceId,
    string DeviceName,
    string ThemeStrategy,
    string ThemeId,
    string ThemeName,
    string RequestedAppearance,
    DevicePreviewMetrics Metrics,
    int Fps,
    RenderFrameStoreReference FrameStore,
    RenderOutputTarget Output)
{
    public const string CurrentSchema = "mockups_render_job_snapshot";
    public const int CurrentVersion = 5;

    public void Validate()
    {
        if (Schema != CurrentSchema
            || Version != CurrentVersion
            || string.IsNullOrWhiteSpace(Context.ProjectId)
            || string.IsNullOrWhiteSpace(Context.ShotId)
            || string.IsNullOrWhiteSpace(Context.ActorId)
            || string.IsNullOrWhiteSpace(DeviceId)
            || !RenderThemeStrategy.IsValid(ThemeStrategy, ThemeId)
            || RequestedAppearance is not RenderQueueAppearance.Light
                and not RenderQueueAppearance.Dark
            || Metrics.CanvasWidth <= 0
            || Metrics.CanvasHeight <= 0
            || Fps <= 0
            || FrameStore is null
            || FrameStore.Appearance != RequestedAppearance
            || Output.Appearance != RequestedAppearance
            || Output.Version <= 0
            || Output.VersionPadding is < 1 or > 8
            || Output.FramePadding is < 1 or > 8
            || string.IsNullOrWhiteSpace(Output.OutputPath))
        {
            throw new InvalidOperationException(
                "The render job snapshot is incomplete or unsupported.");
        }
        RenderSnapshotStore.ValidateReference(FrameStore);
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

internal sealed record RenderJobPlan(
    string Schema,
    int Version,
    string ProjectId,
    string ShotId,
    string ShotName,
    string DeviceId,
    string ThemeStrategy,
    string ThemeId,
    string RequestedAppearance,
    RenderOutputTarget Output)
{
    public const string CurrentSchema = "mockups_render_job_plan";
    public const int CurrentVersion = 2;

    public void Validate()
    {
        if (Schema != CurrentSchema
            || Version != CurrentVersion
            || string.IsNullOrWhiteSpace(ProjectId)
            || string.IsNullOrWhiteSpace(ShotId)
            || string.IsNullOrWhiteSpace(ShotName)
            || string.IsNullOrWhiteSpace(DeviceId)
            || !RenderThemeStrategy.IsValid(ThemeStrategy, ThemeId)
            || RequestedAppearance is not RenderQueueAppearance.Light
                and not RenderQueueAppearance.Dark
            || Output.Appearance != RequestedAppearance)
        {
            throw new InvalidOperationException(
                "The live render job plan is incomplete or unsupported.");
        }
        _ = RenderOutputModes.Require(Output.OutputModeId);
    }
}

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
    public RenderJobPlan Plan { get; init; } =
        new(
            RenderJobPlan.CurrentSchema,
            RenderJobPlan.CurrentVersion,
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            new RenderOutputTarget("", "", "", "", "", "", 0, 3, "", "", 8));
    public RenderJobSummary Summary { get; set; } =
        new(
            new RenderShotContext("", "", "", "", ""),
            "",
            "",
            "",
            0,
            new RenderOutputTarget("", "", "", "", "", "", 0, 3, "", "", 8));
    public string? Error { get; set; }
}

internal sealed class RenderQueueDocument
{
    public string Schema { get; init; } = "mockups_render_queue";
    public int Version { get; init; } = 3;
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
    bool PlanAvailable,
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

internal interface IRenderJobPreparer
{
    System.Threading.Tasks.Task<RenderJobSnapshot> PrepareAsync(
        RenderJobPlan plan,
        string temporaryRoot,
        IProgress<RenderSnapshotFreezeProgress> progress,
        System.Threading.CancellationToken cancellationToken);
}
