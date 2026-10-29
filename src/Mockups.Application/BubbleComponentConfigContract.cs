using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class BubbleComponentConfigContract
{
    public static void Validate(JsonObject config, string context)
    {
        var bubble = JsonPath.RequiredObject(config, "bubble", context);
        var actorLabel = JsonPath.RequiredObject(bubble, "actorLabelSlot", $"{context}.bubble");
        foreach (var key in new[] { "showLabel", "showIncoming", "showOutgoing", "showSystem" })
            JsonPath.RequiredBoolean(actorLabel, key, $"{context}.bubble.actorLabelSlot");
        foreach (var key in new[] { "incomingTextSizeToken", "outgoingTextSizeToken", "systemTextSizeToken" })
        {
            FieldOptionContract.ValidateValue(
                ComponentClassFieldCatalog.TypographySizeOptions,
                JsonPath.RequiredString(bubble, key, $"{context}.bubble"),
                $"{context}.bubble.{key}");
        }
    }
}
