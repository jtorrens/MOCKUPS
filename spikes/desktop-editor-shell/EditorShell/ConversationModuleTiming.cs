using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ConversationModuleTiming
{
    public static int ResolveDurationFrames(string contentJson, string behaviorJson, string animationJson)
    {
        var content = JsonNode.Parse(contentJson)?.AsObject()
            ?? throw new InvalidOperationException("Conversation content must be a JSON object.");
        var behavior = JsonNode.Parse(behaviorJson)?.AsObject()
            ?? throw new InvalidOperationException("Conversation behavior must be a JSON object.");
        var animation = JsonNode.Parse(animationJson)?.AsObject()
            ?? throw new InvalidOperationException("Conversation animation must be a JSON object.");

        var writeOnDurationFrames = NonNegativeInt(behavior["writeOnDurationFrames"]);
        var postWriteOnHoldFrames = NonNegativeInt(behavior["postWriteOnHoldFrames"]);
        var cursor = NonNegativeInt(behavior["headFrames"]);
        foreach (var message in content["messages"]?.AsArray() ?? [])
        {
            if (message is not JsonObject messageObject) continue;
            var direction = messageObject["direction"]?.GetValue<string>() ?? "incoming";
            var isSystem = string.Equals(direction, "system", StringComparison.Ordinal);
            var isOutgoing = string.Equals(direction, "outgoing", StringComparison.Ordinal);
            cursor += NonNegativeInt(messageObject["delayAfterPreviousFrames"]);
            cursor = Math.Max(
                cursor + (isSystem ? 0 : writeOnDurationFrames) + (isOutgoing ? postWriteOnHoldFrames : 0),
                LastAnimationEndFrame(animation, messageObject["id"]?.GetValue<string>() ?? ""));
        }

        return cursor + NonNegativeInt(behavior["tailFrames"]);
    }

    private static int LastAnimationEndFrame(JsonObject animation, string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId)) return 0;
        var endFrame = 0;
        foreach (var track in animation["tracks"]?.AsArray() ?? [])
        {
            if (track is not JsonObject trackObject
                || !string.Equals(trackObject["targetId"]?.GetValue<string>(), messageId, StringComparison.Ordinal)) continue;
            foreach (var eventNode in trackObject["events"]?.AsArray() ?? [])
            {
                if (eventNode is not JsonObject eventObject) continue;
                endFrame = Math.Max(endFrame,
                    NonNegativeInt(eventObject["startFrame"]) + NonNegativeInt(eventObject["durationFrames"]));
            }
        }
        return endFrame;
    }

    private static int NonNegativeInt(JsonNode? value)
    {
        return value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var result)
            ? Math.Max(0, result)
            : 0;
    }
}
