using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Data;

internal static class BubbleConfigTests
{
    public static void DirectionSizesAreRequired()
    {
        var config = JsonNode.Parse("""
            {"bubble":{"incomingTextSizeToken":"theme.typography.sizes.s",
            "outgoingTextSizeToken":"theme.typography.sizes.m",
            "systemTextSizeToken":"theme.typography.sizes.l",
            "actorLabelSlot":{"showLabel":true,"showIncoming":true,"showOutgoing":false,"showSystem":false}}}
            """)!.AsObject();
        CurrentComponentConfigContract.Validate("bubble", config, "fixture");
        foreach (var key in new[] { "incomingTextSizeToken", "outgoingTextSizeToken", "systemTextSizeToken" })
        {
            foreach (var invalid in new JsonNode?[] { null, JsonValue.Create(""), JsonValue.Create("theme.iconSizes.m") })
            {
                var candidate = config.DeepClone().AsObject();
                candidate["bubble"]!.AsObject().Remove(key);
                if (invalid is not null) candidate["bubble"]![key] = invalid.DeepClone();
                try { CurrentComponentConfigContract.Validate("bubble", candidate, "fixture"); }
                catch (InvalidOperationException error) when (error.Message.Contains(key, StringComparison.Ordinal)) { continue; }
                throw new InvalidOperationException($"Bubble accepted invalid or missing {key}.");
            }
        }
        foreach (var key in new[] { "showLabel", "showIncoming", "showOutgoing", "showSystem" })
        {
            foreach (var invalid in new JsonNode?[] { null, JsonValue.Create("true"), JsonValue.Create(1) })
            {
                var candidate = config.DeepClone().AsObject();
                var label = candidate["bubble"]!["actorLabelSlot"]!.AsObject();
                label.Remove(key);
                if (invalid is not null) label[key] = invalid.DeepClone();
                try { CurrentComponentConfigContract.Validate("bubble", candidate, "fixture"); }
                catch (InvalidOperationException error) when (error.Message.Contains(key, StringComparison.Ordinal)) { continue; }
                throw new InvalidOperationException($"Bubble accepted invalid or missing {key}.");
            }
        }

    }
}
