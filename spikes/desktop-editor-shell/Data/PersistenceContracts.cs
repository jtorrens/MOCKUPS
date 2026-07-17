using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

internal sealed record ProjectSettings(string Slug, int DefaultFps, string MediaRoot);

internal sealed record EpisodeSettings(string Slug, int SortOrder);

internal sealed record RenderPresetSettings(
    string ProjectId,
    string Name,
    int Width,
    int Height,
    int Fps,
    string Format,
    string CodecJson,
    string ColorJson,
    string QualityJson,
    string ExportJson,
    string MetadataJson);

internal sealed record ProjectRecord(string Id, string Name, string Notes);

internal sealed record EpisodeRecord(
    string Id,
    string ProjectId,
    string Name,
    string Slug,
    string Notes,
    int SortOrder);

internal sealed record ShotRecord(
    string Id,
    string EpisodeId,
    string ProjectId,
    string Name,
    string Slug,
    int Version,
    string Notes,
    int SortOrder,
    int? FpsOverride,
    int DurationFrames,
    string OwnerActorId,
    string RenderPresetId,
    string CanvasJson,
    string MetadataJson);

internal sealed record RenderPresetRecord(
    string Id,
    string ProjectId,
    string Name,
    int Width,
    int Height,
    int Fps,
    string Format,
    string CodecJson,
    string ColorJson,
    string QualityJson,
    string ExportJson,
    string MetadataJson);

internal sealed record RenderPresetOption(string Value, string Label);

internal sealed record PaletteColorSettings(
    string Token,
    string ValueHex,
    bool IsNeutral,
    string Source,
    bool IsProtected,
    bool HiddenFromPickers,
    string Note);

internal sealed record DeviceSettings(
    string Name,
    string Manufacturer,
    string Model,
    string OsFamily,
    string MetricsJson);

internal sealed record ActorSettings(
    string ProjectId,
    string DisplayName,
    string ShortName,
    string DefaultDeviceId,
    string DefaultThemeId,
    string MetadataJson);

internal sealed record ResourceOption(string Value, string Label);

internal sealed record PaletteColorOption(
    string Token,
    string Label,
    string ColorHex,
    bool IsNeutral);

internal sealed record PaletteColorRecord(
    string Id,
    string ProjectId,
    string Token,
    string ValueHex,
    string Note,
    bool IsNeutral,
    string MetadataJson);

internal sealed record DeviceRecord(
    string Id,
    string ProjectId,
    string Name,
    string Manufacturer,
    string Model,
    string OsFamily,
    string MetricsJson);

internal sealed record ActorRecord(
    string Id,
    string ProjectId,
    string DisplayName,
    string ShortName,
    string DefaultDeviceId,
    string DefaultThemeId,
    string MetadataJson);

internal sealed record ThemeRecord(
    string Id,
    string ProjectId,
    string Name,
    string Family,
    string IconThemeId,
    string StatusBarId,
    string NavigationBarId,
    string TokensJson,
    string MetadataJson);

internal sealed record ProductionFontRecord(
    string Id,
    string ProjectId,
    string FamilyName,
    string Category,
    string SourceDirectory,
    string FilesJson,
    string MetadataJson);

internal sealed record IconThemeRecord(
    string Id,
    string ProjectId,
    string Name,
    string AssetRoot,
    string MappingJson,
    string MetadataJson);

internal sealed record AppDefinitionRecord(
    string Id,
    string ProjectId,
    string RecordClassId,
    string Name,
    string BundleKey,
    string AppType,
    string Notes,
    int SortOrder,
    string ConfigJson,
    string MetadataJson);

internal sealed record ModuleDefinitionRecord(
    string Id,
    string AppId,
    string ProjectId,
    string RecordClassId,
    string Name,
    string Notes,
    int SortOrder,
    string ConfigJson,
    string DesignPreviewJson,
    string MetadataJson);

internal sealed record ComponentClassDefinitionRecord(
    string Id,
    string ProjectId,
    string ComponentType,
    string RecordClassId,
    string Name,
    string Notes,
    string ConfigJson,
    string DesignPreviewJson,
    string MetadataJson);

internal sealed record ModuleInstanceRecord(
    string Id,
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

internal enum ReferenceUsageScope
{
    Design,
    Production,
}

internal sealed record ReferenceTarget(ProjectTreeNodeKind Kind, string Id);

internal sealed record ReferenceEmbeddedContext(
    string ParentComponentClassId,
    string ParentComponentName,
    string ParentComponentType,
    string SlotFieldId,
    string SlotLabel,
    bool HasOverrides,
    string SourceNodeId);

internal sealed record ReferenceUsageRecord(
    ReferenceTarget Referenced,
    string SourceNodeId,
    ProjectTreeNodeKind SourceKind,
    string SourceTypeLabel,
    string SourceName,
    string FieldLabel,
    ReferenceUsageScope Scope,
    ReferenceEmbeddedContext? EmbeddedContext = null);

internal interface IEditorLayoutRepository
{
    EditorLayout Load(string recordClassId);

    void Save(string recordClassId, EditorLayout layout);
}

internal interface IProjectEpisodeRepository
{
    ProjectSettings GetProjectSettings(string projectId);

    ProjectSettings GetProjectSettings(SqliteConnection connection, string projectId);

    void UpdateProjectField(string projectId, string fieldId, string value);

    EpisodeSettings GetEpisodeSettings(string episodeId);

    void UpdateEpisodeField(string episodeId, string fieldId, string value);

    IReadOnlyList<ProjectRecord> QueryProjects(SqliteConnection connection);

    IReadOnlyList<EpisodeRecord> QueryEpisodes(SqliteConnection connection);

    EpisodeRecord CreateEpisode(SqliteConnection connection, string projectId);

    EpisodeRecord DuplicateEpisode(SqliteConnection connection, string sourceEpisodeId, string copyName);

    void DeleteEpisode(SqliteConnection connection, string episodeId);

    void UpdateProjectNode(SqliteConnection connection, string projectId, string name, string notes);

    void UpdateEpisodeNode(SqliteConnection connection, string episodeId, string name, string notes);
}

internal interface IShotRepository
{
    ShotRecord Get(string shotId);

    ShotRecord Get(SqliteConnection connection, string shotId);

    IReadOnlyList<ShotRecord> QueryAll(SqliteConnection connection);

    IReadOnlyList<ShotRecord> QueryByEpisode(SqliteConnection connection, string episodeId);

    ShotRecord Create(SqliteConnection connection, string episodeId, string actorId);

    ShotRecord Duplicate(SqliteConnection connection, string sourceId, string id, string name);

    void DuplicateForEpisode(
        SqliteConnection connection,
        string sourceEpisodeId,
        string targetEpisodeId);

    void ClearFpsOverride(SqliteConnection connection, string shotId);

    void UpdateField(SqliteConnection connection, string shotId, string fieldId, string value);

    void UpdateDuration(SqliteConnection connection, string shotId, int durationFrames);

    void UpdateNode(SqliteConnection connection, string shotId, string name, string notes);

    void Delete(SqliteConnection connection, string shotId);
}

internal interface IRenderPresetRepository
{
    RenderPresetSettings GetSettings(string renderPresetId);

    void UpdateField(string renderPresetId, string fieldId, string value);

    IReadOnlyList<RenderPresetOption> GetOptions(string projectId);

    IReadOnlyList<RenderPresetRecord> QueryAll(SqliteConnection connection);

    RenderPresetRecord Create(SqliteConnection connection, string projectId);

    RenderPresetRecord Duplicate(SqliteConnection connection, string sourceId, string copyName);

    void Delete(SqliteConnection connection, string renderPresetId);

    void Rename(SqliteConnection connection, string renderPresetId, string name);
}

internal interface IPaletteRepository
{
    PaletteColorSettings GetSettings(string colorId);

    void UpdateField(string colorId, string fieldId, string value);

    IReadOnlyList<PaletteColorOption> GetOptions(string projectId);

    IReadOnlyDictionary<string, string> GetColorMap(string projectId);

    IReadOnlyDictionary<string, bool> GetNeutralMap(string projectId);

    IReadOnlyList<PaletteColorRecord> QueryAll(SqliteConnection connection);

    PaletteColorRecord Create(SqliteConnection connection, string projectId);

    PaletteColorRecord Duplicate(SqliteConnection connection, string sourceId);

    void Delete(SqliteConnection connection, string colorId);

    void UpdateNode(SqliteConnection connection, string colorId, string token, string note);
}

internal interface IDeviceRepository
{
    DeviceSettings GetSettings(string deviceId);

    void UpdateField(string deviceId, string fieldId, string value);

    IReadOnlyList<ResourceOption> GetOptions(string projectId);

    IReadOnlyList<DeviceRecord> QueryAll(SqliteConnection connection);

    DeviceRecord Create(SqliteConnection connection, string projectId);

    DeviceRecord CreateImported(
        SqliteConnection connection,
        string projectId,
        string name,
        string manufacturer,
        string model,
        string osFamily,
        string metricsJson);

    DeviceRecord Duplicate(SqliteConnection connection, string sourceId, string copyName);

    void Delete(SqliteConnection connection, string deviceId);

    void Rename(SqliteConnection connection, string deviceId, string name);
}

internal interface IActorRepository
{
    ActorSettings GetSettings(string actorId);

    void UpdateField(string actorId, string fieldId, string value);

    IReadOnlyList<ResourceOption> GetOptions(string projectId);

    IReadOnlyList<ActorRecord> QueryAll(SqliteConnection connection);

    ActorRecord Create(SqliteConnection connection, string projectId);

    ActorRecord Duplicate(SqliteConnection connection, string sourceId, string copyName);

    void Delete(SqliteConnection connection, string actorId);

    void Rename(SqliteConnection connection, string actorId, string name);
}

internal interface IThemeRepository
{
    ThemeRecord Get(string themeId);

    IReadOnlyList<ThemeRecord> QueryAll(SqliteConnection connection);

    void UpdateDirectField(string themeId, string fieldId, string value);

    void UpdateTokens(string themeId, string tokensJson);

    ThemeRecord Create(
        SqliteConnection connection,
        string projectId,
        string family,
        string iconThemeId,
        string statusBarId,
        string navigationBarId,
        string tokensJson,
        string metadataJson);

    ThemeRecord Duplicate(SqliteConnection connection, string sourceId, string copyName);

    void Delete(SqliteConnection connection, string themeId);

    void Rename(SqliteConnection connection, string themeId, string name);
}

internal interface IProductionFontRepository
{
    ProductionFontRecord Get(string fontId);

    ProductionFontRecord Get(SqliteConnection connection, string fontId);

    IReadOnlyList<ProductionFontRecord> QueryAll(SqliteConnection connection);

    void UpdateField(string fontId, string fieldId, string value);

    ProductionFontRecord UpsertImported(
        SqliteConnection connection,
        string projectId,
        string familyName,
        string category,
        string sourceDirectory,
        string filesJson);

    void Delete(SqliteConnection connection, string fontId);

    void Rename(SqliteConnection connection, string fontId, string name);
}

internal interface IIconThemeRepository
{
    IconThemeRecord Get(string iconThemeId);

    IconThemeRecord Get(SqliteConnection connection, string iconThemeId);

    IReadOnlyList<IconThemeRecord> QueryAll(SqliteConnection connection);

    IconThemeRecord UpsertDiscovered(
        SqliteConnection connection,
        string id,
        string projectId,
        string name,
        string assetRoot,
        string metadataJson);

    IconThemeRecord CreateDuplicate(
        SqliteConnection connection,
        string sourceId,
        string id,
        string name,
        string assetRoot,
        string metadataJson);

    void UpdateMapping(SqliteConnection connection, string iconThemeId, string mappingJson);

    void UpdateIdentity(
        SqliteConnection connection,
        string iconThemeId,
        string name,
        string assetRoot,
        string metadataJson);

    void Delete(SqliteConnection connection, string iconThemeId);
}

internal interface IAppModuleRepository
{
    AppDefinitionRecord GetApp(string appId);

    AppDefinitionRecord GetApp(SqliteConnection connection, string appId);

    ModuleDefinitionRecord GetModule(string moduleId);

    ModuleDefinitionRecord GetModule(SqliteConnection connection, string moduleId);

    AppDefinitionRecord GetModuleApp(string moduleId);

    IReadOnlyList<AppDefinitionRecord> QueryApps(SqliteConnection connection);

    IReadOnlyList<ModuleDefinitionRecord> QueryModules(SqliteConnection connection);

    void UpdateAppDirectField(SqliteConnection connection, string appId, string fieldId, string value);

    void UpdateAppConfig(SqliteConnection connection, string appId, string configJson);

    void UpdateAppMetadata(SqliteConnection connection, string appId, string metadataJson);

    void UpdateModuleSortOrder(SqliteConnection connection, string moduleId, int sortOrder);

    void UpdateModuleConfig(SqliteConnection connection, string moduleId, string configJson);

    void UpdateModuleDesignPreview(string moduleId, string designPreviewJson);

    void UpdateModuleMetadata(SqliteConnection connection, string moduleId, string metadataJson);

    void RenameApp(SqliteConnection connection, string appId, string name);

    void RenameModule(SqliteConnection connection, string moduleId, string name);

    void UpdateAppNode(SqliteConnection connection, string appId, string name, string notes);

    void UpdateModuleNode(SqliteConnection connection, string moduleId, string name, string notes);
}

internal interface IComponentClassRepository
{
    ComponentClassDefinitionRecord Get(string componentClassId);

    ComponentClassDefinitionRecord Get(SqliteConnection connection, string componentClassId);

    IReadOnlyList<ComponentClassDefinitionRecord> QueryAll(SqliteConnection connection);

    IReadOnlyList<ComponentClassDefinitionRecord> QueryByProject(SqliteConnection connection, string projectId);

    void UpdateDesignPreview(string componentClassId, string designPreviewJson);

    void UpdateConfigAndMetadata(
        SqliteConnection connection,
        string componentClassId,
        string configJson,
        string metadataJson);

    void UpdateMetadata(SqliteConnection connection, string componentClassId, string metadataJson);

    void Rename(SqliteConnection connection, string componentClassId, string name);

    void UpdateNode(SqliteConnection connection, string componentClassId, string name, string notes);
}

internal interface IModuleInstanceRepository
{
    ModuleInstanceRecord Get(string moduleInstanceId);

    ModuleInstanceRecord Get(SqliteConnection connection, string moduleInstanceId);

    IReadOnlyList<ModuleInstanceRecord> QueryAll(SqliteConnection connection);

    IReadOnlyList<ModuleInstanceRecord> QueryByShot(SqliteConnection connection, string shotId);

    int NextSortOrder(SqliteConnection connection, string shotId);

    string UniqueName(SqliteConnection connection, string shotId, string requestedName);

    void Insert(SqliteConnection connection, ModuleInstanceRecord record);

    ModuleInstanceRecord Duplicate(
        SqliteConnection connection,
        string sourceId,
        string id,
        string name,
        int sortOrder);

    void UpdateContent(SqliteConnection connection, string moduleInstanceId, string contentJson);

    void UpdateAnimation(SqliteConnection connection, string moduleInstanceId, string animationJson);

    void UpdateContentAndAnimation(
        SqliteConnection connection,
        string moduleInstanceId,
        string contentJson,
        string animationJson);

    void UpdateVariantDocuments(
        SqliteConnection connection,
        string moduleInstanceId,
        string metadataJson,
        string contentJson,
        string animationJson);

    void UpdateDuration(SqliteConnection connection, string moduleInstanceId, int durationFrames);

    void SwapSortOrder(
        SqliteConnection connection,
        string firstId,
        int firstSortOrder,
        string secondId,
        int secondSortOrder);

    long CountVariantReferences(SqliteConnection connection, string moduleId, string variantReference);

    void Rename(SqliteConnection connection, string moduleInstanceId, string name);

    void Delete(SqliteConnection connection, string moduleInstanceId);
}

internal interface IModuleInstanceThemeContextService
{
    string GetTokensJson(string moduleInstanceId);

    string GetTokensJson(SqliteConnection connection, string moduleInstanceId);

    void RequireShotContext(SqliteConnection connection, string shotId);

    void RequireEpisodeActor(SqliteConnection connection, string episodeId, string actorId);

    void RequireShotOwnerChange(SqliteConnection connection, string shotId, string actorId);

    void RequireActorThemeChange(SqliteConnection connection, string actorId, string themeId);
}

internal interface IReferenceUsageService
{
    IReadOnlyDictionary<ReferenceTarget, IReadOnlyList<ReferenceUsageRecord>> BuildIndex(SqliteConnection connection);

    IReadOnlyList<ReferenceUsageRecord> GetUsages(ProjectTreeNodeKind targetKind, string targetId);

    IReadOnlyList<ReferenceUsageRecord> GetUsages(
        SqliteConnection connection,
        ProjectTreeNodeKind targetKind,
        string targetId);
}
