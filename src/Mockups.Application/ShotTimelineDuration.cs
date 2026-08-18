using System;

namespace Mockups.DesktopEditorShell.Data;

public enum ShotDurationPolicy
{
    Calculated,
    Explicit,
}

public static class ShotTimelineDuration
{
    public static ShotDurationPolicy ParsePolicy(string value) => value switch
    {
        "calculated" => ShotDurationPolicy.Calculated,
        "explicit" => ShotDurationPolicy.Explicit,
        _ => throw new InvalidOperationException(
            $"Shot duration policy must be 'calculated' or 'explicit', got '{value}'."),
    };

    public static string FormatPolicy(ShotDurationPolicy policy) => policy switch
    {
        ShotDurationPolicy.Calculated => "calculated",
        ShotDurationPolicy.Explicit => "explicit",
        _ => throw new InvalidOperationException($"Unknown Shot duration policy '{policy}'."),
    };

    public static int Resolve(
        ShotDurationPolicy policy,
        int calculatedDurationFrames,
        int explicitDurationFrames)
    {
        if (calculatedDurationFrames <= 0)
            throw new InvalidOperationException("Calculated Shot duration must be positive.");
        if (explicitDurationFrames <= 0)
            throw new InvalidOperationException("Explicit Shot duration must be positive.");
        return policy == ShotDurationPolicy.Explicit
            ? explicitDurationFrames
            : calculatedDurationFrames;
    }
}
