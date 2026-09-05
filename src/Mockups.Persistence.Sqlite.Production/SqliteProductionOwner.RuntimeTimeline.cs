using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner
{
    internal void SynchronizeTimelineDurations(
        SqliteConnection connection,
        string? shotId = null,
        SqliteTransaction? transaction = null)
    {
        var instances = shotId is null
            ? _moduleInstanceRepository.QueryAll(connection)
            : _moduleInstanceRepository.QueryByShot(connection, shotId);
        var shots = _shotRepository.QueryAll(connection);
        var updates = new List<(string Id, int Duration)>();
        foreach (var instance in instances)
        {
            var contract = ResolveModuleInstanceContract(
                instance.ModuleId,
                instance.MetadataJson);
            if (RuntimeDurationContract.RequireAllowedPolicy(
                    contract,
                    instance.DurationPolicy)
                == RuntimeDurationPolicy.Explicit)
            {
                continue;
            }

            var duration = RuntimeTimeline.DurationFrames(
                contract.ToJsonString(),
                instance.ContentJson,
                instance.AnimationJson,
                instance.DurationFrames,
                _moduleInstanceThemeContextService.GetTokensJson(
                    connection,
                    instance.Id),
                ResolveFrameRate(instance.ShotId));
            if (duration != instance.DurationFrames)
            {
                updates.Add((instance.Id, duration));
            }
        }

        foreach (var update in updates)
        {
            _moduleInstanceRepository.UpdateDuration(
                connection,
                update.Id,
                update.Duration,
                transaction);
        }

        var durationByShot =
            new Dictionary<string, int>(
                StringComparer.Ordinal);
        foreach (var group in
                 _moduleInstanceRepository
                     .QueryAll(connection)
                     .GroupBy(
                         (instance) =>
                             instance.ShotId,
                         StringComparer.Ordinal))
        {
            var shot =
                shots.Single((candidate) =>
                    candidate.Id == group.Key);
            var project =
                _projectEpisodeRepository
                    .GetProjectSettings(
                        connection,
                        shot.ProjectId);
            var frameRate =
                shot.FpsOverride
                ?? project.DefaultFps;
            var ordered =
                group.OrderBy((instance) =>
                        instance.SortOrder)
                    .ThenBy((instance) =>
                        instance.Name,
                        StringComparer.Ordinal)
                    .ThenBy((instance) =>
                        instance.Id,
                        StringComparer.Ordinal)
                    .ToList();
            var duration =
                0;
            for (var index = 0;
                 index < ordered.Count;
                 index++)
            {
                var current =
                    ordered[index];
                duration += EffectiveScreenDurationFrames(
                    connection,
                    current,
                    frameRate);
            }
            durationByShot.Add(
                group.Key,
                Math.Max(
                    1,
                    duration));
        }
        foreach (var shot in shots)
        {
            var calculatedDuration = durationByShot.GetValueOrDefault(shot.Id, 1);
            var duration = ShotTimelineDuration.Resolve(
                ShotTimelineDuration.ParsePolicy(shot.DurationPolicy),
                calculatedDuration,
                shot.ExplicitDurationFrames);
            if (duration == shot.DurationFrames)
            {
                continue;
            }

            _shotRepository.UpdateDuration(
                connection,
                shot.Id,
                duration,
                transaction);
        }

        int ResolveFrameRate(string instanceShotId)
        {
            var shot = shots.Single((candidate) => candidate.Id == instanceShotId);
            var project = _projectEpisodeRepository.GetProjectSettings(connection, shot.ProjectId);
            return shot.FpsOverride ?? project.DefaultFps;
        }
    }

    private int EffectiveScreenDurationFrames(
        SqliteConnection connection,
        ModuleInstanceRecord screen,
        int frameRate)
    {
        var theme = _moduleInstanceThemeContextService.GetTokensJson(
            connection,
            screen.Id);
        var transitionFrames = ScreenTimelineTiming.TransitionFrameCount(
            MotionVariantValue.NoneValue.ToJsonString(),
            screen.TransitionJson,
            theme,
            theme,
            frameRate);
        return ScreenTimelineTiming.EffectiveDurationFrames(
            screen.DurationFrames,
            transitionFrames,
            screen.ActionDelayFrames);
    }
}
