using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class ConversationModuleConfigContract
{
    public const string RecordClassId = "module.core.chat";

    public static void Validate(JsonObject config, string context)
    {
        ModuleAppearanceModeContract.Read(config, context);
        var conversation = JsonPath.RequiredObject(config, "conversation", context);
        var owner = $"{context}.conversation";

        JsonPath.RequiredBoolean(conversation, "showHeader", owner);
        JsonPath.RequiredBoolean(conversation, "useAppWallpaper", owner);
        RequireNonNegative(JsonPath.RequiredNumber(conversation, "headerHeight", owner), $"{owner}.headerHeight");
        RequireSlot(conversation, "headerSurfaceSlot", owner);
        JsonPath.RequiredBoolean(conversation, "headerUseActorColor", owner);
        RequireSlot(conversation, "headerAvatarSlot", owner);
        RequireOneOf(
            JsonPath.RequiredString(conversation, "headerAvatarAlignment", owner),
            ["left", "center", "right"],
            $"{owner}.headerAvatarAlignment");
        RequireSlot(conversation, "headerLeftIconRowSlot", owner);
        RequireSlot(conversation, "headerRightIconRowSlot", owner);
        var headerLeftIconRowInputs = JsonPath.RequiredObject(
            conversation,
            "headerLeftIconRowInputs",
            owner);
        ValidateIconRowRuntimeInputs(
            headerLeftIconRowInputs,
            $"{owner}.headerLeftIconRowInputs");
        var headerRightIconRowInputs = JsonPath.RequiredObject(
            conversation,
            "headerRightIconRowInputs",
            owner);
        ValidateIconRowRuntimeInputs(
            headerRightIconRowInputs,
            $"{owner}.headerRightIconRowInputs");
        JsonPath.RequiredBoolean(conversation, "showStatusBar", owner);
        JsonPath.RequiredBoolean(conversation, "showNavigationBar", owner);
        JsonPath.RequiredBoolean(conversation, "showTextInputBar", owner);
        RequireSlot(conversation, "textInputBarSlot", owner);
        JsonPath.RequiredBoolean(conversation, "showKeyboard", owner);
        RequireSlot(conversation, "keyboardSlot", owner);
        RequireSlot(conversation, "bubbleSlot", owner);
        RequireRange(
            JsonPath.RequiredNumber(conversation, "bubbleMaxWidth", owner),
            1,
            100,
            $"{owner}.bubbleMaxWidth");
        JsonPath.RequiredString(conversation, "screenGutter", owner);
        JsonPath.RequiredString(conversation, "messageGap", owner);

        MotionVariantValue.Parse(
            JsonPath.RequiredObject(conversation, "messageMotion", owner).ToJsonString());
        MotionVariantValue.Parse(
            JsonPath.RequiredObject(
                conversation,
                "messageViewportMotion",
                owner).ToJsonString());
    }

    private static void RequireSlot(JsonObject conversation, string key, string owner)
    {
        var slot = JsonPath.RequiredObject(conversation, key, owner);
        var slotOwner = $"{owner}.{key}";
        JsonPath.RequiredString(slot, "variantReference", slotOwner);
        JsonPath.RequiredObject(slot, "overrides", slotOwner);
    }

    private static void ValidateIconRowRuntimeInputs(
        JsonObject inputs,
        string owner)
    {
        foreach (var key in inputs.Select((entry) => entry.Key))
        {
            if (!key.Equals("buttonInputs", StringComparison.Ordinal)
                && !key.Equals(
                    RuntimeInputForwardingContract.StorageKey,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{owner} contains unknown Runtime Input '{key}'.");
            }
        }
        RuntimeCollectionDocumentContract.Validate(
            JsonPath.RequiredArray(inputs, "buttonInputs", owner),
            $"{owner}.buttonInputs");
        _ = RuntimeInputForwardingContract.Labels(inputs);
    }

    private static void RequireNonNegative(double value, string path)
    {
        if (value < 0)
        {
            throw new InvalidOperationException($"{path} must be non-negative.");
        }
    }

    private static void RequireRange(double value, double minimum, double maximum, string path)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException($"{path} must be between {minimum} and {maximum}.");
        }
    }

    private static void RequireOneOf(string value, string[] options, string path)
    {
        if (!options.Contains(value, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"{path} has unsupported value '{value}'.");
        }
    }
}
