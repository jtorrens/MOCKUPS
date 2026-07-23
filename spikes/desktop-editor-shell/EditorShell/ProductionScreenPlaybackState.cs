using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ProductionScreenFrameRange(
    string ScreenId,
    int StartFrame,
    int DurationFrames);

internal static class ProductionScreenPlaybackState
{
    public static IReadOnlyList<ProductionScreenFrameRange> FrameRanges(
        ModuleInstanceTimelineDataSource dataSource,
        string shotId)
    {
        var result = new List<ProductionScreenFrameRange>();
        var startFrame = 0;
        foreach (var screenId in dataSource.ShotSlotIds(shotId))
        {
            var durationFrames = ModuleInstanceTimeline.DurationFrames(dataSource, screenId);
            result.Add(new ProductionScreenFrameRange(screenId, startFrame, durationFrames));
            startFrame += durationFrames;
        }
        return result;
    }

    public static int ActiveScreenIndex(
        IReadOnlyList<ProductionScreenFrameRange> ranges,
        int shotFrame)
    {
        for (var index = 0; index < ranges.Count; index++)
        {
            var range = ranges[index];
            if (shotFrame < range.StartFrame + range.DurationFrames)
            {
                return index;
            }
        }
        return ranges.Count - 1;
    }

    public static string ActiveScreenId(
        IReadOnlyList<ProductionScreenFrameRange> ranges,
        int shotFrame)
    {
        var index = ActiveScreenIndex(ranges, shotFrame);
        return index >= 0 ? ranges[index].ScreenId : "";
    }
}
