using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.EditorShell;

internal static class BehaviorTimingIdentityTests
{
    public static void ExactOwnerIdentities()
    {
        foreach (var name in new[] { "password", "caption" })
        {
            var local = new JsonObject { ["id"] = name, ["valueKind"] = "StringSingleLine" };
            var forwarded = new JsonObject
            {
                ["id"] = $"forwarded.{name}", ["sourceInputId"] = name,
                ["valueKind"] = "StringSingleLine",
            };
            var timing = new JsonObject
            {
                ["id"] = "timing", ["valueKind"] = "BehaviorTiming",
                ["naturalTiming"] = new JsonObject
                {
                    ["sourceFieldId"] = name, ["unit"] = "grapheme", ["baseFramesPerUnit"] = 2,
                },
            };
            RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions([local, forwarded, timing], "Runtime");
            RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions([forwarded, timing], "Forwarding", "sourceInputId");
            try
            {
                RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions([forwarded, timing], "Runtime");
                throw new Exception("A source identity must not substitute a missing Runtime field ID.");
            }
            catch (InvalidOperationException error) when (error.Message.Contains("one exact sibling field")) { }
        }
    }
}
