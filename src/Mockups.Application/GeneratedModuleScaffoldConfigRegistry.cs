// Generated from scaffolding/modules/*.json. Do not edit manually.
// Modules: module.core.chat, module.core.chatList, module.core.lockScreen, module.core.socialPost
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed record GeneratedModuleConfigFieldDescriptor(
    string RecordClassId,
    string FieldId,
    ValueKind ValueKind,
    string[] JsonPath,
    string ComponentVariantType,
    string[][] SynchronizedVariantReferenceJsonPaths);

internal static class GeneratedModuleScaffoldConfigRegistry
{
    private static readonly Dictionary<string, GeneratedModuleConfigFieldDescriptor> Fields =
        new(StringComparer.Ordinal)
    {
        ["module.core.chat.bubbleMaxWidth"] = new(
            "module.core.chat",
            "module.core.chat.bubbleMaxWidth",
            ValueKind.Integer,
            ["conversation", "bubbleMaxWidth"],
            "",
            []),
        ["module.core.chat.bubbleVariant"] = new(
            "module.core.chat",
            "module.core.chat.bubbleVariant",
            ValueKind.ComponentVariantSlot,
            ["conversation", "bubbleSlot"],
            "bubble",
            []),
        ["module.core.chat.headerAvatar.editor"] = new(
            "module.core.chat",
            "module.core.chat.headerAvatar.editor",
            ValueKind.ComponentVariantSlot,
            ["conversation", "headerAvatarSlot"],
            "avatar",
            []),
        ["module.core.chat.headerAvatarAlignment"] = new(
            "module.core.chat",
            "module.core.chat.headerAvatarAlignment",
            ValueKind.OptionToken,
            ["conversation", "headerAvatarAlignment"],
            "",
            []),
        ["module.core.chat.headerHeight"] = new(
            "module.core.chat",
            "module.core.chat.headerHeight",
            ValueKind.Integer,
            ["conversation", "headerHeight"],
            "",
            []),
        ["module.core.chat.headerLeftIconRow.editor"] = new(
            "module.core.chat",
            "module.core.chat.headerLeftIconRow.editor",
            ValueKind.ComponentVariantSlot,
            ["conversation", "headerLeftIconRowSlot"],
            "iconRow",
            []),
        ["module.core.chat.headerLeftIconRow.inputs"] = new(
            "module.core.chat",
            "module.core.chat.headerLeftIconRow.inputs",
            ValueKind.ComponentInputBindings,
            ["conversation", "headerLeftIconRowInputs"],
            "",
            []),
        ["module.core.chat.headerRightIconRow.editor"] = new(
            "module.core.chat",
            "module.core.chat.headerRightIconRow.editor",
            ValueKind.ComponentVariantSlot,
            ["conversation", "headerRightIconRowSlot"],
            "iconRow",
            []),
        ["module.core.chat.headerRightIconRow.inputs"] = new(
            "module.core.chat",
            "module.core.chat.headerRightIconRow.inputs",
            ValueKind.ComponentInputBindings,
            ["conversation", "headerRightIconRowInputs"],
            "",
            []),
        ["module.core.chat.headerSurface.editor"] = new(
            "module.core.chat",
            "module.core.chat.headerSurface.editor",
            ValueKind.ComponentVariantSlot,
            ["conversation", "headerSurfaceSlot"],
            "surface",
            []),
        ["module.core.chat.headerUseActorColor"] = new(
            "module.core.chat",
            "module.core.chat.headerUseActorColor",
            ValueKind.Boolean,
            ["conversation", "headerUseActorColor"],
            "",
            []),
        ["module.core.chat.keyboardVariant"] = new(
            "module.core.chat",
            "module.core.chat.keyboardVariant",
            ValueKind.ComponentVariantSlot,
            ["conversation", "keyboardSlot"],
            "keyboard",
            []),
        ["module.core.chat.messageGap"] = new(
            "module.core.chat",
            "module.core.chat.messageGap",
            ValueKind.ThemeToken,
            ["conversation", "messageGap"],
            "",
            []),
        ["module.core.chat.messageMotion"] = new(
            "module.core.chat",
            "module.core.chat.messageMotion",
            ValueKind.Motion,
            ["conversation", "messageMotion"],
            "",
            []),
        ["module.core.chat.messageReflowTiming"] = new(
            "module.core.chat",
            "module.core.chat.messageReflowTiming",
            ValueKind.MotionTiming,
            ["conversation", "messageReflowTiming"],
            "",
            []),
        ["module.core.chat.messageViewportMotion"] = new(
            "module.core.chat",
            "module.core.chat.messageViewportMotion",
            ValueKind.Motion,
            ["conversation", "messageViewportMotion"],
            "",
            []),
        ["module.core.chat.screenGutter"] = new(
            "module.core.chat",
            "module.core.chat.screenGutter",
            ValueKind.ThemeTokenPair,
            ["conversation", "screenGutter"],
            "",
            []),
        ["module.core.chat.showHeader"] = new(
            "module.core.chat",
            "module.core.chat.showHeader",
            ValueKind.Boolean,
            ["conversation", "showHeader"],
            "",
            []),
        ["module.core.chat.showHeaderSeparator"] = new(
            "module.core.chat",
            "module.core.chat.showHeaderSeparator",
            ValueKind.Boolean,
            ["conversation", "showHeaderSeparator"],
            "",
            []),
        ["module.core.chat.showKeyboard"] = new(
            "module.core.chat",
            "module.core.chat.showKeyboard",
            ValueKind.Boolean,
            ["conversation", "showKeyboard"],
            "",
            []),
        ["module.core.chat.showNavigationBar"] = new(
            "module.core.chat",
            "module.core.chat.showNavigationBar",
            ValueKind.Boolean,
            ["conversation", "showNavigationBar"],
            "",
            []),
        ["module.core.chat.showStatusBar"] = new(
            "module.core.chat",
            "module.core.chat.showStatusBar",
            ValueKind.Boolean,
            ["conversation", "showStatusBar"],
            "",
            []),
        ["module.core.chat.showTextInputBar"] = new(
            "module.core.chat",
            "module.core.chat.showTextInputBar",
            ValueKind.Boolean,
            ["conversation", "showTextInputBar"],
            "",
            []),
        ["module.core.chat.textInputBarVariant"] = new(
            "module.core.chat",
            "module.core.chat.textInputBarVariant",
            ValueKind.ComponentVariantSlot,
            ["conversation", "textInputBarSlot"],
            "textInputBar",
            []),
        ["module.core.chat.useAppWallpaper"] = new(
            "module.core.chat",
            "module.core.chat.useAppWallpaper",
            ValueKind.Boolean,
            ["conversation", "useAppWallpaper"],
            "",
            []),
        ["module.core.chatList.bottomIconBar"] = new(
            "module.core.chatList",
            "module.core.chatList.bottomIconBar",
            ValueKind.ComponentVariantSlot,
            ["chatList", "bottomIconBarSlot"],
            "iconBar",
            []),
        ["module.core.chatList.list"] = new(
            "module.core.chatList",
            "module.core.chatList.list",
            ValueKind.ComponentVariantSlot,
            ["chatList", "listSlot"],
            "list",
            [["chatList", "runtimeContract", "variantReference"]]),
        ["module.core.chatList.navigationBar"] = new(
            "module.core.chatList",
            "module.core.chatList.navigationBar",
            ValueKind.ComponentVariantSlot,
            ["chatList", "navigationBarSlot"],
            "navigation_bar",
            []),
        ["module.core.chatList.stack"] = new(
            "module.core.chatList",
            "module.core.chatList.stack",
            ValueKind.ComponentVariantSlot,
            ["chatList", "stackSlot"],
            "componentStack",
            []),
        ["module.core.chatList.statusBar"] = new(
            "module.core.chatList",
            "module.core.chatList.statusBar",
            ValueKind.ComponentVariantSlot,
            ["chatList", "statusBarSlot"],
            "status_bar",
            []),
        ["module.core.chatList.topIconBar"] = new(
            "module.core.chatList",
            "module.core.chatList.topIconBar",
            ValueKind.ComponentVariantSlot,
            ["chatList", "topIconBarSlot"],
            "iconBar",
            []),
        ["module.core.chatList.wallpaperEnabled"] = new(
            "module.core.chatList",
            "module.core.chatList.wallpaperEnabled",
            ValueKind.Boolean,
            ["chatList", "wallpaperEnabled"],
            "",
            []),
        ["module.core.lockScreen.navigationBarVariant"] = new(
            "module.core.lockScreen",
            "module.core.lockScreen.navigationBarVariant",
            ValueKind.ComponentVariantSlot,
            ["lockScreen", "navigationBarSlot"],
            "navigation_bar",
            []),
        ["module.core.lockScreen.stackInputs"] = new(
            "module.core.lockScreen",
            "module.core.lockScreen.stackInputs",
            ValueKind.ComponentInputBindings,
            ["lockScreen", "stackInputs"],
            "",
            []),
        ["module.core.lockScreen.stackItems"] = new(
            "module.core.lockScreen",
            "module.core.lockScreen.stackItems",
            ValueKind.StructuredCollection,
            ["lockScreen", "stackInputs", "items"],
            "",
            []),
        ["module.core.lockScreen.stackVariant"] = new(
            "module.core.lockScreen",
            "module.core.lockScreen.stackVariant",
            ValueKind.ComponentVariantSlot,
            ["lockScreen", "stackSlot"],
            "componentStack",
            []),
        ["module.core.lockScreen.statusBarVariant"] = new(
            "module.core.lockScreen",
            "module.core.lockScreen.statusBarVariant",
            ValueKind.ComponentVariantSlot,
            ["lockScreen", "statusBarSlot"],
            "status_bar",
            []),
        ["module.core.socialPost.headerSurface"] = new(
            "module.core.socialPost",
            "module.core.socialPost.headerSurface",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "headerSurfaceSlot"],
            "surface",
            []),
        ["module.core.socialPost.row1Padding"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Padding",
            ValueKind.ThemeTokenPair,
            ["socialPost", "row1Padding"],
            "",
            []),
        ["module.core.socialPost.row1ShowSeparator"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1ShowSeparator",
            ValueKind.Boolean,
            ["socialPost", "row1ShowSeparator"],
            "",
            []),
        ["module.core.socialPost.row1Slot1.avatar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot1.avatar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot1AvatarSlot"],
            "avatar",
            []),
        ["module.core.socialPost.row1Slot1.icon"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot1.icon",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot1IconSlot"],
            "button",
            []),
        ["module.core.socialPost.row1Slot1.kind"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot1.kind",
            ValueKind.OptionToken,
            ["socialPost", "row1Slot1Kind"],
            "",
            []),
        ["module.core.socialPost.row1Slot1.label"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot1.label",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot1LabelSlot"],
            "label",
            []),
        ["module.core.socialPost.row1Slot2.avatar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot2.avatar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot2AvatarSlot"],
            "avatar",
            []),
        ["module.core.socialPost.row1Slot2.icon"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot2.icon",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot2IconSlot"],
            "button",
            []),
        ["module.core.socialPost.row1Slot2.kind"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot2.kind",
            ValueKind.OptionToken,
            ["socialPost", "row1Slot2Kind"],
            "",
            []),
        ["module.core.socialPost.row1Slot2.label"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot2.label",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot2LabelSlot"],
            "label",
            []),
        ["module.core.socialPost.row1Slot3.avatar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot3.avatar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot3AvatarSlot"],
            "avatar",
            []),
        ["module.core.socialPost.row1Slot3.icon"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot3.icon",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot3IconSlot"],
            "button",
            []),
        ["module.core.socialPost.row1Slot3.kind"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot3.kind",
            ValueKind.OptionToken,
            ["socialPost", "row1Slot3Kind"],
            "",
            []),
        ["module.core.socialPost.row1Slot3.label"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot3.label",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot3LabelSlot"],
            "label",
            []),
        ["module.core.socialPost.row1Slot4.avatar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot4.avatar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot4AvatarSlot"],
            "avatar",
            []),
        ["module.core.socialPost.row1Slot4.icon"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot4.icon",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot4IconSlot"],
            "button",
            []),
        ["module.core.socialPost.row1Slot4.kind"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot4.kind",
            ValueKind.OptionToken,
            ["socialPost", "row1Slot4Kind"],
            "",
            []),
        ["module.core.socialPost.row1Slot4.label"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot4.label",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot4LabelSlot"],
            "label",
            []),
        ["module.core.socialPost.row1Slot5.avatar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot5.avatar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot5AvatarSlot"],
            "avatar",
            []),
        ["module.core.socialPost.row1Slot5.icon"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot5.icon",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot5IconSlot"],
            "button",
            []),
        ["module.core.socialPost.row1Slot5.kind"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot5.kind",
            ValueKind.OptionToken,
            ["socialPost", "row1Slot5Kind"],
            "",
            []),
        ["module.core.socialPost.row1Slot5.label"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1Slot5.label",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row1Slot5LabelSlot"],
            "label",
            []),
        ["module.core.socialPost.row1VerticalAlignment"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row1VerticalAlignment",
            ValueKind.OptionToken,
            ["socialPost", "row1VerticalAlignment"],
            "",
            []),
        ["module.core.socialPost.row2Padding"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Padding",
            ValueKind.ThemeTokenPair,
            ["socialPost", "row2Padding"],
            "",
            []),
        ["module.core.socialPost.row2ShowSeparator"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2ShowSeparator",
            ValueKind.Boolean,
            ["socialPost", "row2ShowSeparator"],
            "",
            []),
        ["module.core.socialPost.row2Slot1.avatar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot1.avatar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot1AvatarSlot"],
            "avatar",
            []),
        ["module.core.socialPost.row2Slot1.icon"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot1.icon",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot1IconSlot"],
            "button",
            []),
        ["module.core.socialPost.row2Slot1.kind"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot1.kind",
            ValueKind.OptionToken,
            ["socialPost", "row2Slot1Kind"],
            "",
            []),
        ["module.core.socialPost.row2Slot1.label"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot1.label",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot1LabelSlot"],
            "label",
            []),
        ["module.core.socialPost.row2Slot2.avatar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot2.avatar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot2AvatarSlot"],
            "avatar",
            []),
        ["module.core.socialPost.row2Slot2.icon"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot2.icon",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot2IconSlot"],
            "button",
            []),
        ["module.core.socialPost.row2Slot2.kind"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot2.kind",
            ValueKind.OptionToken,
            ["socialPost", "row2Slot2Kind"],
            "",
            []),
        ["module.core.socialPost.row2Slot2.label"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot2.label",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot2LabelSlot"],
            "label",
            []),
        ["module.core.socialPost.row2Slot3.avatar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot3.avatar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot3AvatarSlot"],
            "avatar",
            []),
        ["module.core.socialPost.row2Slot3.icon"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot3.icon",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot3IconSlot"],
            "button",
            []),
        ["module.core.socialPost.row2Slot3.kind"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot3.kind",
            ValueKind.OptionToken,
            ["socialPost", "row2Slot3Kind"],
            "",
            []),
        ["module.core.socialPost.row2Slot3.label"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot3.label",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot3LabelSlot"],
            "label",
            []),
        ["module.core.socialPost.row2Slot4.avatar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot4.avatar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot4AvatarSlot"],
            "avatar",
            []),
        ["module.core.socialPost.row2Slot4.icon"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot4.icon",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot4IconSlot"],
            "button",
            []),
        ["module.core.socialPost.row2Slot4.kind"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot4.kind",
            ValueKind.OptionToken,
            ["socialPost", "row2Slot4Kind"],
            "",
            []),
        ["module.core.socialPost.row2Slot4.label"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot4.label",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot4LabelSlot"],
            "label",
            []),
        ["module.core.socialPost.row2Slot5.avatar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot5.avatar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot5AvatarSlot"],
            "avatar",
            []),
        ["module.core.socialPost.row2Slot5.icon"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot5.icon",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot5IconSlot"],
            "button",
            []),
        ["module.core.socialPost.row2Slot5.kind"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot5.kind",
            ValueKind.OptionToken,
            ["socialPost", "row2Slot5Kind"],
            "",
            []),
        ["module.core.socialPost.row2Slot5.label"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2Slot5.label",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "row2Slot5LabelSlot"],
            "label",
            []),
        ["module.core.socialPost.row2VerticalAlignment"] = new(
            "module.core.socialPost",
            "module.core.socialPost.row2VerticalAlignment",
            ValueKind.OptionToken,
            ["socialPost", "row2VerticalAlignment"],
            "",
            []),
        ["module.core.socialPost.rowGapToken"] = new(
            "module.core.socialPost",
            "module.core.socialPost.rowGapToken",
            ValueKind.ThemeToken,
            ["socialPost", "rowGapToken"],
            "",
            []),
        ["module.core.socialPost.showHeader"] = new(
            "module.core.socialPost",
            "module.core.socialPost.showHeader",
            ValueKind.Boolean,
            ["socialPost", "showHeader"],
            "",
            []),
        ["module.core.socialPost.showNavigationBar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.showNavigationBar",
            ValueKind.Boolean,
            ["socialPost", "showNavigationBar"],
            "",
            []),
        ["module.core.socialPost.showStatusBar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.showStatusBar",
            ValueKind.Boolean,
            ["socialPost", "showStatusBar"],
            "",
            []),
        ["module.core.socialPost.useAppWallpaper"] = new(
            "module.core.socialPost",
            "module.core.socialPost.useAppWallpaper",
            ValueKind.Boolean,
            ["socialPost", "useAppWallpaper"],
            "",
            []),
    };

    public static bool TryValidate(
        string recordClassId,
        JsonObject config,
        string context)
    {
        switch (recordClassId)
        {
            case "module.core.chat":
                ConversationModuleConfigContract.Validate(config, context);
                return true;
            case "module.core.chatList":
                ChatListModuleConfigContract.Validate(config, context);
                return true;
            case "module.core.lockScreen":
                LockScreenModuleConfigContract.Validate(config, context);
                return true;
            case "module.core.socialPost":
                SocialPostModuleConfigContract.Validate(config, context);
                return true;
            default:
                return false;
        }
    }

    public static bool TryGetField(
        string recordClassId,
        string fieldId,
        out GeneratedModuleConfigFieldDescriptor descriptor)
    {
        if (Fields.TryGetValue(fieldId, out var candidate)
            && candidate.RecordClassId.Equals(recordClassId, StringComparison.Ordinal))
        {
            descriptor = candidate;
            return true;
        }
        descriptor = new("", "", ValueKind.StringSingleLine, [], "", []);
        return false;
    }
}
