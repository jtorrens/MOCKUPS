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
