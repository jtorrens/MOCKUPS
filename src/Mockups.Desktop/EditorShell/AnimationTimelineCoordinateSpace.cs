using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class AnimationTimelineCoordinateSpace
{
    public static int TimelineFrameForScreenFrame(
        bool usesOwnerTimeline,
        int screenFrame,
        Func<int, double> localFrameForScreenFrame)
    {
        return usesOwnerTimeline
            ? Round(localFrameForScreenFrame(screenFrame))
            : screenFrame;
    }

    public static int ScreenFrameForTimelineFrame(
        bool usesOwnerTimeline,
        double timelineFrame,
        Func<double, int> screenFrameForLocalFrame)
    {
        return usesOwnerTimeline
            ? screenFrameForLocalFrame(timelineFrame)
            : Round(timelineFrame);
    }

    public static int MarkerFrame(
        bool usesOwnerTimeline,
        int keyframeFrame,
        Func<double, int> screenFrameForLocalFrame)
    {
        return usesOwnerTimeline
            ? keyframeFrame
            : screenFrameForLocalFrame(keyframeFrame);
    }

    public static double LocalFrameForTimelineFrame(
        bool usesOwnerTimeline,
        int timelineFrame,
        Func<int, double> localFrameForScreenFrame)
    {
        return usesOwnerTimeline
            ? timelineFrame
            : localFrameForScreenFrame(timelineFrame);
    }

    private static int Round(double value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
