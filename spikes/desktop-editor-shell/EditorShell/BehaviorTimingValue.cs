using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record BehaviorTimingValue(string Mode, int FixedFrames, string PaceToken)
{
    public const string DefaultPaceToken = "theme.motion.naturalPace.normal";

    public static BehaviorTimingValue Parse(string json)
    {
        try
        {
            var value = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject;
            return new BehaviorTimingValue(
                value?["mode"]?.GetValue<string>() == "natural" ? "natural" : "fixed",
                Math.Max(0, value?["fixedFrames"]?.GetValue<int>() ?? 0),
                value?["paceToken"]?.GetValue<string>() is { Length: > 0 } token ? token : DefaultPaceToken);
        }
        catch
        {
            return new BehaviorTimingValue("fixed", 0, DefaultPaceToken);
        }
    }

    public string ToJson() => new JsonObject
    {
        ["mode"] = Mode,
        ["fixedFrames"] = Math.Max(0, FixedFrames),
        ["paceToken"] = PaceToken,
    }.ToJsonString();
}
