using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class CurrentModuleConfigContract
{
    public static void Validate(string recordClassId, JsonObject config, string context)
    {
        switch (recordClassId)
        {
            case ConversationModuleConfigContract.RecordClassId:
                ConversationModuleConfigContract.Validate(config, context);
                break;
            case LockScreenModuleConfigContract.RecordClassId:
                LockScreenModuleConfigContract.Validate(config, context);
                break;
            default:
                if (GeneratedModuleScaffoldConfigRegistry.TryValidate(
                        recordClassId,
                        config,
                        context))
                {
                    break;
                }
                throw new InvalidOperationException(
                    $"{context} has no current Module config contract for record class '{recordClassId}'.");
        }
    }
}
