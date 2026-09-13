using System;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class ScreenTimelineTiming
{
    public static int EffectiveTransitionDurationFrames(
        string motionJson,
        int configuredDurationFrames)
    {
        if (configuredDurationFrames <= 0)
        {
            throw new InvalidOperationException(
                "Shot transition duration must be positive.");
        }
        var motion = MotionVariantValue.Parse(motionJson);
        return motion.Transition == MotionVariantValue.None
            && !motion.Fade
            && !motion.Translate
            && !motion.Scale
                ? 0
                : configuredDurationFrames;
    }

    public static int EffectiveDurationFrames(
        int actionDurationFrames,
        int transitionFrameCount,
        int actionDelayFrames)
    {
        if (actionDurationFrames <= 0)
        {
            throw new InvalidOperationException(
                "Screen action duration must be positive.");
        }
        if (transitionFrameCount < 0
            || actionDelayFrames < 0)
        {
            throw new InvalidOperationException(
                "Screen transition and action delay must be non-negative.");
        }
        return checked(
            actionDurationFrames
            + transitionFrameCount * 2
            + actionDelayFrames);
    }
}
