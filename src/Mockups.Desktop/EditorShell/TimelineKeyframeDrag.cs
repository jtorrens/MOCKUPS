using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class TimelineKeyframeDrag
{
    private const double ExistingKeyframeCapturePixels = 4;

    public static int ResolveScreenFrame(
        double rawFrame,
        bool precise,
        int minimumFrame,
        int maximumFrame,
        double laneWidth,
        IReadOnlyList<int> existingKeyframes)
    {
        var clamped = Math.Clamp(rawFrame, minimumFrame, maximumFrame);
        var captureFrames = Math.Max(0.5, ExistingKeyframeCapturePixels
            * Math.Max(1, maximumFrame - minimumFrame) / Math.Max(1, laneWidth));
        var existing = existingKeyframes
            .Where((frame) => frame >= minimumFrame && frame <= maximumFrame)
            .Distinct()
            .OrderBy((frame) => Math.Abs(frame - clamped))
            .FirstOrDefault();
        if (existingKeyframes.Any((frame) => frame >= minimumFrame && frame <= maximumFrame)
            && Math.Abs(existing - clamped) <= captureFrames)
        {
            return existing;
        }

        _ = precise;
        const int step = 1;
        return Math.Clamp(
            (int)Math.Round(clamped / step, MidpointRounding.AwayFromZero) * step,
            minimumFrame,
            maximumFrame);
    }
}
