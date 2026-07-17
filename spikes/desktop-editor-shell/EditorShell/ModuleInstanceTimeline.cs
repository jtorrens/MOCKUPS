using Mockups.DesktopEditorShell.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ModuleInstanceTimeline
{
    public static int DurationFrames(ModuleInstanceTimelineDataSource dataSource, string moduleInstanceId)
    {
        var source = dataSource.Load(moduleInstanceId);
        if (RuntimeDurationContract.Policy(source.EffectiveContractJson) == RuntimeDurationPolicy.Explicit)
            return System.Math.Max(1, source.PersistedDurationFrames);
        return RuntimeTimeline.DurationFrames(
            source.EffectiveContractJson,
            source.ContentJson,
            source.AnimationJson,
            source.PersistedDurationFrames,
            source.ThemeTokensJson);
    }

    public static int ShotDurationFrames(ModuleInstanceTimelineDataSource dataSource, string shotId) =>
        dataSource.ShotSlotIds(shotId).Sum((slotId) => DurationFrames(dataSource, slotId));

    public static int ScreenStartFrame(ModuleInstanceTimelineDataSource dataSource, string moduleInstanceId)
    {
        var source = dataSource.Load(moduleInstanceId);
        var start = 0;
        foreach (var slotId in dataSource.ShotSlotIds(source.ShotId))
        {
            if (slotId == moduleInstanceId) return start;
            start += DurationFrames(dataSource, slotId);
        }
        return 0;
    }

    public static IReadOnlyList<int> KeyframeFrames(ModuleInstanceTimelineDataSource dataSource, string moduleInstanceId)
    {
        var source = dataSource.Load(moduleInstanceId);
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
        var result = new List<int>();
        var screenStart = 0;
        foreach (var slotId in dataSource.ShotSlotIds(shotId))
        {
            result.AddRange(KeyframeFrames(dataSource, slotId).Select((frame) => screenStart + frame));
            screenStart += DurationFrames(dataSource, slotId);
        }
        return result.Distinct().Order().ToList();
    }

    private static JsonObject Parse(string json) =>
        JsonPath.ParseRequiredObject(json, "Module Instance timeline JSON");

}
