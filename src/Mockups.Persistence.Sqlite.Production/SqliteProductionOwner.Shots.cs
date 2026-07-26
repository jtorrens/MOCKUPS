using Microsoft.Data.Sqlite;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner
{
    public ShotSettings GetShotSettings(string shotId)
    {
        using var connection = OpenConnection();
        var record = _shotRepository.Get(connection, shotId);
        var project = _projectEpisodeRepository.GetProjectSettings(
            connection,
            record.ProjectId);

        return new ShotSettings(
            record.ProjectId,
            record.EpisodeId,
            record.Slug,
            record.ShotNumber,
            record.Version,
            record.SortOrder,
            project.DefaultFps,
            record.FpsOverride ?? project.DefaultFps,
            record.FpsOverride,
            record.DurationFrames,
            record.OwnerActorId,
            record.CanvasJson,
            record.MetadataJson);
    }

    public string GetShotRenderName(string shotId)
    {
        var governed = _shotManagerIntegrationRepository.GetShotStructure(
            shotId);
        if (governed is not null)
        {
            return governed.FullName;
        }

        using var connection = OpenConnection();
        var shot = _shotRepository.Get(connection, shotId);
        var episode = _projectEpisodeRepository.QueryEpisodes(connection)
            .SingleOrDefault(
                (candidate) => candidate.Id == shot.EpisodeId)
            ?? throw new InvalidOperationException(
                $"Missing episode '{shot.EpisodeId}'.");
        var project = _projectEpisodeRepository.QueryProjects(connection)
            .SingleOrDefault(
                (candidate) => candidate.Id == shot.ProjectId)
            ?? throw new InvalidOperationException(
                $"Missing project '{shot.ProjectId}'.");
        var projectSettings =
            _projectEpisodeRepository.GetProjectSettings(
                connection,
                project.Id);
        var projectSlug = SlugOrName(
            projectSettings.Slug,
            project.Name,
            "project");
        var episodeSlug = SlugOrName(
            episode.Slug,
            episode.Name,
            "episode");
        var shotSlug = SlugOrName(shot.Slug, shot.Name, "shot");
        return $"{projectSlug}_{episodeSlug}_{shotSlug}_v{Math.Max(0, shot.Version):00}";
    }

    internal bool UpdateShotField(
        SqliteConnection connection,
        string shotId,
        string fieldId,
        string value)
    {
        if (fieldId == "shot.fps" && value == "inherited")
        {
            _shotRepository.ClearFpsOverride(connection, shotId);
            return false;
        }

        var changesOwner = fieldId == "shot.ownerActorId";
        if (changesOwner)
        {
            _moduleInstanceThemeContextService.RequireShotOwnerChange(
                connection,
                shotId,
                value);
        }

        _shotRepository.UpdateField(
            connection,
            shotId,
            fieldId,
            value);
        return changesOwner;
    }
}
