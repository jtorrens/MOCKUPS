using System;
using System.Collections.Generic;
using System.Linq;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class ScreenTimelineTiming
{
    public static ScreenTimelineFrame ResolveFrame(
        int shotFrame,
        int shotDurationFrames,
        int screenStartFrame,
        int actionDurationFrames,
        int transitionFrameCount,
        int actionDelayFrames,
        bool clampToScreen)
    {
        var extent = EffectiveDurationFrames(actionDurationFrames, transitionFrameCount, actionDelayFrames);
        var screenFrame = shotFrame - screenStartFrame;
        if (clampToScreen) screenFrame = Math.Clamp(screenFrame, 0, extent - 1);
        var actionStart = transitionFrameCount + actionDelayFrames;
        var actionEnd = actionStart + actionDurationFrames;
        var phase = shotFrame < 0 || shotFrame >= shotDurationFrames
            ? "content"
            : screenFrame < transitionFrameCount ? "enter"
            : screenFrame >= actionEnd ? "exit" : "content";
        return new ScreenTimelineFrame(
            screenFrame,
            Math.Clamp(screenFrame - actionStart, 0, actionDurationFrames - 1),
            phase,
            phase == "enter" ? screenFrame : phase == "exit" ? screenFrame - actionEnd : 0);
    }

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

    public static int CalculatedShotDurationFrames(
        IEnumerable<(
            int StartFrame,
            int ActionDurationFrames,
            int ActionDelayFrames)> screens,
        int transitionFrameCount)
    {
        if (transitionFrameCount < 0)
        {
            throw new InvalidOperationException(
                "Shot transition duration must be non-negative.");
        }
        var ordered = screens.ToArray();
        if (ordered.Any((screen) =>
                screen.ActionDurationFrames <= 0
                || screen.ActionDelayFrames < 0))
        {
            throw new InvalidOperationException(
                "Shot Screen action duration must be positive and delay non-negative.");
        }
        var sequentialDuration = checked(
            ordered.Sum((screen) =>
                screen.ActionDurationFrames
                + screen.ActionDelayFrames)
            + Math.Max(0, ordered.Length - 1)
            * transitionFrameCount);
        var latestAuthoredEnd = ordered
            .Select((screen) => checked(
                screen.StartFrame
                + EffectiveDurationFrames(
                    screen.ActionDurationFrames,
                    transitionFrameCount,
                    screen.ActionDelayFrames)))
            .DefaultIfEmpty(1)
            .Max();
        return Math.Max(
            1,
            Math.Max(
                sequentialDuration,
                latestAuthoredEnd));
    }
}

public sealed record ScreenTimelineFrame(int ScreenFrame, int ActionFrame, string Phase, int PhaseElapsedFrames);
