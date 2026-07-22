using Mockups.DesktopEditorShell.Common;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class LockScreenModuleConfigContract
{
    public const string RecordClassId = "module.core.lockScreen";

    public static void Validate(JsonObject config, string context)
    {
        ModuleAppearanceModeContract.Read(config, context);
        var lockScreen = JsonPath.RequiredObject(config, "lockScreen", context);
        var owner = $"{context}.lockScreen";
        RequireSlot(lockScreen, "statusBarSlot", owner);
        RequireSlot(lockScreen, "navigationBarSlot", owner);
        RequireSlot(lockScreen, "stackSlot", owner);
        var stackInputs = JsonPath.RequiredObject(lockScreen, "stackInputs", owner);
        JsonPath.RequiredArray(stackInputs, "items", $"{owner}.stackInputs");
    }

    private static void RequireSlot(JsonObject lockScreen, string key, string owner)
    {
        var slot = JsonPath.RequiredObject(lockScreen, key, owner);
        var slotOwner = $"{owner}.{key}";
        JsonPath.RequiredString(slot, "variantReference", slotOwner);
        JsonPath.RequiredObject(slot, "overrides", slotOwner);
    }
}
