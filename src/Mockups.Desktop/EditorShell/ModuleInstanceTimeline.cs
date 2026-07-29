using Mockups.DesktopEditorShell.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ScreenTimelineRange(
    string ScreenId,
    int StartFrame,
    int TransitionFrameCount,
    int ActionDelayFrames,
    int ActionDurationFrames)
{
    public int ActionStartFrame =>
        TransitionFrameCount
        + ActionDelayFrames;

    public int EffectiveDurationFrames =>
        ScreenTimelineTiming.EffectiveDurationFrames(
            ActionDurationFrames,
            TransitionFrameCount,
            ActionDelayFrames);
}

internal static class ModuleInstanceTimeline
{
    public static int DurationFrames(ModuleInstanceTimelineDataSource dataSource, string moduleInstanceId)
    {
        return DurationFrames(
            dataSource.Load(moduleInstanceId));
    }

    public static int DurationFrames(
        ModuleInstanceTimelineSource source)
    {
        if (RuntimeDurationContract.Policy(source.EffectiveContractJson) == RuntimeDurationPolicy.Explicit)
            return System.Math.Max(1, source.PersistedDurationFrames);
        return RuntimeTimeline.DurationFrames(
            source.EffectiveContractJson,
            source.ContentJson,
            source.AnimationJson,
            source.PersistedDurationFrames,
            source.ThemeTokensJson);
    }

    public static int ShotDurationFrames(
        ModuleInstanceTimelineDataSource dataSource,
        string shotId) =>
        ScreenRanges(dataSource, shotId)
            .Sum((range) =>
                range.EffectiveDurationFrames);

    public static IReadOnlyList<ScreenTimelineRange> ScreenRanges(
        ModuleInstanceTimelineDataSource dataSource,
        string shotId)
    {
        var ids =
            dataSource.ShotSlotIds(shotId);
        var sources =
            ids.Select(dataSource.Load)
                .ToList();
        var ranges =
            new List<ScreenTimelineRange>(
                sources.Count);
        var startFrame = 0;
        for (var index = 0;
             index < sources.Count;
             index++)
        {
            var source =
                sources[index];
            var transitionFrames =
                index == 0
                    ? 0
                    : ScreenTimelineTiming
                        .TransitionFrameCount(
                            sources[index - 1]
                                .TransitionJson,
                            source.TransitionJson,
                            sources[index - 1]
                                .ThemeTokensJson,
                            source.ThemeTokensJson,
                            source.FrameRate);
            var range =
                new ScreenTimelineRange(
                    ids[index],
                    startFrame,
                    transitionFrames,
                    source.ActionDelayFrames,
                    DurationFrames(source));
            ranges.Add(range);
            startFrame +=
                range.EffectiveDurationFrames;
        }
        return ranges;
    }

    public static ScreenTimelineRange ScreenRange(
        ModuleInstanceTimelineDataSource dataSource,
        string moduleInstanceId)
    {
        var source =
            dataSource.Load(
                moduleInstanceId);
        return ScreenRanges(
                dataSource,
                source.ShotId)
            .Single((range) =>
                range.ScreenId == moduleInstanceId);
    }

    public static int ScreenStartFrame(ModuleInstanceTimelineDataSource dataSource, string moduleInstanceId)
        => ScreenRange(
            dataSource,
            moduleInstanceId)
            .StartFrame;

    public static IReadOnlyList<int> KeyframeFrames(ModuleInstanceTimelineDataSource dataSource, string moduleInstanceId)
    {
        return KeyframeFrames(
            dataSource.Load(moduleInstanceId));
    }

    public static IReadOnlyList<int> KeyframeFrames(
        ModuleInstanceTimelineSource source)
    {
        var contract = Parse(source.EffectiveContractJson);
        var runtime = Parse(source.RuntimePreviewJson);
        var animation = Parse(source.AnimationJson);
        var themeTokens = Parse(source.ThemeTokensJson);
        return (animation["tracks"] as JsonArray)?.OfType<JsonObject>()
            .SelectMany((track) =>
            {
                var fieldId = track["fieldId"]?.GetValue<string>() ?? "";
                var targetId = track["targetId"]?.GetValue<string>() ?? "";
                return (track["keyframes"] as JsonArray)?.OfType<JsonObject>()
                    .Where((keyframe) => keyframe["enabled"]?.GetValue<bool>() != false)
                    .Select((keyframe) => RuntimeAnimationFrameOrigin.ScreenFrame(
                        contract,
                        runtime,
                        animation,
                        fieldId,
                        targetId,
                        System.Math.Max(0, keyframe["frame"]?.GetValue<int>() ?? 0),
                        themeTokens))
                    ?? [];
            })
            .Distinct()
            .Order()
            .ToList() ?? [];
    }

    public static IReadOnlyList<int> ShotKeyframeFrames(ModuleInstanceTimelineDataSource dataSource, string shotId)
    {
        var result =
            new List<int>();
        foreach (var range in
                 ScreenRanges(
                     dataSource,
                     shotId))
        {
            result.AddRange(
                KeyframeFrames(
                        dataSource,
                        range.ScreenId)
                    .Select((frame) =>
                        range.StartFrame
                        + range.ActionStartFrame
                        + frame));
        }
        return result.Distinct().Order().ToList();
    }

    private static JsonObject Parse(string json) =>
        JsonPath.ParseRequiredObject(json, "Module Instance timeline JSON");

}
