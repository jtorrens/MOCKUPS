using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class AnimationTimelineCoordinateSpace
{
    public static int TimelineFrameForScreenFrame(
        bool usesOwnerTimeline,
        int screenFrame,
        Func<int, double> ownerFrameForScreenFrame)
    {
        return usesOwnerTimeline
            ? Round(ownerFrameForScreenFrame(screenFrame))
            : screenFrame;
    }

    public static int ScreenFrameForTimelineFrame(
        bool usesOwnerTimeline,
        double timelineFrame,
        Func<double, int> screenFrameForOwnerFrame)
    {
        return usesOwnerTimeline
            ? screenFrameForOwnerFrame(timelineFrame)
            : Round(timelineFrame);
    }

    public static int MarkerFrame(
        bool usesOwnerTimeline,
        double fieldOwnerFrameOrigin,
        int keyframeFrame,
        Func<double, int> screenFrameForOwnerFrame)
    {
        var ownerFrame = fieldOwnerFrameOrigin + keyframeFrame;
        return usesOwnerTimeline
            ? Round(ownerFrame)
            : screenFrameForOwnerFrame(ownerFrame);
    }

    public static double OwnerFrameForTimelineFrame(
        bool usesOwnerTimeline,
        int timelineFrame,
        Func<int, double> ownerFrameForScreenFrame)
    {
        return usesOwnerTimeline
            ? timelineFrame
            : ownerFrameForScreenFrame(timelineFrame);
    }

    private static int Round(double value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
