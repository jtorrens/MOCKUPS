using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class RuntimeTimeline
{
    public static int DurationFrames(string contractJson, string runtimeJson, string animationJson, int storedFallback)
    {
        var contract = Parse(contractJson);
        var runtime = Parse(runtimeJson);
        var animation = Parse(animationJson);
        return RuntimeAnimationFrameOrigin.DurationFrames(contract, runtime, animation, storedFallback);
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();
}
