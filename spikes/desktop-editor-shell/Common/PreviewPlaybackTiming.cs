using System;

namespace Mockups.DesktopEditorShell.Common;

internal static class PreviewPlaybackTiming
{
    public const int FrameRateMultiplier = 2;
    private const int MinimumFrameRate = 1;
    private const int MaximumFrameRate = 120;

    public static int PreviewFrameRate(int projectFrameRate)
    {
        return Math.Clamp(projectFrameRate * FrameRateMultiplier, MinimumFrameRate, MaximumFrameRate);
    }
}
