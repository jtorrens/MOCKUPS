using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class AnimationTimelineCoordinateSpace
{
    public const double MarkerInset = 9;

    public static double PixelSpan(double width) => Math.Max(1, width - 2 * MarkerInset);

    public static double PixelForFrame(double frame, double width, int minimum, int maximum) =>
        MarkerInset + Math.Clamp((frame - minimum) / Math.Max(1, maximum - minimum), 0, 1) * PixelSpan(width);

    public static double FrameForPixel(double pixel, double width, int minimum, int maximum) =>
        minimum + Math.Clamp((pixel - MarkerInset) / PixelSpan(width), 0, 1) * Math.Max(0, maximum - minimum);

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
