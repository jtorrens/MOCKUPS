using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public void UpdateShotField(string shotId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (fieldId == "shot.fps" && value == "inherited")
        {
            _productionOwner.ShotRepository.ClearFpsOverride(connection, shotId);
            return;
        }

        if (fieldId == "shot.ownerActorId")
        {
            _productionOwner.ModuleInstanceThemeContextService.RequireShotOwnerChange(connection, shotId, value);
        }

        _productionOwner.ShotRepository.UpdateField(connection, shotId, fieldId, value);
        if (fieldId == "shot.ownerActorId")
        {
            SynchronizeTimelineDurations(connection, shotId);
        }
    }

    public string GetShotOwnerDeviceName(string shotId)
    {
        using var connection = OpenConnection();
        var shot = _productionOwner.ShotRepository.Get(connection, shotId);
        var actor = _resourceOwner.ActorRepository.QueryAll(connection)
            .SingleOrDefault((candidate) => candidate.Id == shot.OwnerActorId)
            ?? throw new InvalidOperationException($"Missing Actor '{shot.OwnerActorId}'.");
        if (string.IsNullOrWhiteSpace(actor.DefaultDeviceId)) return "No default device";
        return _resourceOwner.DeviceRepository.QueryAll(connection)
            .SingleOrDefault((candidate) => candidate.Id == actor.DefaultDeviceId)?.Name
            ?? throw new InvalidOperationException($"Missing Device '{actor.DefaultDeviceId}'.");
    }

    public void UpdateModuleField(string moduleId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        var module = _designOwner.AppModuleRepository.GetModule(connection, moduleId);
        if (fieldId == "module.appearanceMode"
            || fieldId.StartsWith("module.conversation.", StringComparison.Ordinal)
            || fieldId.StartsWith("module.lockScreen.", StringComparison.Ordinal)
            || GeneratedModuleScaffoldConfigRegistry.TryGetField(
                module.RecordClassId,
                fieldId,
                out _))
        {
            UpdateModuleConfigField(connection, moduleId, fieldId, value);
            return;
        }

        switch (fieldId)
        {
            case "module.sortOrder":
                _designOwner.AppModuleRepository.UpdateModuleSortOrder(connection, moduleId, NumericText.Int32(value, 0));
                return;
            case "module.metadata":
                var metadata = ParseJsonObject(value);
                VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Module '{moduleId}'");
                _designOwner.AppModuleRepository.UpdateModuleMetadata(connection, moduleId, value);
                return;
            case "module.recordClassId":
                return;
            default:
                throw new InvalidOperationException($"Unknown module field '{fieldId}'.");
        }
    }

    private void UpdateModuleConfigField(SqliteConnection connection, string moduleId, string fieldId, string value)
    {
        var module = _designOwner.AppModuleRepository.GetModule(connection, moduleId);
        var config = ParseJsonObject(module.ConfigJson);

        UpdateModuleConfigFieldValue(connection, module.ProjectId, module.RecordClassId, config, fieldId, value);
        _designOwner.AppModuleRepository.UpdateModuleConfig(connection, moduleId, config.ToJsonString());
    }

    private void UpdateModuleConfigFieldValue(
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
            if (!string.IsNullOrWhiteSpace(generated.ComponentVariantType))
            {
                if (generated.ValueKind == ValueKind.ComponentVariant)
                {
                    next = JsonValue.Create(ValidateComponentVariantReference(
                        connection,
                        projectId,
                        generated.ComponentVariantType,
                        next.GetValue<string>()))!;
                }
                else if (generated.ValueKind == ValueKind.ComponentVariantSlot)
                {
                    var slot = next.AsObject();
                    var owner = $"Module field '{fieldId}'";
                    var reference = ValidateComponentVariantReference(
                        connection,
                        projectId,
                        generated.ComponentVariantType,
                        ComponentVariantSlotDocumentContract.VariantReference(slot, owner));
                    slot["variantReference"] = reference;
                    foreach (var path in generated.SynchronizedVariantReferenceJsonPaths)
                    {
                        JsonPath.Set(config, path, JsonValue.Create(reference)!);
                    }
                }
            }
            JsonPath.Set(config, generated.JsonPath, next);
            CurrentModuleConfigContract.Validate(recordClassId, config, "Edited Module config");
            return;
        }
        switch (fieldId)
        {
            case "module.appearanceMode":
                SetJsonValue(
                    config,
                    ["appearanceMode"],
                    JsonValue.Create(ModuleAppearanceModeContract.Require(value, "Module Variant config"))!);
                break;
            case "module.conversation.showHeader":
                SetJsonValue(config, ["conversation", "showHeader"], JsonValue.Create(BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.conversation.useAppWallpaper":
                SetJsonValue(config, ["conversation", "useAppWallpaper"], JsonValue.Create(BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.conversation.headerHeight":
                SetJsonValue(config, ["conversation", "headerHeight"], JsonPath.ParseRequiredNumberNode(value, fieldId));
                break;
            case "module.conversation.headerAvatarVariant":
                SetJsonValue(config, ["conversation", "headerAvatarVariant"], JsonValue.Create(ValidateComponentVariantReference(connection, projectId, "avatar", value))!);
                break;
            case "module.conversation.headerAvatarAlignment":
                SetJsonValue(config, ["conversation", "headerAvatarAlignment"], JsonValue.Create(value)!);
                break;
            case "module.conversation.headerLeftIconRow.editor":
                SetJsonValue(config, ["conversation", "headerLeftIconRowSlot", "variantReference"], JsonValue.Create(ValidateComponentVariantReference(connection, projectId, "iconRow", value))!);
                break;
            case "module.conversation.headerLeftIconRow.inputs":
                SetJsonValue(config, ["conversation", "headerLeftIconRowInputs"], JsonPath.ParseRequiredObject(value, fieldId));
                break;
            case "module.conversation.headerRightIconRow.editor":
                SetJsonValue(config, ["conversation", "headerRightIconRowSlot", "variantReference"], JsonValue.Create(ValidateComponentVariantReference(connection, projectId, "iconRow", value))!);
                break;
            case "module.conversation.headerRightIconRow.inputs":
                SetJsonValue(config, ["conversation", "headerRightIconRowInputs"], JsonPath.ParseRequiredObject(value, fieldId));
                break;
            case "module.conversation.showStatusBar":
                SetJsonValue(config, ["conversation", "showStatusBar"], JsonValue.Create(BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.conversation.showNavigationBar":
                SetJsonValue(config, ["conversation", "showNavigationBar"], JsonValue.Create(BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.conversation.showTextInputBar":
                SetJsonValue(config, ["conversation", "showTextInputBar"], JsonValue.Create(BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.conversation.textInputBarVariant":
                SetJsonValue(config, ["conversation", "textInputBarVariant"], JsonValue.Create(ValidateComponentVariantReference(connection, projectId, "textInputBar", value))!);
                break;
            case "module.conversation.showKeyboard":
                SetJsonValue(config, ["conversation", "showKeyboard"], JsonValue.Create(BooleanText.ParseRequired(value, fieldId))!);
                break;
            case "module.conversation.keyboardVariant":
                SetJsonValue(config, ["conversation", "keyboardVariant"], JsonValue.Create(ValidateComponentVariantReference(connection, projectId, "keyboard", value))!);
                break;
            case "module.conversation.bubbleVariant":
                SetJsonValue(config, ["conversation", "bubbleVariant"], JsonValue.Create(ValidateComponentVariantReference(connection, projectId, "bubble", value))!);
                break;
            case "module.conversation.bubbleMaxWidth":
                SetJsonValue(config, ["conversation", "bubbleMaxWidth"], JsonPath.ParseRequiredNumberNode(value, fieldId));
                break;
            case "module.conversation.screenGutter":
                SetJsonValue(config, ["conversation", "screenGutter"], JsonValue.Create(value)!);
                break;
            case "module.conversation.messageGap":
                SetJsonValue(config, ["conversation", "messageGap"], JsonValue.Create(value)!);
                break;
            case "module.conversation.messageViewportMotion":
                SetJsonValue(config, ["conversation", "messageViewportMotion"], JsonNode.Parse(MotionVariantValue.Parse(value).ToJsonString())!);
                break;
            case "module.lockScreen.statusBarVariant":
                SetJsonValue(config, ["lockScreen", "statusBarSlot", "variantReference"], JsonValue.Create(ValidateComponentVariantReference(connection, projectId, "status_bar", value))!);
                break;
            case "module.lockScreen.navigationBarVariant":
                SetJsonValue(config, ["lockScreen", "navigationBarSlot", "variantReference"], JsonValue.Create(ValidateComponentVariantReference(connection, projectId, "navigation_bar", value))!);
                break;
            case "module.lockScreen.stackVariant":
                SetJsonValue(config, ["lockScreen", "stackSlot", "variantReference"], JsonValue.Create(ValidateComponentVariantReference(connection, projectId, "componentStack", value))!);
                break;
            case "module.lockScreen.stackInputs":
                SetJsonValue(config, ["lockScreen", "stackInputs"], JsonPath.ParseRequiredObject(value, fieldId));
                break;
            case "module.lockScreen.stackItems":
                SetJsonValue(config, ["lockScreen", "stackInputs", "items"], JsonPath.ParseRequiredArray(value, fieldId));
                break;
            default:
                throw new InvalidOperationException($"Unknown module config field '{fieldId}'.");
        }

        CurrentModuleConfigContract.Validate(recordClassId, config, "Edited Module config");

    }

}
