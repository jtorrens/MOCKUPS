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
            ShotTimelineDuration.ParsePolicy(record.DurationPolicy),
            record.ExplicitDurationFrames,
            record.OwnerActorId,
            record.DeviceOverrideId,
            record.DeviceOverridesJson,
            record.ThemeOverrideId,
            record.CanvasJson,
            record.ReferenceVideoJson,
            record.MetadataJson);
    }

    public void UpdateShotDeviceOverrides(
        string shotId,
        string overridesJson)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            _shotRepository.UpdateDeviceOverrides(
                connection,
                shotId,
                overridesJson);
        }
    }

    public string GetShotRenderName(string shotId)
    {
        return GetProductionOutputShotPlan(shotId).TechnicalName;
    }

    public ProductionOutputShotPlan GetProductionOutputShotPlan(
        string shotId)
    {
        using var connection = OpenConnection();
        var shot = _shotRepository.Get(connection, shotId);
        var episode = _projectEpisodeRepository.QueryEpisodes(connection)
            .SingleOrDefault(
                (candidate) => candidate.Id == shot.EpisodeId)
            ?? throw new InvalidOperationException(
                $"Missing episode '{shot.EpisodeId}'.");
        var projectSettings =
            _projectEpisodeRepository.GetProjectSettings(
                connection,
                shot.ProjectId);
        return ProductionOutputContract.Resolve(
            shot.ProjectId,
            shot.Id,
            shot.ShotNumber,
            episode.Slug,
            shot.Slug,
            projectSettings.ProductionOutput);
    }

    internal ShotRecord CreateShot(
        SqliteConnection connection,
        string episodeId,
        string actorId,
        int shotNumber)
    {
        var plan = ResolveNewShotPlan(
            connection,
            episodeId,
            shotNumber);
        return _shotRepository.Create(
            connection,
            episodeId,
            actorId,
            shotNumber,
            plan.ShotCode);
    }

    internal ShotRecord DuplicateShot(
        SqliteConnection connection,
        string sourceShotId,
        string id,
        string name,
        string actorId,
        int shotNumber)
    {
        var source = _shotRepository.Get(connection, sourceShotId);
        var plan = ResolveNewShotPlan(
            connection,
            source.EpisodeId,
            shotNumber);
        return _shotRepository.Duplicate(
            connection,
            sourceShotId,
            id,
            name,
            actorId,
            shotNumber,
            plan.ShotCode);
    }

    private ProductionOutputShotPlan ResolveNewShotPlan(
        SqliteConnection connection,
        string episodeId,
        int shotNumber)
    {
        var episode = _projectEpisodeRepository.QueryEpisodes(connection)
            .SingleOrDefault((candidate) =>
                candidate.Id == episodeId)
            ?? throw new InvalidOperationException(
                $"Missing episode '{episodeId}'.");
        var project = _projectEpisodeRepository.GetProjectSettings(
            connection,
            episode.ProjectId);
        return ProductionOutputContract.Resolve(
            episode.ProjectId,
            "new-shot",
            shotNumber,
            episode.Slug,
            ProductionOutputContract.CreateShotCode(
                project.ProductionOutput.ShotPrefix,
                shotNumber,
                project.ProductionOutput.ShotNumberPadding),
            project.ProductionOutput);
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

        if (fieldId is "shot.deviceOverrideId" or "shot.themeOverrideId"
            && value == "inherited")
        {
            _shotRepository.ClearResourceOverride(
                connection,
                shotId,
                fieldId);
            return true;
        }

        var changesContext = fieldId is "shot.ownerActorId"
            or "shot.deviceOverrideId"
            or "shot.themeOverrideId"
            or "shot.durationPolicy"
            or "shot.durationFrames";
        if (fieldId == "shot.ownerActorId")
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
        return changesContext;
    }
}
