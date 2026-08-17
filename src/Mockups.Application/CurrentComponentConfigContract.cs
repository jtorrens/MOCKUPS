using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class CurrentComponentConfigContract
{
    public static void Validate(string componentType, JsonObject config, string context)
    {
        if (!GeneratedComponentScaffoldConfigRegistry.TryValidate(
            componentType,
            config,
            context))
        {
            throw new InvalidOperationException(
                $"{context} has no registered current Component config contract for '{componentType}'.");
        }
    }
}
