using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner
{
    internal void SynchronizeTimelineDurations(
        SqliteConnection connection,
        string? shotId = null)
    {
        var instances = shotId is null
            ? _moduleInstanceRepository.QueryAll(connection)
            : _moduleInstanceRepository.QueryByShot(connection, shotId);
        var updates = new List<(string Id, int Duration)>();
        foreach (var instance in instances)
        {
            var contract = ResolveModuleInstanceContract(
                instance.ModuleId,
                instance.MetadataJson);
            if (RuntimeDurationContract.Policy(contract)
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
                    instance.Id));
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
                update.Duration);
        }

        var durationByShot = _moduleInstanceRepository
            .QueryAll(connection)
            .GroupBy((instance) => instance.ShotId, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => Math.Max(
                    1,
                    group.Sum((instance) => instance.DurationFrames)),
                StringComparer.Ordinal);
        foreach (var shot in _shotRepository.QueryAll(connection))
        {
            var duration = durationByShot.GetValueOrDefault(shot.Id, 1);
            if (duration == shot.DurationFrames)
            {
                continue;
            }

            _shotRepository.UpdateDuration(
                connection,
                shot.Id,
                duration);
        }
    }
}
