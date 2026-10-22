using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Data;

internal static class BubbleConfigTests
{
    public static void DirectionSizesAreRequired()
    {
        var config = JsonNode.Parse("""
            {"bubble":{"incomingTextSizeToken":"theme.typography.sizes.s",
            "outgoingTextSizeToken":"theme.typography.sizes.m",
            "systemTextSizeToken":"theme.typography.sizes.l"}}
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
    }
}
