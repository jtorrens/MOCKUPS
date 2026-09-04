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
            case "audio":
                return true;
            case "avatar":
                return true;
            case "badge":
                return true;
            case "bubble":
                return true;
            case "button":
                return true;
            case "callParticipant":
                CallParticipantComponentConfigContract.Validate(config, context);
                return true;
            case "codeIndicator":
                return true;
            case "collectionStack":
                return true;
            case "componentStack":
                return true;
            case "cursor":
                return true;
            case "drawPassword":
                return true;
            case "faceRecognition":
                return true;
            case "fingerprint":
                return true;
            case "gallery":
                return true;
            case "iconBar":
                return true;
            case "iconRow":
                return true;
            case "incomingCallNotification":
                IncomingCallNotificationComponentConfigContract.Validate(config, context);
                return true;
            case "keyboard":
                return true;
            case "keypad":
                return true;
            case "label":
                return true;
            case "list":
                ListComponentConfigContract.Validate(config, context);
                return true;
            case "listItem":
                ListItemComponentConfigContract.Validate(config, context);
                return true;
            case "media":
                return true;
            case "navigation_bar":
                NavigationBarComponentConfigContract.Validate(config, context);
                return true;
            case "notification":
                return true;
            case "notifications":
                return true;
            case "password":
                return true;
            case "status_bar":
                StatusBarComponentConfigContract.Validate(config, context);
                return true;
            case "surface":
                return true;
            case "textBox":
                return true;
            case "textInputBar":
                return true;
            default:
                return false;
        }
    }
}
