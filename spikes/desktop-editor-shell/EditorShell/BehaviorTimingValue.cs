using Mockups.DesktopEditorShell.Common;
using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record BehaviorTimingValue(string Mode, int FixedFrames, string PaceToken)
{
    public const string DefaultPaceToken = "theme.motion.naturalPace.normal";

    public static BehaviorTimingValue Parse(string json)
    {
        var value = JsonPath.ParseRequiredObject(json, "Behavior Timing value");
        var mode = JsonPath.RequiredString(value, "mode", "Behavior Timing value");
        if (mode is not "fixed" and not "natural")
        {
            throw new InvalidOperationException($"Behavior Timing mode '{mode}' is not supported.");
        }
        var fixedFrames = JsonPath.RequiredInteger(value, "fixedFrames", "Behavior Timing value");
        if (fixedFrames < 0)
        {
            throw new InvalidOperationException("Behavior Timing fixedFrames must be non-negative.");
        }

        return new BehaviorTimingValue(
            mode,
            fixedFrames,
            JsonPath.RequiredString(value, "paceToken", "Behavior Timing value"));
    }

    public string ToJson() => new JsonObject
    {
        ["mode"] = Mode,
        ["fixedFrames"] = Math.Max(0, FixedFrames),
        ["paceToken"] = PaceToken,
    }.ToJsonString();
}
