using Microsoft.Data.Sqlite;
using System;
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

    public EpisodeSettings GetEpisodeSettings(string episodeId)
    {
        return _projectEpisodeRepository.GetEpisodeSettings(episodeId);
    }

    public void UpdateEpisodeField(string episodeId, string fieldId, string value)
    {
        if (_shotManagerIntegrationRepository.GetEpisodeBinding(episodeId) is not null)
        {
            throw new InvalidOperationException(
                "Shot Manager governs this Episode. Change it there and synchronize.");
        }
        _projectEpisodeRepository.UpdateEpisodeField(episodeId, fieldId, value);
    }

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
