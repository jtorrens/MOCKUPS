using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ProductionPreviewScreenSnapshot(
    string ScreenId,
    string ShotId,
    int StartFrame,
    int DurationFrames,
    string VariantConfigJson,
    IReadOnlyList<int> ShotKeyframeFrames)
{
    public ProductionScreenFrameRange FrameRange =>
        new(ScreenId, StartFrame, DurationFrames);
}

internal sealed record ProductionPreviewShotSnapshot(
    string ShotId,
    int FrameRate,
    IReadOnlyList<ProductionPreviewScreenSnapshot> Screens)
{
    public int DurationFrames =>
        Math.Max(
            1,
            Screens.Sum(
                (screen) => screen.DurationFrames));

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

    public ProductionPreviewSessionDataSource(
        IPreviewInputRepository database,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes)
    {
        _database = database;
        _timeline = timeline;
        _timelineDataSource =
            new ModuleInstanceTimelineDataSource(
                timeline,
                moduleInstanceThemes);
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
            var shotScreens =
                new List<ProductionPreviewScreenSnapshot>();
            var startFrame = 0;
            foreach (var screenId in
                     _timelineDataSource.ShotSlotIds(
                         shotNode.Id))
            {
                var source =
                    _timelineDataSource.Load(screenId);
                if (!source.ShotId.Equals(
                        shotNode.Id,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Production Preview Screen '{screenId}' belongs to Shot '{source.ShotId}', not '{shotNode.Id}'.");
                }

                var durationFrames =
                    Math.Max(
                        1,
                        ModuleInstanceTimeline
                            .DurationFrames(source));
                var screen =
                    new ProductionPreviewScreenSnapshot(
                        screenId,
                        shotNode.Id,
                        startFrame,
                        durationFrames,
                        _timeline
                            .GetModuleInstanceVariantSettings(
                                screenId)
                            .ConfigJson,
                        ModuleInstanceTimeline
                            .KeyframeFrames(source)
                            .Select((frame) =>
                                startFrame + frame)
                            .ToArray());
                shotScreens.Add(screen);
                if (!screens.TryAdd(
                        screenId,
                        screen))
                {
                    throw new InvalidOperationException(
                        $"Production Preview Screen '{screenId}' is declared more than once.");
                }
                startFrame += durationFrames;
            }

            var shot =
                new ProductionPreviewShotSnapshot(
                    shotNode.Id,
                    _database.GetShotSettings(
                        shotNode.Id).Fps,
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
