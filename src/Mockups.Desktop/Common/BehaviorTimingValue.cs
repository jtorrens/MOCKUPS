using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record BehaviorTimingValue(string Mode, int FixedFrames, string PaceToken)
{
    public const string DefaultPaceToken = "theme.motion.naturalPace.normal";

    public static BehaviorTimingValue Parse(
        string json,
        string context = "Behavior Timing value") =>
        Parse(JsonPath.ParseRequiredObject(json, context), context);

    public static BehaviorTimingValue Parse(
        JsonObject value,
        string context = "Behavior Timing value")
    {
        var mode = JsonPath.RequiredString(value, "mode", context);
        var fixedFrames = JsonPath.RequiredInteger(value, "fixedFrames", context);
        var paceToken = JsonPath.RequiredString(value, "paceToken", context);
        Validate(mode, fixedFrames, paceToken, context);
        return new BehaviorTimingValue(mode, fixedFrames, paceToken);
    }

    public string ToJson()
    {
        Validate(Mode, FixedFrames, PaceToken, "Behavior Timing value");
        return new JsonObject
        {
            ["mode"] = Mode,
            ["fixedFrames"] = FixedFrames,
            ["paceToken"] = PaceToken,
        }.ToJsonString();
    }

    private static void Validate(
        string mode,
        int fixedFrames,
        string paceToken,
        string context)
    {
        if (mode is not "fixed" and not "natural")
        {
            throw new InvalidOperationException(
                $"{context} mode '{mode}' is not supported.");
        }
        if (fixedFrames < 0)
        {
            throw new InvalidOperationException(
                $"{context} fixedFrames must be non-negative.");
        }
        if (!paceToken.StartsWith("theme.motion.naturalPace.", StringComparison.Ordinal)
            || !ThemeNumericTokenCatalog.TryGet(paceToken, out _))
        {
            throw new InvalidOperationException(
                $"{context} paceToken '{paceToken}' is not a declared natural pace token.");
        }
    }
}
