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
        JsonPath.RequiredString(conversation, "headerAvatarVariant", owner);
        RequireOneOf(
            JsonPath.RequiredString(conversation, "headerAvatarAlignment", owner),
            ["left", "center", "right"],
            $"{owner}.headerAvatarAlignment");
        RequireSlot(conversation, "headerLeftIconRowSlot", owner);
        RequireSlot(conversation, "headerRightIconRowSlot", owner);
        JsonPath.RequiredObject(conversation, "headerLeftIconRowInputs", owner);
        JsonPath.RequiredObject(conversation, "headerRightIconRowInputs", owner);
        JsonPath.RequiredBoolean(conversation, "showStatusBar", owner);
        JsonPath.RequiredBoolean(conversation, "showNavigationBar", owner);
        JsonPath.RequiredBoolean(conversation, "showTextInputBar", owner);
        JsonPath.RequiredString(conversation, "textInputBarVariant", owner);
        JsonPath.RequiredBoolean(conversation, "showKeyboard", owner);
        JsonPath.RequiredString(conversation, "keyboardVariant", owner);
        JsonPath.RequiredString(conversation, "bubbleVariant", owner);
        RequireRange(
            JsonPath.RequiredNumber(conversation, "bubbleMaxWidth", owner),
            1,
            100,
            $"{owner}.bubbleMaxWidth");
        JsonPath.RequiredString(conversation, "screenGutter", owner);
        JsonPath.RequiredString(conversation, "messageGap", owner);

        if (conversation["messageViewportMotion"] is JsonNode motion)
        {
            MotionVariantValue.Parse(motion.ToJsonString());
        }
    }

    private static void RequireSlot(JsonObject conversation, string key, string owner)
    {
        var slot = JsonPath.RequiredObject(conversation, key, owner);
        var slotOwner = $"{owner}.{key}";
        JsonPath.RequiredString(slot, "variantReference", slotOwner);
        JsonPath.RequiredObject(slot, "overrides", slotOwner);
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
