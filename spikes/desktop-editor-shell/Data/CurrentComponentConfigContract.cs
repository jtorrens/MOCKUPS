using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class CurrentComponentConfigContract
{
    public static void Validate(string componentType, JsonObject config, string context)
    {
        switch (componentType)
        {
            case StatusBarComponentConfigContract.ComponentType:
                StatusBarComponentConfigContract.Validate(config, context);
                break;
            case NavigationBarComponentConfigContract.ComponentType:
                NavigationBarComponentConfigContract.Validate(config, context);
                break;
            case ListItemComponentConfigContract.ComponentType:
                ListItemComponentConfigContract.Validate(config, context);
                break;
            case ListComponentConfigContract.ComponentType:
                ListComponentConfigContract.Validate(config, context);
                break;
        }
    }
}
