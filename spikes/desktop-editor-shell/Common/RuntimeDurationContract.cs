using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal enum RuntimeDurationPolicy
{
    Calculated,
    Explicit,
}

internal static class RuntimeDurationContract
{
    public static RuntimeDurationPolicy Policy(string contractJson) =>
        Policy(Parse(contractJson));

    public static RuntimeDurationPolicy Policy(JsonObject contract)
    {
        var value = (contract["animationTimeline"] as JsonObject)?["durationPolicy"]?.GetValue<string>()
            ?? "calculated";
        return value switch
        {
            "calculated" => RuntimeDurationPolicy.Calculated,
            "explicit" => RuntimeDurationPolicy.Explicit,
            _ => throw new InvalidOperationException($"Unknown runtime duration policy '{value}'."),
        };
    }

    public static int InitialDurationFrames(string contractJson)
    {
        var contract = Parse(contractJson);
        if (Policy(contract) == RuntimeDurationPolicy.Calculated) return 1;
        var duration = (contract["animationTimeline"] as JsonObject)?["defaultDurationFrames"]?.GetValue<int>() ?? 0;
        if (duration <= 0)
            throw new InvalidOperationException("An explicit runtime duration requires a positive defaultDurationFrames value.");
        return duration;
    }

    private static JsonObject Parse(string json) =>
        JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();
}
