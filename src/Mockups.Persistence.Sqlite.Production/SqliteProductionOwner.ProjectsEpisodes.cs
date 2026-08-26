using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner
{
    public ProjectSettings GetProjectSettings(string projectId)
    {
        return _projectEpisodeRepository.GetProjectSettings(projectId);
    }

    public void UpdateProjectField(string projectId, string fieldId, string value)
    {
        _projectEpisodeRepository.UpdateProjectField(projectId, fieldId, value);
    }

    public void ConnectShotManagerProduction(
        string projectId,
        ShotManagerReadonlyProduction production,
        string workstreamName,
        string folderName) =>
        _projectEpisodeRepository.ConnectShotManagerProduction(
            projectId,
            production,
            workstreamName,
            folderName);

    public void SetShotManagerProductionEnabled(
        string projectId,
        bool enabled) =>
        _projectEpisodeRepository.SetShotManagerProductionEnabled(
            projectId,
            enabled);

    public void RefreshShotManagerProduction(
        string projectId,
        ShotManagerReadonlyProduction production) =>
        _projectEpisodeRepository.RefreshShotManagerProduction(
            projectId,
            production);

    public EpisodeSettings GetEpisodeSettings(string episodeId)
    {
        return _projectEpisodeRepository.GetEpisodeSettings(episodeId);
    }

    public void UpdateEpisodeField(string episodeId, string fieldId, string value)
    {
        _projectEpisodeRepository.UpdateEpisodeField(episodeId, fieldId, value);
    }

    public void AssociateShotManagerEpisode(
        string episodeId,
        ShotManagerReadonlyEpisode? episode) =>
        _projectEpisodeRepository.AssociateShotManagerEpisode(
            episodeId,
            episode);

    internal IReadOnlyList<ProjectRecord> QueryProjectRows(SqliteConnection connection)
    {
        return _projectEpisodeRepository.QueryProjects(connection);
    }

    internal IReadOnlyList<EpisodeRecord> QueryEpisodeRows(SqliteConnection connection)
    {
        return _projectEpisodeRepository.QueryEpisodes(connection);
    }

    internal ProjectSettings GetProjectSettings(SqliteConnection connection, string projectId)
    {
        return _projectEpisodeRepository.GetProjectSettings(connection, projectId);
    }
}
