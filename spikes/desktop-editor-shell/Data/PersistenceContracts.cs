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

internal interface IModuleInstanceThemeContextService
{
    string GetTokensJson(string moduleInstanceId);

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
