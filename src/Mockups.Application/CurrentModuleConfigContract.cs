using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class CurrentModuleConfigContract
{
    public static void Validate(string recordClassId, JsonObject config, string context)
    {
        if (!GeneratedModuleScaffoldConfigRegistry.TryValidate(
            recordClassId,
            config,
            context))
        {
            throw new InvalidOperationException(
                $"{context} has no current Module config contract for record class '{recordClassId}'.");
        }
    }
}
