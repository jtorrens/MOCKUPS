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
        int maximumFrame,
        double laneWidth,
        IReadOnlyList<int> existingKeyframes)
    {
        var clamped = Math.Clamp(rawFrame, 0, Math.Max(0, maximumFrame));
        var captureFrames = Math.Max(0.5, ExistingKeyframeCapturePixels
            * Math.Max(1, maximumFrame) / Math.Max(1, laneWidth));
        var existing = existingKeyframes
            .Where((frame) => frame >= 0 && frame <= maximumFrame)
            .Distinct()
            .OrderBy((frame) => Math.Abs(frame - clamped))
            .FirstOrDefault();
        if (existingKeyframes.Any((frame) => frame >= 0 && frame <= maximumFrame)
            && Math.Abs(existing - clamped) <= captureFrames)
        {
            return existing;
        }

        var step = precise ? 1 : 5;
        return Math.Clamp(
            (int)Math.Round(clamped / step, MidpointRounding.AwayFromZero) * step,
            0,
            maximumFrame);
    }
}
