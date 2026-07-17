using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public ProjectSettings GetProjectSettings(string projectId)
    {
        return _projectEpisodeRepository.GetProjectSettings(projectId);
    }

    public void UpdateProjectField(string projectId, string fieldId, string value)
    {
        _projectEpisodeRepository.UpdateProjectField(projectId, fieldId, value);
    }

    public EpisodeSettings GetEpisodeSettings(string episodeId)
    {
        return _projectEpisodeRepository.GetEpisodeSettings(episodeId);
    }

    public void UpdateEpisodeField(string episodeId, string fieldId, string value)
    {
        _projectEpisodeRepository.UpdateEpisodeField(episodeId, fieldId, value);
    }

    private IReadOnlyList<ProjectRecord> QueryProjectRows(SqliteConnection connection)
    {
        return _projectEpisodeRepository.QueryProjects(connection);
    }

    private IReadOnlyList<EpisodeRecord> QueryEpisodeRows(SqliteConnection connection)
    {
        return _projectEpisodeRepository.QueryEpisodes(connection);
    }

    private ProjectSettings GetProjectSettings(SqliteConnection connection, string projectId)
    {
        return _projectEpisodeRepository.GetProjectSettings(connection, projectId);
    }
}
