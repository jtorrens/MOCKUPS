using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ProductionPreviewScreenSnapshot(
    string ScreenId,
    string ScreenLabel,
    string ShotId,
    int StartFrame,
    int DurationFrames,
    int TransitionFrameCount,
    int ActionDelayFrames,
    int ActionDurationFrames,
    bool IsDurationEditable,
    string DeviceId,
    DevicePreviewMetrics DeviceMetrics,
    string TransitionJson,
    string VariantConfigJson,
    IReadOnlyList<int> ShotKeyframeFrames)
{
    public ProductionScreenFrameRange FrameRange =>
        new(ScreenId, StartFrame, DurationFrames);
}

internal sealed record ProductionPreviewShotSnapshot(
    string ShotId,
    int FrameRate,
    int DurationFrames,
    string DeviceId,
    DevicePreviewMetrics DeviceMetrics,
    ProductionShotContext Context,
    ShotReferenceVideoDocument ReferenceVideo,
    IReadOnlyList<ProductionPreviewScreenSnapshot> Screens)
{
    public IReadOnlyList<ProductionScreenFrameRange>
        FrameRanges =>
        Screens.Select(
            (screen) => screen.FrameRange).ToArray();

    public IReadOnlyList<int> KeyframeFrames =>
        Screens.SelectMany(
                (screen) => screen.ShotKeyframeFrames)
            .Distinct()
            .Order()
            .ToArray();
}

internal sealed record ProductionPreviewSessionSnapshot(
    IReadOnlyDictionary<string, ProductionPreviewShotSnapshot>
        ShotsById,
    IReadOnlyDictionary<string, ProductionPreviewScreenSnapshot>
        ScreensById)
{
    public ProductionPreviewShotSnapshot Shot(
        string shotId)
    {
        return ShotsById.TryGetValue(
            shotId,
            out var shot)
                ? shot
                : throw new InvalidOperationException(
                    $"Production Preview Shot '{shotId}' is not part of the prepared session.");
    }

    public ProductionPreviewScreenSnapshot Screen(
        string screenId)
    {
        return ScreensById.TryGetValue(
            screenId,
            out var screen)
                ? screen
                : throw new InvalidOperationException(
                    $"Production Preview Screen '{screenId}' is not part of the prepared session.");
    }
}

internal sealed class ProductionPreviewSessionDataSource
{
    private readonly IPreviewInputRepository _database;
    private readonly IModuleInstanceTimelineStore _timeline;
    private readonly ModuleInstanceTimelineDataSource
        _timelineDataSource;
    private readonly ProductionShotContextService
        _shotContexts;
    private readonly ActorPreviewDataSource _actors;

    public ProductionPreviewSessionDataSource(
        IPreviewInputRepository database,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes,
        IActorPreviewRepository actors)
    {
        _database = database;
        _timeline = timeline;
        _actors = new ActorPreviewDataSource(actors);
        _timelineDataSource =
            new ModuleInstanceTimelineDataSource(
                timeline,
                moduleInstanceThemes);
        _shotContexts =
            new ProductionShotContextService(
                new ProductionShotContextDataSource(
                    database,
                    actors));
    }

    public ProductionPreviewSessionSnapshot LoadSnapshot(
        IReadOnlyList<ProjectTreeNode> treeRoots)
    {
        var shots =
            new Dictionary<string, ProductionPreviewShotSnapshot>(
                StringComparer.Ordinal);
        var screens =
            new Dictionary<string, ProductionPreviewScreenSnapshot>(
                StringComparer.Ordinal);
        foreach (var shotNode in Descendants(treeRoots)
                     .Where((node) =>
                         node.Kind
                         == ProjectTreeNodeKind.Shot))
        {
            var shotSettings = _database.GetShotSettings(shotNode.Id);
            var actor = _actors.LoadContext(shotSettings.OwnerActorId);
            var shotDeviceId = shotSettings.EffectiveDeviceId(
                actor.DefaultDeviceId);
            if (string.IsNullOrWhiteSpace(shotDeviceId))
            {
                throw new InvalidOperationException(
                    $"Shot '{shotNode.Id}' has no effective Device.");
            }
            var shotDeviceMetrics = DeviceSettingsFieldContract.PreviewMetrics(
                _database.GetDeviceSettings(shotDeviceId));
            var shotScreens =
                new List<ProductionPreviewScreenSnapshot>();
            foreach (var range in
                     ModuleInstanceTimeline.ScreenRanges(
                         _timelineDataSource,
                         shotNode.Id))
            {
                var screenId =
                    range.ScreenId;
                var source =
                    _timelineDataSource.Load(screenId);
                var screenSettings = _timeline.GetModuleInstanceSettings(screenId);
                var deviceId = shotDeviceId;
                if (string.IsNullOrWhiteSpace(deviceId))
                    throw new InvalidOperationException($"Screen '{screenId}' has no effective Device.");
                var effectiveDevice = screenSettings.EffectiveDeviceSettings(
                    _database.GetDeviceSettings(deviceId));
                if (!source.ShotId.Equals(
                        shotNode.Id,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Production Preview Screen '{screenId}' belongs to Shot '{source.ShotId}', not '{shotNode.Id}'.");
                }

                var durationFrames =
                    range.EffectiveDurationFrames;
                var screen =
                    new ProductionPreviewScreenSnapshot(
                        screenId,
                        screenSettings.Name,
                        shotNode.Id,
                        range.StartFrame,
                        durationFrames,
                        range.TransitionFrameCount,
                        range.ActionDelayFrames,
                        range.ActionDurationFrames,
                        RuntimeDurationContract.ParsePolicy(
                            screenSettings.DurationPolicy)
                            == RuntimeDurationPolicy.Explicit,
                        deviceId,
                        DeviceSettingsFieldContract.PreviewMetrics(effectiveDevice),
                        source.TransitionJson,
                        _timeline
                            .GetModuleInstanceVariantSettings(
                                screenId)
                            .ConfigJson,
                        ModuleInstanceTimeline
                            .KeyframeFrames(source)
                            .Select((frame) =>
                                range.StartFrame
                                + range.ActionStartFrame
                                + frame)
                            .ToArray());
                shotScreens.Add(screen);
                if (!screens.TryAdd(
                        screenId,
                        screen))
                {
                    throw new InvalidOperationException(
                        $"Production Preview Screen '{screenId}' is declared more than once.");
                }
            }

            var shot =
                new ProductionPreviewShotSnapshot(
                    shotNode.Id,
                    shotSettings.Fps,
                    shotSettings.DurationFrames,
                    shotDeviceId,
                    shotDeviceMetrics,
                    _shotContexts.Resolve(
                        shotNode.Id),
                    shotSettings.ReferenceVideo,
                    shotScreens);
            if (!shots.TryAdd(
                    shotNode.Id,
                    shot))
            {
                throw new InvalidOperationException(
                    $"Production Preview Shot '{shotNode.Id}' is declared more than once.");
            }
        }

        return new ProductionPreviewSessionSnapshot(
            shots,
            screens);
    }

    private static IEnumerable<ProjectTreeNode> Descendants(
        IEnumerable<ProjectTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Descendants(
                         node.Children))
            {
                yield return child;
            }
        }
    }
}
