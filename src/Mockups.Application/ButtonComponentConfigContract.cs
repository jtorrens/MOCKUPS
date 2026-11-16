using Mockups.DesktopEditorShell.Common;
using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class ButtonComponentConfigContract
{
    public static void Validate(JsonObject config, string context)
    {
        var button = JsonPath.RequiredObject(config, "button", context);
        var position = JsonPath.RequiredString(button, "labelPosition", $"{context}.button");
        if (position is not ("top" or "bottom" or "left" or "right"))
            throw new InvalidOperationException($"{context}.button.labelPosition has unsupported value '{position}'.");
    }
}
