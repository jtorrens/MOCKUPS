using Microsoft.Data.Sqlite;
using System;
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
            record.CanvasJson,
            record.ReferenceVideoJson,
            record.MetadataJson,
            ShotManagerReadonlyContract.RequireShotAssociation(
                new ShotManagerShotAssociation(
                    record.ShotManagerAssociationState == "associated",
                    record.ShotManagerReferenceProductionId,
                    record.ShotManagerShotId,
                    record.ShotManagerCanonicalName),
                $"Shot '{shotId}' Shot Manager association"));
    }

    public ProductionOutputShotContext GetProductionOutputShotContext(
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
        var episodeSettings =
            _projectEpisodeRepository.GetEpisodeSettings(episode.Id);
        return new ProductionOutputShotContext(
            shot.ProjectId,
            shot.Id,
            shot.ShotNumber,
            episode.Slug,
            shot.Slug,
            projectSettings.ProductionOutput,
            projectSettings.ShotManagerOutput,
            episodeSettings.ShotManagerEpisode,
            ShotManagerReadonlyContract.RequireShotAssociation(
                new ShotManagerShotAssociation(
                    shot.ShotManagerAssociationState == "associated",
                    shot.ShotManagerReferenceProductionId,
                    shot.ShotManagerShotId,
                    shot.ShotManagerCanonicalName),
                $"Shot '{shotId}' Shot Manager association"));
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
        lock (WriteGate)
        {
            using var transaction = connection.BeginTransaction();
            var source = _shotRepository.Get(connection, sourceShotId);
            var sourceScreens = _moduleInstanceRepository.QueryByShot(
                connection,
                sourceShotId);
            var plan = ResolveNewShotPlan(
                connection,
                source.EpisodeId,
                shotNumber);
            var duplicate = _shotRepository.Duplicate(
                connection,
                sourceShotId,
                id,
                name,
                actorId,
                shotNumber,
                plan.ShotCode,
                transaction);
            foreach (var sourceScreen in sourceScreens)
            {
                _moduleInstanceRepository.Duplicate(
                    connection,
                    sourceScreen.Id,
                    $"module_instance_{Guid.NewGuid():N}",
                    duplicate.Id,
                    sourceScreen.Name,
                    sourceScreen.SortOrder,
                    transaction);
            }
            transaction.Commit();
            return duplicate;
        }
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

        if (fieldId == "shot.deviceOverrideId" && value == "inherited")
        {
            _shotRepository.ClearDeviceOverride(connection, shotId);
            return true;
        }

        var changesContext = fieldId is "shot.ownerActorId"
            or "shot.deviceOverrideId"
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

    internal void AssociateShotManagerShot(
        SqliteConnection connection,
        string shotId,
        ShotManagerReadonlyShot? shot) =>
        _shotRepository.AssociateShotManagerShot(
            connection,
            shotId,
            shot);
}
