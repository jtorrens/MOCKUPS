using System;

namespace Mockups.DesktopEditorShell.Common;

internal static class PreviewPlaybackTiming
{
    public readonly record struct FramePosition(int Frame, bool Completed, long WrappedFrames);

    // Both endpoints belong to the scope. Loop never repeats just the suffix
    // after Play, and cannot change the authored duration or temporal origins.
    public static FramePosition ResolveFrame(long requestedFrame, int firstFrame, int lastFrame, bool loop)
    {
        if (lastFrame < firstFrame) throw new ArgumentOutOfRangeException(nameof(lastFrame));
        if (requestedFrame < firstFrame) throw new ArgumentOutOfRangeException(nameof(requestedFrame));
        if (!loop) return new((int)Math.Min(requestedFrame, lastFrame), requestedFrame >= lastFrame, 0);
        var length = (long)lastFrame - firstFrame + 1;
        var frame = (int)(firstFrame + (requestedFrame - firstFrame) % length);
        return new(frame, false, requestedFrame - frame);
    }

    public const int FrameRateMultiplier = 1;
    private const int MinimumFrameRate = 1;
    private const int MaximumFrameRate = 120;

    public static int PreviewFrameRate(int projectFrameRate)
    {
        return Math.Clamp(projectFrameRate * FrameRateMultiplier, MinimumFrameRate, MaximumFrameRate);
    }
}
