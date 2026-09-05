using System.Collections.Generic;
using System.Linq;

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
        return ModuleInstanceTimeline
            .ScreenRanges(
                dataSource,
                shotId)
            .Select((range) =>
                new ProductionScreenFrameRange(
                    range.ScreenId,
                    range.StartFrame,
                    range.EffectiveDurationFrames))
            .ToList();
    }

    public static int ActiveScreenIndex(
        IReadOnlyList<ProductionScreenFrameRange> ranges,
        int shotFrame)
    {
        for (var index = 0; index < ranges.Count; index++)
        {
            var range = ranges[index];
            if (shotFrame >= range.StartFrame
                && shotFrame < range.StartFrame + range.DurationFrames)
            {
                return index;
            }
        }
        return -1;
    }

    public static string ActiveScreenId(
        IReadOnlyList<ProductionScreenFrameRange> ranges,
        int shotFrame)
    {
        var index = ActiveScreenIndex(ranges, shotFrame);
        return index >= 0 ? ranges[index].ScreenId : "";
    }
}
