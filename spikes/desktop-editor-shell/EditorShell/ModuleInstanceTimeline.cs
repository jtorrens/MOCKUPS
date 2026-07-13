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
        var module = database.GetModuleSettings(instance.ModuleId);
        return RuntimeTimeline.DurationFrames(
            module.DesignPreviewJson,
            instance.ContentJson,
            instance.AnimationJson,
            instance.DurationFrames);
    }

    public static int ShotDurationFrames(SpikeDatabase database, string shotId) =>
        database.GetShotModuleInstanceSlots(shotId).Sum((slot) => DurationFrames(database, slot.Id));

    public static IReadOnlyList<int> KeyframeFrames(SpikeDatabase database, string moduleInstanceId)
    {
        var instance = database.GetModuleInstanceSettings(moduleInstanceId);
        var module = database.GetModuleSettings(instance.ModuleId);
        var contract = Parse(module.DesignPreviewJson);
        var runtime = Parse(database.GetModuleInstanceRuntimePreviewJson(moduleInstanceId));
        var animation = Parse(instance.AnimationJson);
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
                        System.Math.Max(0, keyframe["frame"]?.GetValue<int>() ?? 0)))
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
