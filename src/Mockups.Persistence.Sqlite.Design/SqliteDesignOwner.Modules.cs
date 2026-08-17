using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    public ModuleSettings GetModuleSettings(string moduleId)
    {
        using var connection = OpenConnection();
        var record = _appModuleRepository.GetModule(
            connection,
            moduleId);
        var config = ParseJsonObject(record.ConfigJson);
        ApplyComponentInputBindingsProjections(
            connection,
            record.ProjectId,
            config,
            ComponentInputBindingsProjectionCatalog.RecordOwners());

        return new ModuleSettings(
            record.ProjectId,
            record.RecordClassId,
            record.SortOrder,
            config.ToJsonString(),
            record.DesignPreviewJson,
            record.MetadataJson);
    }

    public string GetModuleName(string moduleId) =>
        _appModuleRepository.GetModule(moduleId).Name;

    public IReadOnlyDictionary<string, string> GetModuleNames(
        IReadOnlyCollection<string> moduleIds)
    {
        var requested = moduleIds.ToHashSet(StringComparer.Ordinal);
        using var connection = OpenConnection();
        return _appModuleRepository.QueryModules(connection)
            .Where((module) => requested.Contains(module.Id))
            .ToDictionary(
                (module) => module.Id,
                (module) => module.Name,
                StringComparer.Ordinal);
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
            "module.core.chat.",
            StringComparison.Ordinal)
                ? JsonPath.RequiredObject(
                    config,
                    "conversation",
                    "Module config")
                : null;
        var lockScreen = fieldId.StartsWith(
            "module.core.lockScreen.",
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
            "module.core.chat.showHeader" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "showHeader",
                        "Module config.conversation")),
            "module.core.chat.useAppWallpaper" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "useAppWallpaper",
                        "Module config.conversation")),
            "module.core.chat.headerHeight" =>
                RequiredNumberString(conversation!, "headerHeight"),
            "module.core.chat.headerSurface.editor" =>
                RequiredSlotReference(
                    conversation!,
                    "headerSurfaceSlot"),
            "module.core.chat.headerUseActorColor" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "headerUseActorColor",
                        "Module config.conversation")),
            "module.core.chat.headerAvatarVariant" =>
                JsonPath.RequiredString(
                    conversation!,
                    "headerAvatarVariant",
                    "Module config.conversation"),
            "module.core.chat.headerAvatarAlignment" =>
                JsonPath.RequiredString(
                    conversation!,
                    "headerAvatarAlignment",
                    "Module config.conversation"),
            "module.core.chat.headerLeftIconRow.editor" =>
                RequiredSlotReference(
                    conversation!,
                    "headerLeftIconRowSlot"),
            "module.core.chat.headerLeftIconRow.inputs" =>
                JsonPath.RequiredObject(
                    conversation!,
                    "headerLeftIconRowInputs",
                    "Module config.conversation").ToJsonString(),
            "module.core.chat.headerRightIconRow.editor" =>
                RequiredSlotReference(
                    conversation!,
                    "headerRightIconRowSlot"),
            "module.core.chat.headerRightIconRow.inputs" =>
                JsonPath.RequiredObject(
                    conversation!,
                    "headerRightIconRowInputs",
                    "Module config.conversation").ToJsonString(),
            "module.core.chat.showStatusBar" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "showStatusBar",
                        "Module config.conversation")),
            "module.core.chat.showNavigationBar" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "showNavigationBar",
                        "Module config.conversation")),
            "module.core.chat.showTextInputBar" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "showTextInputBar",
                        "Module config.conversation")),
            "module.core.chat.textInputBarVariant" =>
                JsonPath.RequiredString(
                    conversation!,
                    "textInputBarVariant",
                    "Module config.conversation"),
            "module.core.chat.showKeyboard" =>
                BooleanText.Format(
                    JsonPath.RequiredBoolean(
                        conversation!,
                        "showKeyboard",
                        "Module config.conversation")),
            "module.core.chat.keyboardVariant" =>
                JsonPath.RequiredString(
                    conversation!,
                    "keyboardVariant",
                    "Module config.conversation"),
            "module.core.chat.bubbleVariant" =>
                JsonPath.RequiredString(
                    conversation!,
                    "bubbleVariant",
                    "Module config.conversation"),
            "module.core.chat.bubbleMaxWidth" =>
                RequiredNumberString(conversation!, "bubbleMaxWidth"),
            "module.core.chat.screenGutter" =>
                JsonPath.RequiredString(
                    conversation!,
                    "screenGutter",
                    "Module config.conversation"),
            "module.core.chat.messageGap" =>
                JsonPath.RequiredString(
                    conversation!,
                    "messageGap",
                    "Module config.conversation"),
            "module.core.chat.messageMotion" =>
                JsonPath.RequiredObject(
                    conversation!,
                    "messageMotion",
                    "Module config.conversation").ToJsonString(),
            "module.core.chat.messageViewportMotion" =>
                conversation!["messageViewportMotion"]?.ToJsonString()
                    ?? (MotionVariantValue.Default with
                    {
                        Bounds = MotionVariantValue.Parent,
                    }).ToJsonString(),
            "module.core.lockScreen.statusBarVariant" =>
                RequiredSlotReference(lockScreen!, "statusBarSlot"),
            "module.core.lockScreen.navigationBarVariant" =>
                RequiredSlotReference(lockScreen!, "navigationBarSlot"),
            "module.core.lockScreen.stackVariant" =>
                RequiredSlotReference(lockScreen!, "stackSlot"),
            "module.core.lockScreen.stackInputs" =>
                JsonPath.RequiredObject(
                    lockScreen!,
                    "stackInputs",
                    "Module config.lockScreen").ToJsonString(),
            "module.core.lockScreen.stackItems" =>
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

    public void UpdateModuleField(
        string moduleId,
        string fieldId,
        string value)
    {
        using var connection = OpenConnection();
        var module = _appModuleRepository.GetModule(
            connection,
            moduleId);
        if (fieldId == "module.appearanceMode"
            || fieldId.StartsWith(
                "module.core.chat.",
                StringComparison.Ordinal)
            || fieldId.StartsWith(
                "module.core.lockScreen.",
                StringComparison.Ordinal)
            || GeneratedModuleScaffoldConfigRegistry.TryGetField(
                module.RecordClassId,
                fieldId,
                out _))
        {
            UpdateModuleConfigField(
                connection,
                moduleId,
                fieldId,
                value);
            return;
        }

        switch (fieldId)
        {
            case "module.sortOrder":
                _appModuleRepository.UpdateModuleSortOrder(
                    connection,
                    moduleId,
                    NumericText.Int32(value, 0));
                return;
            case "module.metadata":
                var metadata = ParseJsonObject(value);
                VariantEnvelopeContract.RequiredArray(
                    metadata,
                    "variants",
                    $"Module '{moduleId}'");
                _appModuleRepository.UpdateModuleMetadata(
                    connection,
                    moduleId,
                    value);
                return;
            case "module.recordClassId":
                return;
            default:
                throw new InvalidOperationException(
                    $"Unknown module field '{fieldId}'.");
        }
    }

    private void UpdateModuleConfigField(
        SqliteConnection connection,
        string moduleId,
        string fieldId,
        string value)
    {
        var module = _appModuleRepository.GetModule(
            connection,
            moduleId);
        var config = ParseJsonObject(module.ConfigJson);

        UpdateModuleConfigFieldValue(
            connection,
            module.ProjectId,
            module.RecordClassId,
            config,
            fieldId,
            value);
        _appModuleRepository.UpdateModuleConfig(
            connection,
            moduleId,
            config.ToJsonString());
    }

    internal void UpdateModuleConfigFieldValue(
        SqliteConnection connection,
        string projectId,
        string recordClassId,
        JsonObject config,
        string fieldId,
        string value)
    {
        if (GeneratedModuleScaffoldConfigRegistry.TryGetField(
                recordClassId,
                fieldId,
                out var generated))
        {
            var next = RuntimeInputValueKindContract.ParseValue(
                generated.ValueKind,
                value,
                $"Module field '{fieldId}' value");
            if (!string.IsNullOrWhiteSpace(
                    generated.ComponentVariantType))
            {
                if (generated.ValueKind == ValueKind.ComponentVariant)
                {
                    next = JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            generated.ComponentVariantType,
                            next.GetValue<string>()))!;
                }
                else if (
                    generated.ValueKind
                        == ValueKind.ComponentVariantSlot)
                {
                    var slot = next.AsObject();
                    var owner = $"Module field '{fieldId}'";
                    var reference =
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            generated.ComponentVariantType,
                            ComponentVariantSlotDocumentContract
                                .VariantReference(slot, owner));
                    slot["variantReference"] = reference;
                    foreach (var path in generated
                        .SynchronizedVariantReferenceJsonPaths)
                    {
                        JsonPath.Set(
                            config,
                            path,
                            JsonValue.Create(reference)!);
                    }
                }
            }
            JsonPath.Set(config, generated.JsonPath, next);
            ApplyComponentInputBindingsProjections(
                connection,
                projectId,
                config,
                ComponentInputBindingsProjectionCatalog.RecordOwners());
            CurrentModuleConfigContract.Validate(
                recordClassId,
                config,
                "Edited Module config");
            return;
        }

        switch (fieldId)
        {
            case "module.appearanceMode":
                SetJsonValue(
                    config,
                    ["appearanceMode"],
                    JsonValue.Create(
                        ModuleAppearanceModeContract.Require(
                            value,
                            "Module Variant config"))!);
                break;
            case "module.core.chat.showHeader":
                SetJsonValue(
                    config,
                    ["conversation", "showHeader"],
                    JsonValue.Create(
                        BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.core.chat.useAppWallpaper":
                SetJsonValue(
                    config,
                    ["conversation", "useAppWallpaper"],
                    JsonValue.Create(
                        BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.core.chat.headerHeight":
                SetJsonValue(
                    config,
                    ["conversation", "headerHeight"],
                    JsonPath.ParseRequiredNumberNode(
                        value,
                        fieldId));
                break;
            case "module.core.chat.headerSurface.editor":
                SetJsonValue(
                    config,
                    [
                        "conversation",
                        "headerSurfaceSlot",
                        "variantReference",
                    ],
                    JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            "surface",
                            value))!);
                break;
            case "module.core.chat.headerUseActorColor":
                SetJsonValue(
                    config,
                    ["conversation", "headerUseActorColor"],
                    JsonValue.Create(
                        BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.core.chat.headerAvatarVariant":
                SetJsonValue(
                    config,
                    ["conversation", "headerAvatarVariant"],
                    JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            "avatar",
                            value))!);
                break;
            case "module.core.chat.headerAvatarAlignment":
                SetJsonValue(
                    config,
                    ["conversation", "headerAvatarAlignment"],
                    JsonValue.Create(value)!);
                break;
            case "module.core.chat.headerLeftIconRow.editor":
                SetJsonValue(
                    config,
                    [
                        "conversation",
                        "headerLeftIconRowSlot",
                        "variantReference",
                    ],
                    JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            "iconRow",
                            value))!);
                break;
            case "module.core.chat.headerLeftIconRow.inputs":
                SetJsonValue(
                    config,
                    ["conversation", "headerLeftIconRowInputs"],
                    JsonPath.ParseRequiredObject(value, fieldId));
                break;
            case "module.core.chat.headerRightIconRow.editor":
                SetJsonValue(
                    config,
                    [
                        "conversation",
                        "headerRightIconRowSlot",
                        "variantReference",
                    ],
                    JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            "iconRow",
                            value))!);
                break;
            case "module.core.chat.headerRightIconRow.inputs":
                SetJsonValue(
                    config,
                    ["conversation", "headerRightIconRowInputs"],
                    JsonPath.ParseRequiredObject(value, fieldId));
                break;
            case "module.core.chat.showStatusBar":
                SetJsonValue(
                    config,
                    ["conversation", "showStatusBar"],
                    JsonValue.Create(
                        BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.core.chat.showNavigationBar":
                SetJsonValue(
                    config,
                    ["conversation", "showNavigationBar"],
                    JsonValue.Create(
                        BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.core.chat.showTextInputBar":
                SetJsonValue(
                    config,
                    ["conversation", "showTextInputBar"],
                    JsonValue.Create(
                        BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.core.chat.textInputBarVariant":
                SetJsonValue(
                    config,
                    ["conversation", "textInputBarVariant"],
                    JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            "textInputBar",
                            value))!);
                break;
            case "module.core.chat.showKeyboard":
                SetJsonValue(
                    config,
                    ["conversation", "showKeyboard"],
                    JsonValue.Create(
                        BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.core.chat.keyboardVariant":
                SetJsonValue(
                    config,
                    ["conversation", "keyboardVariant"],
                    JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            "keyboard",
                            value))!);
                break;
            case "module.core.chat.bubbleVariant":
                SetJsonValue(
                    config,
                    ["conversation", "bubbleVariant"],
                    JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            "bubble",
                            value))!);
                break;
            case "module.core.chat.bubbleMaxWidth":
                SetJsonValue(
                    config,
                    ["conversation", "bubbleMaxWidth"],
                    JsonPath.ParseRequiredNumberNode(
                        value,
                        fieldId));
                break;
            case "module.core.chat.screenGutter":
                SetJsonValue(
                    config,
                    ["conversation", "screenGutter"],
                    JsonValue.Create(value)!);
                break;
            case "module.core.chat.messageGap":
                SetJsonValue(
                    config,
                    ["conversation", "messageGap"],
                    JsonValue.Create(value)!);
                break;
            case "module.core.chat.messageMotion":
                SetJsonValue(
                    config,
                    ["conversation", "messageMotion"],
                    JsonNode.Parse(
                        MotionVariantValue.Parse(value)
                            .ToJsonString())!);
                break;
            case "module.core.chat.messageViewportMotion":
                SetJsonValue(
                    config,
                    ["conversation", "messageViewportMotion"],
                    JsonNode.Parse(
                        MotionVariantValue.Parse(value)
                            .ToJsonString())!);
                break;
            case "module.core.lockScreen.statusBarVariant":
                SetJsonValue(
                    config,
                    [
                        "lockScreen",
                        "statusBarSlot",
                        "variantReference",
                    ],
                    JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            "status_bar",
                            value))!);
                break;
            case "module.core.lockScreen.navigationBarVariant":
                SetJsonValue(
                    config,
                    [
                        "lockScreen",
                        "navigationBarSlot",
                        "variantReference",
                    ],
                    JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            "navigation_bar",
                            value))!);
                break;
            case "module.core.lockScreen.stackVariant":
                SetJsonValue(
                    config,
                    [
                        "lockScreen",
                        "stackSlot",
                        "variantReference",
                    ],
                    JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            "componentStack",
                            value))!);
                break;
            case "module.core.lockScreen.stackInputs":
                SetJsonValue(
                    config,
                    ["lockScreen", "stackInputs"],
                    JsonPath.ParseRequiredObject(value, fieldId));
                break;
            case "module.core.lockScreen.stackItems":
                SetJsonValue(
                    config,
                    ["lockScreen", "stackInputs", "items"],
                    JsonPath.ParseRequiredArray(value, fieldId));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown module config field '{fieldId}'.");
        }

        ApplyComponentInputBindingsProjections(
            connection,
            projectId,
            config,
            ComponentInputBindingsProjectionCatalog.RecordOwners());
        CurrentModuleConfigContract.Validate(
            recordClassId,
            config,
            "Edited Module config");
    }
}
