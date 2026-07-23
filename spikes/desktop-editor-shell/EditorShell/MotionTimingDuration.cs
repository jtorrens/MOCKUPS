using System;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class MotionTimingDuration
{
    public static double ResolveMilliseconds(
        JsonObject themeTokens,
        JsonObject motion,
        string context)
    {
        var value = MotionVariantValue.Parse(motion.ToJsonString());
        if (value.Transition == MotionVariantValue.None && !value.Fade) return 0;
        var timingKey = value.Transition == MotionVariantValue.None
            ? "fade"
            : value.Transition;
        return ThemeNumericTokenValue.RequireNonNegative(
                   themeTokens,
                   $"theme.motion.{timingKey}.delayMs",
                   context)
               + ThemeNumericTokenValue.RequireNonNegative(
                   themeTokens,
                   $"theme.motion.{timingKey}.durationMs",
                   context);
    }

    public static double RequirePositiveMilliseconds(
        JsonObject themeTokens,
        JsonObject motion,
        string context)
    {
        var duration = ResolveMilliseconds(themeTokens, motion, context);
        if (duration <= 0)
        {
            throw new InvalidOperationException(
                $"{context} must resolve to a positive finite Motion duration.");
        }
        return duration;
    }
}
