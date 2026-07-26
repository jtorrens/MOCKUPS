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
        var timeline = JsonPath.OptionalObject(
            contract,
            "animationTimeline",
            "Runtime duration contract");
        var value = timeline is null || !timeline.TryGetPropertyValue("durationPolicy", out _)
            ? "calculated"
            : JsonPath.RequiredString(timeline, "durationPolicy", "Runtime duration contract animationTimeline");
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
        var timeline = JsonPath.RequiredObject(contract, "animationTimeline", "Runtime duration contract");
        var duration = JsonPath.RequiredInteger(
            timeline,
            "defaultDurationFrames",
            "Explicit runtime duration contract");
        if (duration <= 0)
            throw new InvalidOperationException("An explicit runtime duration requires a positive defaultDurationFrames value.");
        return duration;
    }

    private static JsonObject Parse(string json) =>
        JsonPath.ParseRequiredObject(json, "Runtime duration contract");
}
