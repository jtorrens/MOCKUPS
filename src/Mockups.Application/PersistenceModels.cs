using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

public sealed record ProjectSettings(
    string Slug,
    int DefaultFps,
    string MediaRoot,
    ProductionOutputSettings ProductionOutput);

public sealed record EpisodeSettings(
    string Slug,
    int SortOrder);

public sealed record EpisodeRecord(
    string Id,
    string ProjectId,
    string Name,
    string Slug,
    string Notes,
    int SortOrder);

public sealed record ShotSettings(
    string ProjectId,
    string EpisodeId,
    string Slug,
    int ShotNumber,
    int Version,
    int SortOrder,
    int ProjectDefaultFps,
    int Fps,
    int? FpsOverride,
    int DurationFrames,
    string OwnerActorId,
    string? DeviceOverrideId,
    string? ThemeOverrideId,
    string CanvasJson,
    string ReferenceVideoJson,
    string MetadataJson)
{
    public ShotReferenceVideoDocument ReferenceVideo =>
        ShotReferenceVideoDocument.ParseRequired(
            ReferenceVideoJson,
            $"Shot '{Slug}' reference_video_json");

    public string EffectiveDeviceId(string actorDefaultDeviceId) =>
        DeviceOverrideId ?? actorDefaultDeviceId;

    public string EffectiveThemeId(string actorDefaultThemeId) =>
        ThemeOverrideId ?? actorDefaultThemeId;
}

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
    int ActionDelayFrames,
    string TransitionJson,
    string ContentJson,
    string BehaviorJson,
    string AnimationJson,
    string MetadataJson,
    int FrameRate);

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

public sealed record IconThemeTokenSvg(
    string Token,
    string File,
    string SvgText);

public sealed record IconThemeRefreshResult(
    int ThemeCount,
    int CommonTokenCount,
    int OmittedTokenCount);

public sealed record IconThemeReplaceSvgResult(
    string Token,
    string File);

public sealed record IconThemeWriteAllSvgResult(
    string Token,
    int WrittenFileCount,
    IconThemeRefreshResult RefreshResult);

public sealed record IconThemeSearchCandidate(
    string Provider,
    string SourceName,
    string PreviewUrl);

public sealed record IconThemeSearchResult(
    IReadOnlyList<IconThemeSearchCandidate> Lucide,
    IReadOnlyList<IconThemeSearchCandidate> Material);

public sealed record IconThemeGenerateResult(
    string Token,
    int WrittenFileCount,
    IconThemeRefreshResult RefreshResult);

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

public sealed record ComponentVariantSelectionSettings(
    string ProjectId,
    string ComponentType,
    string RecordClassId,
    string ConfigJson);

public sealed record EmbeddedComponentUsage(
    string ParentComponentClassId,
    string ParentComponentName,
    string ParentComponentType,
    string SlotFieldId,
    string SlotLabel,
    bool HasOverrides,
    string SourceNodeId = "");

public sealed record ComponentVariantReferenceUsage(
    string SourceKind,
    string SourceName,
    string Detail,
    string TargetNodeId,
    EmbeddedComponentUsage? EmbeddedUsage);

public sealed record ComponentClassVariant(
    string Id,
    string Name,
    bool IsProtected,
    bool IsLocked,
    string ConfigJson);

public sealed record ModuleVariant(
    string Id,
    string Name,
    bool IsProtected,
    bool IsLocked,
    string ConfigJson);

public sealed record ModuleInstanceSlot(
    string Id,
    string Name,
    string ModuleName,
    int SortOrder,
    string TransitionJson,
    string TransitionType,
    int StoredDurationFrames,
    int ActionDelayFrames);

public sealed record PaletteColorSettings(
    string Token,
    string ValueHex,
    bool IsNeutral,
    string Source,
    bool IsProtected,
    bool HiddenFromPickers,
    string Note);

public sealed record DeviceSettings(
    string ProjectId,
    string Name,
    string Manufacturer,
    string Model,
    string OsFamily,
    string MetricsJson);

public sealed record ActorSettings(
    string ProjectId,
    string DisplayName,
    string ShortName,
    string DefaultDeviceId,
    string DefaultThemeId,
    string MetadataJson);

public sealed record ProductionFontFace(
    string FontId,
    string FamilyName,
    string Category,
    string RelativePath,
    int Weight,
    string Style);

public enum ReferenceUsageScope
{
    Design,
    Production,
}
