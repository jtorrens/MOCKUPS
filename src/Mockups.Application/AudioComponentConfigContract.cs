using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal static class AudioComponentConfigContract
{
    public static void Validate(JsonObject config, string context)
    {
        var audio = JsonPath.RequiredObject(config, "audio", context);
        var badge = JsonPath.RequiredObject(audio, "badgeSlot", $"{context}.audio");
        var owner = $"{context}.audio.badgeSlot";
        JsonPath.RequiredBoolean(badge, "showBadge", owner);
        var mode = JsonPath.RequiredString(badge, "contentMode", owner);
        FieldOptionContract.ValidateValue(
            ComponentClassFieldCatalog.Get("component.audio.badge.contentMode").Options!, mode, $"{owner}.contentMode");
        JsonPath.RequiredString(badge, "iconToken", owner, allowEmpty: mode != "icon");
        JsonPath.RequiredString(badge, "text", owner, allowEmpty: mode != "text");
        var size = JsonPath.RequiredInteger(badge, "size", owner);
        if (size is < 1 or > 512)
            throw new InvalidOperationException($"{owner}.size must be between 1 and 512.");
        JsonPath.RequiredString(badge, "backgroundPaletteColor", owner);
        JsonPath.RequiredString(badge, "contentPaletteColor", owner);
    }
}
