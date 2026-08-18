using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

public static class RuntimeTimeline
{
    public static int DurationFrames(
        string contractJson,
        string runtimeJson,
        string animationJson,
        int storedFallback,
        string themeTokensJson = "{}",
        int frameRate = 0)
    {
        var contract = Parse(contractJson);
        var runtime = Parse(runtimeJson);
        var animation = Parse(animationJson);
        var themeTokens = Parse(themeTokensJson);
        return RuntimeAnimationFrameOrigin.DurationFrames(
            contract,
            runtime,
            animation,
            storedFallback,
            themeTokens,
            frameRate);
    }

    private static JsonObject Parse(string json) =>
        JsonPath.ParseRequiredObject(json, "Runtime timeline JSON");
}
