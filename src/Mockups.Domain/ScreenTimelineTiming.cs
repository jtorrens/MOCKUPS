using System;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class ScreenTimelineTiming
{
    public static int TransitionFrameCount(
        string outgoingMotionJson,
        string incomingMotionJson,
        string outgoingThemeTokensJson,
        string incomingThemeTokensJson,
        int frameRate)
    {
        if (frameRate <= 0)
        {
            throw new InvalidOperationException(
                "Screen transition frame rate must be positive.");
        }

        var outgoingMilliseconds =
            MotionTimingDuration.ResolveMilliseconds(
                Parse(outgoingThemeTokensJson, "outgoing Screen Theme tokens"),
                Parse(outgoingMotionJson, "outgoing Screen Motion"),
                "outgoing Screen Motion");
        var incomingMilliseconds =
            MotionTimingDuration.ResolveMilliseconds(
                Parse(incomingThemeTokensJson, "incoming Screen Theme tokens"),
                Parse(incomingMotionJson, "incoming Screen Motion"),
                "incoming Screen Motion");
        return Math.Max(
            0,
            (int)Math.Ceiling(
                Math.Max(outgoingMilliseconds, incomingMilliseconds)
                / 1000.0
                * frameRate));
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
            + transitionFrameCount
            + actionDelayFrames);
    }

    private static JsonObject Parse(
        string json,
        string owner) =>
        JsonPath.ParseRequiredObject(
            json,
            owner);
}
