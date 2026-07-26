// Generated from scaffolding/components/*.json. Do not edit manually.
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class GeneratedComponentScaffoldConfigRegistry
{
    public static bool TryValidate(
        string componentType,
        JsonObject config,
        string context)
    {
        switch (componentType)
        {
            case IncomingCallNotificationComponentConfigContract.ComponentType:
                IncomingCallNotificationComponentConfigContract.Validate(config, context);
                return true;
            case ListComponentConfigContract.ComponentType:
                ListComponentConfigContract.Validate(config, context);
                return true;
            case ListItemComponentConfigContract.ComponentType:
                ListItemComponentConfigContract.Validate(config, context);
                return true;
            default:
                return false;
        }
    }
}
