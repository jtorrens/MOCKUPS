using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class BubbleComponentConfigContract
{
    public static void Validate(JsonObject config, string context)
    {
        var bubble = JsonPath.RequiredObject(config, "bubble", context);
        foreach (var key in new[] { "incomingTextSizeToken", "outgoingTextSizeToken", "systemTextSizeToken" })
        {
            FieldOptionContract.ValidateValue(
                ComponentClassFieldCatalog.TypographySizeOptions,
                JsonPath.RequiredString(bubble, key, $"{context}.bubble"),
                $"{context}.bubble.{key}");
        }
    }
}
