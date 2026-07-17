using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public sealed record ShotSettings(
        string ProjectId,
        string Slug,
        int Version,
        int SortOrder,
        int ProjectDefaultFps,
        int Fps,
        int? FpsOverride,
        int DurationFrames,
        string OwnerActorId,
        string RenderPresetId,
        string CanvasJson,
        string MetadataJson);
    public sealed record AppSettings(
        string ProjectId,
        string BundleKey,
        string AppType,
        string ConfigJson,
        string MetadataJson);
    public sealed record ModuleSettings(
        string ProjectId,
        string RecordClassId,
        int SortOrder,
        string ConfigJson,
        string DesignPreviewJson,
        string MetadataJson);
    public sealed record ShotModuleChoice(
        string Id,
        string Name,
        string AppName,
        string AppId,
        string RecordClassId);
    public sealed record ShotModuleInstanceDraft(
        ShotModuleChoice Module,
        string VariantReference,
        string VariantName,
        string Name);
    public sealed record ModuleInstanceSettings(
        string ShotId,
        string AppId,
        string ModuleId,
        string Name,
        string Notes,
        int SortOrder,
        int DurationFrames,
        string TransitionJson,
        string ContentJson,
        string BehaviorJson,
        string AnimationJson,
        string MetadataJson);
    public sealed record ThemeSettings(
        string ProjectId,
        string Name,
        string Family,
        string IconThemeId,
        string StatusBarId,
        string NavigationBarId,
        string TokensJson,
        string MetadataJson);
    public sealed record ProductionFontSettings(
        string FamilyName,
        string Category,
        string SourceDirectory,
        string FilesJson);
    public sealed record IconThemeSettings(
        string Name,
        string AssetRoot,
        string MappingJson,
        string MetadataJson);
    public sealed record IconThemeToken(
        string Token,
        string Category,
        string File,
        string Description);
    public sealed record IconThemeTokenSvg(string Token, string File, string SvgText);
    public sealed record IconThemeRefreshResult(int ThemeCount, int CommonTokenCount, int OmittedTokenCount);
    public sealed record IconThemeReplaceSvgResult(string Token, string File);
    public sealed record IconThemeWriteAllSvgResult(string Token, int WrittenFileCount, IconThemeRefreshResult RefreshResult);
    public sealed record IconThemeSearchCandidate(string Provider, string SourceName, string PreviewUrl);
    public sealed record IconThemeSearchResult(
        IReadOnlyList<IconThemeSearchCandidate> Lucide,
        IReadOnlyList<IconThemeSearchCandidate> Material);
    public sealed record IconThemeGenerateResult(string Token, int WrittenFileCount, IconThemeRefreshResult RefreshResult);
    public sealed record ComponentClassSettings(
        string ProjectId,
        string ComponentType,
        string RecordClassId,
        string Name,
        string Notes,
        string ConfigJson,
        string DesignPreviewJson,
        string MetadataJson);
    public sealed record ThemeTokenOption(
        string Token,
        string Label,
        string Kind,
        string Value,
        string? LightColorHex,
        string? DarkColorHex);
    private sealed record IconThemeAssetMoveResult(string AssetRoot, string Name);
}
