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
        ["module.core.socialPost.footerHeight"] = new(
            "module.core.socialPost",
            "module.core.socialPost.footerHeight",
            ValueKind.Integer,
            ["socialPost", "footerHeight"],
            "",
            []),
        ["module.core.socialPost.footerRowGapToken"] = new(
            "module.core.socialPost",
            "module.core.socialPost.footerRowGapToken",
            ValueKind.ThemeToken,
            ["socialPost", "footerRowGapToken"],
            "",
            []),
        ["module.core.socialPost.footerRows"] = new(
            "module.core.socialPost",
            "module.core.socialPost.footerRows",
            ValueKind.StructuredCollection,
            ["socialPost", "footerRows"],
            "",
            []),
        ["module.core.socialPost.footerSurface"] = new(
            "module.core.socialPost",
            "module.core.socialPost.footerSurface",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "footerSurfaceSlot"],
            "surface",
            []),
        ["module.core.socialPost.gallery"] = new(
            "module.core.socialPost",
            "module.core.socialPost.gallery",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "gallerySlot"],
            "gallery",
            []),
        ["module.core.socialPost.headerHeight"] = new(
            "module.core.socialPost",
            "module.core.socialPost.headerHeight",
            ValueKind.Integer,
            ["socialPost", "headerHeight"],
            "",
            []),
        ["module.core.socialPost.headerSurface"] = new(
            "module.core.socialPost",
            "module.core.socialPost.headerSurface",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "headerSurfaceSlot"],
            "surface",
            []),
        ["module.core.socialPost.media"] = new(
            "module.core.socialPost",
            "module.core.socialPost.media",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "mediaSlot"],
            "media",
            []),
        ["module.core.socialPost.mediaHeight"] = new(
            "module.core.socialPost",
            "module.core.socialPost.mediaHeight",
            ValueKind.Integer,
            ["socialPost", "mediaHeight"],
            "",
            []),
        ["module.core.socialPost.mediaHeightMode"] = new(
            "module.core.socialPost",
            "module.core.socialPost.mediaHeightMode",
            ValueKind.OptionToken,
            ["socialPost", "mediaHeightMode"],
            "",
            []),
        ["module.core.socialPost.mediaPadding"] = new(
            "module.core.socialPost",
            "module.core.socialPost.mediaPadding",
            ValueKind.ThemeTokenPair,
            ["socialPost", "mediaPadding"],
            "",
            []),
        ["module.core.socialPost.messageBubble"] = new(
            "module.core.socialPost",
            "module.core.socialPost.messageBubble",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "messageBubbleSlot"],
            "bubble",
            []),
        ["module.core.socialPost.messageKeyboard"] = new(
            "module.core.socialPost",
            "module.core.socialPost.messageKeyboard",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "messageKeyboardSlot"],
            "keyboard",
            []),
        ["module.core.socialPost.messageMinHeight"] = new(
            "module.core.socialPost",
            "module.core.socialPost.messageMinHeight",
            ValueKind.Integer,
            ["socialPost", "messageMinHeight"],
            "",
            []),
        ["module.core.socialPost.messagePadding"] = new(
            "module.core.socialPost",
            "module.core.socialPost.messagePadding",
            ValueKind.ThemeTokenPair,
            ["socialPost", "messagePadding"],
            "",
            []),
        ["module.core.socialPost.messageTextInputBar"] = new(
            "module.core.socialPost",
            "module.core.socialPost.messageTextInputBar",
            ValueKind.ComponentVariantSlot,
            ["socialPost", "messageTextInputBarSlot"],
            "textInputBar",
            []),
        ["module.core.socialPost.rowGapToken"] = new(
            "module.core.socialPost",
            "module.core.socialPost.rowGapToken",
            ValueKind.ThemeToken,
            ["socialPost", "rowGapToken"],
            "",
            []),
        ["module.core.socialPost.rows"] = new(
            "module.core.socialPost",
            "module.core.socialPost.rows",
            ValueKind.StructuredCollection,
            ["socialPost", "rows"],
            "",
            []),
        ["module.core.socialPost.showHeader"] = new(
            "module.core.socialPost",
            "module.core.socialPost.showHeader",
            ValueKind.Boolean,
            ["socialPost", "showHeader"],
            "",
            []),
        ["module.core.socialPost.showMedia"] = new(
            "module.core.socialPost",
            "module.core.socialPost.showMedia",
            ValueKind.Boolean,
            ["socialPost", "showMedia"],
            "",
            []),
        ["module.core.socialPost.showMediaSeparator"] = new(
            "module.core.socialPost",
            "module.core.socialPost.showMediaSeparator",
            ValueKind.Boolean,
            ["socialPost", "showMediaSeparator"],
            "",
            []),
        ["module.core.socialPost.showMessage"] = new(
            "module.core.socialPost",
            "module.core.socialPost.showMessage",
            ValueKind.Boolean,
            ["socialPost", "showMessage"],
            "",
            []),
        ["module.core.socialPost.showMessageSeparator"] = new(
            "module.core.socialPost",
            "module.core.socialPost.showMessageSeparator",
            ValueKind.Boolean,
            ["socialPost", "showMessageSeparator"],
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
