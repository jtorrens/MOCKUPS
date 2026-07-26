using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    public ModuleSettings GetModuleSettings(string moduleId)
    {
        var record = _appModuleRepository.GetModule(moduleId);

        return new ModuleSettings(
            record.ProjectId,
            record.RecordClassId,
            record.SortOrder,
            record.ConfigJson,
            record.DesignPreviewJson,
            record.MetadataJson);
    }

    public void UpdateModuleDesignPreviewJson(
        string moduleId,
        string designPreviewJson) =>
        _appModuleRepository.UpdateModuleDesignPreview(
            moduleId,
            designPreviewJson);

    public AppSettings GetModuleAppSettings(string moduleId)
    {
        var record = _appModuleRepository.GetModuleApp(moduleId);

        return new AppSettings(
            record.ProjectId,
            record.BundleKey,
            record.AppType,
            record.ConfigJson,
            record.MetadataJson);
    }

    public string GetModuleConfigFieldValue(
        string moduleId,
        string fieldId)
    {
        var settings = GetModuleSettings(moduleId);
        return ModuleConfigFieldValue(
            settings.RecordClassId,
            settings.ConfigJson,
            fieldId);
    }

    internal static string ModuleConfigFieldValue(
        string recordClassId,
        string configJson,
        string fieldId)
    {
        var config = ParseJsonObject(configJson);
        if (GeneratedModuleScaffoldConfigRegistry.TryGetField(
                recordClassId,
                fieldId,
                out var generated))
        {
            var node = JsonPath.Get(config, generated.JsonPath)
                ?? throw new InvalidOperationException(
                    $"Module config field '{fieldId}' is missing '{string.Join(".", generated.JsonPath)}'.");
            RuntimeInputValueKindContract.ValidateValue(
                generated.ValueKind,
                node,
                $"Module config field '{fieldId}'");
            return generated.ValueKind switch
            {
                ValueKind.Boolean =>
                    BooleanText.Format(node.GetValue<bool>()),
                ValueKind.Integer
                    or ValueKind.Decimal
                    or ValueKind.HueDegrees
                    or ValueKind.Alpha =>
                        node.ToJsonString(),
                ValueKind.TypographyStyle
                    or ValueKind.TypographySystemStyle =>
                        TypographyStyleValue.Parse(node).ToJsonString(),
                ValueKind.AlignmentPlacement
                    or ValueKind.Motion
                    or ValueKind.MotionTiming
                    or ValueKind.IconTokenList
                    or ValueKind.IconSlots
                    or ValueKind.ComponentInputBindings
                    or ValueKind.ComponentVariantSlot
                    or ValueKind.StructuredCollection
                    or ValueKind.BehaviorTiming =>
                        node.ToJsonString(),
                _ => node.GetValue<string>(),
            };
        }

        var conversation = fieldId.StartsWith(
            "module.conversation.",
            StringComparison.Ordinal)
                ? JsonPath.RequiredObject(
                    config,
                    "conversation",
                    "Module config")
                : null;
        var lockScreen = fieldId.StartsWith(
            "module.lockScreen.",
            StringComparison.Ordinal)
                ? JsonPath.RequiredObject(
                    config,
                    "lockScreen",
                    "Module config")
                : null;
        return fieldId switch
        {
            "module.appearanceMode" =>
                ModuleAppearanceModeContract.Read(
                    config,
                    "Module Variant config"),
            "module.conversation.showHeader" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "showHeader",
                        "Module config.conversation")),
            "module.conversation.useAppWallpaper" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "useAppWallpaper",
                        "Module config.conversation")),
            "module.conversation.headerHeight" =>
                RequiredNumberString(conversation!, "headerHeight"),
            "module.conversation.headerAvatarVariant" =>
                JsonPath.RequiredString(
                    conversation!,
                    "headerAvatarVariant",
                    "Module config.conversation"),
            "module.conversation.headerAvatarAlignment" =>
                JsonPath.RequiredString(
                    conversation!,
                    "headerAvatarAlignment",
                    "Module config.conversation"),
            "module.conversation.headerLeftIconRow.editor" =>
                RequiredSlotReference(
                    conversation!,
                    "headerLeftIconRowSlot"),
            "module.conversation.headerLeftIconRow.inputs" =>
                JsonPath.RequiredObject(
                    conversation!,
                    "headerLeftIconRowInputs",
                    "Module config.conversation").ToJsonString(),
            "module.conversation.headerRightIconRow.editor" =>
                RequiredSlotReference(
                    conversation!,
                    "headerRightIconRowSlot"),
            "module.conversation.headerRightIconRow.inputs" =>
                JsonPath.RequiredObject(
                    conversation!,
                    "headerRightIconRowInputs",
                    "Module config.conversation").ToJsonString(),
            "module.conversation.showStatusBar" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "showStatusBar",
                        "Module config.conversation")),
            "module.conversation.showNavigationBar" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "showNavigationBar",
                        "Module config.conversation")),
            "module.conversation.showTextInputBar" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "showTextInputBar",
                        "Module config.conversation")),
            "module.conversation.textInputBarVariant" =>
                JsonPath.RequiredString(
                    conversation!,
                    "textInputBarVariant",
                    "Module config.conversation"),
            "module.conversation.showKeyboard" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "showKeyboard",
                        "Module config.conversation")),
            "module.conversation.keyboardVariant" =>
                JsonPath.RequiredString(
                    conversation!,
                    "keyboardVariant",
                    "Module config.conversation"),
            "module.conversation.bubbleVariant" =>
                JsonPath.RequiredString(
                    conversation!,
                    "bubbleVariant",
                    "Module config.conversation"),
            "module.conversation.bubbleMaxWidth" =>
                RequiredNumberString(conversation!, "bubbleMaxWidth"),
            "module.conversation.screenGutter" =>
                JsonPath.RequiredString(
                    conversation!,
                    "screenGutter",
                    "Module config.conversation"),
            "module.conversation.messageGap" =>
                JsonPath.RequiredString(
                    conversation!,
                    "messageGap",
                    "Module config.conversation"),
            "module.conversation.messageViewportMotion" =>
                conversation!["messageViewportMotion"]?.ToJsonString()
                    ?? (MotionVariantValue.Default with
                    {
                        Bounds = MotionVariantValue.Parent,
                    }).ToJsonString(),
            "module.lockScreen.statusBarVariant" =>
                RequiredSlotReference(lockScreen!, "statusBarSlot"),
            "module.lockScreen.navigationBarVariant" =>
                RequiredSlotReference(lockScreen!, "navigationBarSlot"),
            "module.lockScreen.stackVariant" =>
                RequiredSlotReference(lockScreen!, "stackSlot"),
            "module.lockScreen.stackInputs" =>
                JsonPath.RequiredObject(
                    lockScreen!,
                    "stackInputs",
                    "Module config.lockScreen").ToJsonString(),
            "module.lockScreen.stackItems" =>
                JsonPath.RequiredArray(
                    JsonPath.RequiredObject(
                        lockScreen!,
                        "stackInputs",
                        "Module config.lockScreen"),
                    "items",
                    "Module config.lockScreen.stackInputs")
                    .ToJsonString(),
            _ => throw new InvalidOperationException(
                $"Unknown module config field '{fieldId}'."),
        };
    }

    private static string RequiredSlotReference(
        JsonObject owner,
        string key)
    {
        var slot = JsonPath.RequiredObject(
            owner,
            key,
            "Module config");
        return JsonPath.RequiredString(
            slot,
            "variantReference",
            $"Module config.{key}");
    }

    private static string RequiredNumberString(
        JsonObject owner,
        string key) =>
        JsonPath.RequiredNumber(owner, key, "Module config")
            .ToString(CultureInfo.InvariantCulture);
}
