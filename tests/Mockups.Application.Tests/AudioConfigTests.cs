using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Data;

internal static class AudioConfigTests
{
    public static void BadgeConfigurationIsRequired()
    {
        var config = JsonNode.Parse("""
            {"audio":{"badgeSlot":{"showBadge":true,"contentMode":"icon",
            "iconToken":"media_mic","text":"1","size":16,
            "backgroundPaletteColor":"blue","contentPaletteColor":"gray_100"}}}
            """)!.AsObject();
        foreach (var visible in new[] { true, false })
        {
            config["audio"]!["badgeSlot"]!["showBadge"] = visible;
            CurrentComponentConfigContract.Validate("audio", config, "fixture");
            foreach (var key in config["audio"]!["badgeSlot"]!.AsObject().Select(entry => entry.Key).ToArray())
            {
                var candidate = config.DeepClone().AsObject();
                candidate["audio"]!["badgeSlot"]!.AsObject().Remove(key);
                Reject(candidate, key);
            }
            foreach (var (key, value) in new (string, JsonNode)[] {
                ("showBadge", JsonValue.Create("true")!), ("contentMode", JsonValue.Create("unknown")!),
                ("size", JsonValue.Create(0)!), ("size", JsonValue.Create(513)!),
                ("size", JsonValue.Create(1.5)!), ("iconToken", JsonValue.Create("")!) })
            {
                var candidate = config.DeepClone().AsObject();
                candidate["audio"]!["badgeSlot"]![key] = value;
                Reject(candidate, key);
            }
        }
    }

    private static void Reject(JsonObject config, string key)
    {
        try { CurrentComponentConfigContract.Validate("audio", config, "fixture"); }
        catch (InvalidOperationException error) when (error.Message.Contains(key, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException($"Audio accepted missing or invalid {key}.");
    }
}
