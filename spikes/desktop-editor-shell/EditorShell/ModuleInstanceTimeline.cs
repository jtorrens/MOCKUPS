using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ModuleInstanceTimeline
{
    public static int DurationFrames(SpikeDatabase database, string moduleInstanceId)
    {
        var instance = database.GetModuleInstanceSettings(moduleInstanceId);
        return RuntimeTimeline.DurationFrames(
            database.GetModuleInstanceEffectiveContractJson(moduleInstanceId),
            instance.ContentJson,
            instance.AnimationJson,
            instance.DurationFrames,
            database.GetModuleInstanceThemeTokensJson(moduleInstanceId));
    }

    public static int ShotDurationFrames(SpikeDatabase database, string shotId) =>
        database.GetShotModuleInstanceSlots(shotId).Sum((slot) => DurationFrames(database, slot.Id));

    public static int ScreenStartFrame(SpikeDatabase database, string moduleInstanceId)
    {
        var instance = database.GetModuleInstanceSettings(moduleInstanceId);
        var start = 0;
        foreach (var slot in database.GetShotModuleInstanceSlots(instance.ShotId))
        {
            if (slot.Id == moduleInstanceId) return start;
            start += DurationFrames(database, slot.Id);
        }
        return 0;
    }

    public static IReadOnlyList<int> KeyframeFrames(SpikeDatabase database, string moduleInstanceId)
    {
        var instance = database.GetModuleInstanceSettings(moduleInstanceId);
        var contract = Parse(database.GetModuleInstanceEffectiveContractJson(moduleInstanceId));
        var runtime = Parse(database.GetModuleInstanceRuntimePreviewJson(moduleInstanceId));
        var animation = Parse(instance.AnimationJson);
        var themeTokens = Parse(database.GetModuleInstanceThemeTokensJson(moduleInstanceId));
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

    public static IReadOnlyList<int> ShotKeyframeFrames(SpikeDatabase database, string shotId)
    {
        var result = new List<int>();
        var screenStart = 0;
        foreach (var slot in database.GetShotModuleInstanceSlots(shotId))
        {
            result.AddRange(KeyframeFrames(database, slot.Id).Select((frame) => screenStart + frame));
            screenStart += DurationFrames(database, slot.Id);
        }
        return result.Distinct().Order().ToList();
    }

    private static JsonObject Parse(string json) =>
        JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();

}
