using Mockups.DesktopEditorShell.Common;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class SystemCompositionModuleConfigContract
{
    public const string RecordClassId = "module.system.composition";

    public static void Validate(JsonObject config, string context)
    {
        ModuleAppearanceModeContract.Read(config, context);
        var composition = JsonPath.RequiredObject(config, "systemComposition", context);
        var owner = $"{context}.systemComposition";
        RequireSlot(composition, "statusBarSlot", owner);
        RequireSlot(composition, "navigationBarSlot", owner);
        RequireSlot(composition, "stackSlot", owner);
        var stackInputs = JsonPath.RequiredObject(composition, "stackInputs", owner);
        JsonPath.RequiredArray(stackInputs, "items", $"{owner}.stackInputs");
    }

    private static void RequireSlot(JsonObject composition, string key, string owner)
    {
        var slot = JsonPath.RequiredObject(composition, key, owner);
        var slotOwner = $"{owner}.{key}";
        JsonPath.RequiredString(slot, "variantReference", slotOwner);
        JsonPath.RequiredObject(slot, "overrides", slotOwner);
    }
}
