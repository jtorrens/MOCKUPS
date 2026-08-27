using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

public enum RuntimeDurationPolicy
{
    Calculated,
    Explicit,
}

public static class RuntimeDurationContract
{
    public static string FormatPolicy(RuntimeDurationPolicy policy) => policy switch
    {
        RuntimeDurationPolicy.Calculated => "calculated",
        RuntimeDurationPolicy.Explicit => "explicit",
        _ => throw new InvalidOperationException(
            $"Unknown runtime duration policy '{policy}'."),
    };

    public static RuntimeDurationPolicy ParsePolicy(string value) => value switch
    {
        "calculated" => RuntimeDurationPolicy.Calculated,
        "explicit" => RuntimeDurationPolicy.Explicit,
        _ => throw new InvalidOperationException(
            $"Unknown runtime duration policy '{value}'."),
    };

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
        return ParsePolicy(value);
    }

    public static IReadOnlyList<RuntimeDurationPolicy> AllowedPolicies(
        string contractJson) =>
        AllowedPolicies(Parse(contractJson));

    public static IReadOnlyList<RuntimeDurationPolicy> AllowedPolicies(
        JsonObject contract)
    {
        var defaultPolicy = Policy(contract);
        var timeline = JsonPath.OptionalObject(
            contract,
            "animationTimeline",
            "Runtime duration contract");
        if (timeline is null
            || !timeline.TryGetPropertyValue("durationPolicyOptions", out _))
        {
            return [defaultPolicy];
        }

        var policies = JsonPath.OptionalStringArray(
                timeline,
                "durationPolicyOptions",
                "Runtime duration contract animationTimeline")
            .Select(ParsePolicy)
            .ToList();
        if (policies.Count == 0
            || policies.Distinct().Count() != policies.Count
            || !policies.Contains(defaultPolicy))
        {
            throw new InvalidOperationException(
                "Runtime durationPolicyOptions must contain unique policies including durationPolicy.");
        }
        return policies;
    }

    public static RuntimeDurationPolicy RequireAllowedPolicy(
        JsonObject contract,
        string value)
    {
        var policy = ParsePolicy(value);
        if (!AllowedPolicies(contract).Contains(policy))
        {
            throw new InvalidOperationException(
                $"Runtime duration policy '{value}' is not allowed by this Module.");
        }
        return policy;
    }

    public static JsonObject ApplyPolicy(
        JsonObject contract,
        string value)
    {
        var policy = RequireAllowedPolicy(contract, value);
        var result = contract.DeepClone().AsObject();
        var timeline = result["animationTimeline"] as JsonObject;
        if (timeline is null)
        {
            timeline = new JsonObject();
            result["animationTimeline"] = timeline;
        }
        timeline["durationPolicy"] = FormatPolicy(policy);
        return result;
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
