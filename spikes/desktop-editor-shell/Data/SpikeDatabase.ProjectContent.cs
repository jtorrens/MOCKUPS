using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public ShotSettings GetShotSettings(string shotId)
    {
        using var connection = OpenConnection();
        var record = _shotRepository.Get(connection, shotId);
        var project = _projectEpisodeRepository.GetProjectSettings(connection, record.ProjectId);

        return new ShotSettings(
            record.ProjectId,
            record.Slug,
            record.Version,
            record.SortOrder,
            project.DefaultFps,
            record.FpsOverride ?? project.DefaultFps,
            record.FpsOverride,
            record.DurationFrames,
            record.OwnerActorId,
            record.RenderPresetId,
            record.CanvasJson,
            record.MetadataJson);
    }

    public void UpdateShotField(string shotId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (fieldId == "shot.fps" && value == "inherited")
        {
            _shotRepository.ClearFpsOverride(connection, shotId);
            return;
        }

        if (fieldId == "shot.ownerActorId")
        {
            _moduleInstanceThemeContextService.RequireShotOwnerChange(connection, shotId, value);
        }

        _shotRepository.UpdateField(connection, shotId, fieldId, value);
        if (fieldId == "shot.ownerActorId")
        {
            SynchronizeTimelineDurations(connection, shotId);
        }
    }

    public string GetShotRenderName(string shotId)
    {
        using var connection = OpenConnection();
        var shot = _shotRepository.Get(connection, shotId);
        var episode = _projectEpisodeRepository.QueryEpisodes(connection)
            .SingleOrDefault((candidate) => candidate.Id == shot.EpisodeId)
            ?? throw new InvalidOperationException($"Missing episode '{shot.EpisodeId}'.");
        var project = _projectEpisodeRepository.QueryProjects(connection)
            .SingleOrDefault((candidate) => candidate.Id == shot.ProjectId)
            ?? throw new InvalidOperationException($"Missing project '{shot.ProjectId}'.");
        var projectSettings = _projectEpisodeRepository.GetProjectSettings(connection, project.Id);
        var projectSlug = SlugOrName(projectSettings.Slug, project.Name, "project");
        var episodeSlug = SlugOrName(episode.Slug, episode.Name, "episode");
        var shotSlug = SlugOrName(shot.Slug, shot.Name, "shot");
        return $"{projectSlug}_{episodeSlug}_{shotSlug}_v{Math.Max(0, shot.Version):00}";
    }

    public string GetShotOwnerDeviceName(string shotId)
    {
        using var connection = OpenConnection();
        var shot = _shotRepository.Get(connection, shotId);
        var actor = _actorRepository.QueryAll(connection)
            .SingleOrDefault((candidate) => candidate.Id == shot.OwnerActorId)
            ?? throw new InvalidOperationException($"Missing Actor '{shot.OwnerActorId}'.");
        if (string.IsNullOrWhiteSpace(actor.DefaultDeviceId)) return "No default device";
        return _deviceRepository.QueryAll(connection)
            .SingleOrDefault((candidate) => candidate.Id == actor.DefaultDeviceId)?.Name
            ?? throw new InvalidOperationException($"Missing Device '{actor.DefaultDeviceId}'.");
    }

    public AppSettings GetAppSettings(string appId)
    {
        var record = _appModuleRepository.GetApp(appId);

        return new AppSettings(
            record.ProjectId,
            record.BundleKey,
            record.AppType,
            record.ConfigJson,
            record.MetadataJson);
    }

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

    public void UpdateModuleDesignPreviewJson(string moduleId, string designPreviewJson)
    {
        _appModuleRepository.UpdateModuleDesignPreview(moduleId, designPreviewJson);
    }

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

    public void UpdateModuleField(string moduleId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        var module = _appModuleRepository.GetModule(connection, moduleId);
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
                _appModuleRepository.UpdateModuleSortOrder(connection, moduleId, NumericText.Int32(value, 0));
                return;
            case "module.metadata":
                var metadata = ParseJsonObject(value);
                VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Module '{moduleId}'");
                _appModuleRepository.UpdateModuleMetadata(connection, moduleId, value);
                return;
            case "module.recordClassId":
                return;
            default:
                throw new InvalidOperationException($"Unknown module field '{fieldId}'.");
        }
    }

    public string GetModuleConfigFieldValue(string moduleId, string fieldId)
    {
        var settings = GetModuleSettings(moduleId);
        return ModuleConfigFieldValue(settings.RecordClassId, settings.ConfigJson, fieldId);
    }

    private static string ModuleConfigFieldValue(
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
                ValueKind.Boolean => BooleanText.Format(node.GetValue<bool>()),
                ValueKind.Integer or ValueKind.Decimal or ValueKind.HueDegrees or ValueKind.Alpha =>
                    node.ToJsonString(),
                ValueKind.TypographyStyle or ValueKind.TypographySystemStyle =>
                    TypographyStyleValue.Parse(node).ToJsonString(),
                ValueKind.AlignmentPlacement
                    or ValueKind.Motion
                    or ValueKind.MotionTiming
                    or ValueKind.IconTokenList
                    or ValueKind.IconSlots
                    or ValueKind.ComponentInputBindings
                    or ValueKind.ComponentVariantSlot
                    or ValueKind.StructuredCollection
                    or ValueKind.BehaviorTiming => node.ToJsonString(),
                _ => node.GetValue<string>(),
            };
        }
        var conversation = fieldId.StartsWith("module.conversation.", StringComparison.Ordinal)
            ? JsonPath.RequiredObject(config, "conversation", "Module config")
            : null;
        var lockScreen = fieldId.StartsWith("module.lockScreen.", StringComparison.Ordinal)
            ? JsonPath.RequiredObject(config, "lockScreen", "Module config")
            : null;
        return fieldId switch
        {
            "module.appearanceMode" => ModuleAppearanceModeContract.Read(config, "Module Variant config"),
            "module.conversation.showHeader" => BooleanText.Format(JsonPath.RequiredBoolean(conversation!, "showHeader", "Module config.conversation")),
            "module.conversation.useAppWallpaper" => BooleanText.Format(JsonPath.RequiredBoolean(conversation!, "useAppWallpaper", "Module config.conversation")),
            "module.conversation.headerHeight" => RequiredNumberString(conversation!, "headerHeight"),
            "module.conversation.headerAvatarVariant" => JsonPath.RequiredString(conversation!, "headerAvatarVariant", "Module config.conversation"),
            "module.conversation.headerAvatarAlignment" => JsonPath.RequiredString(conversation!, "headerAvatarAlignment", "Module config.conversation"),
            "module.conversation.headerLeftIconRow.editor" => RequiredSlotReference(conversation!, "headerLeftIconRowSlot"),
            "module.conversation.headerLeftIconRow.inputs" => JsonPath.RequiredObject(conversation!, "headerLeftIconRowInputs", "Module config.conversation").ToJsonString(),
            "module.conversation.headerRightIconRow.editor" => RequiredSlotReference(conversation!, "headerRightIconRowSlot"),
            "module.conversation.headerRightIconRow.inputs" => JsonPath.RequiredObject(conversation!, "headerRightIconRowInputs", "Module config.conversation").ToJsonString(),
            "module.conversation.showStatusBar" => BooleanText.Format(JsonPath.RequiredBoolean(conversation!, "showStatusBar", "Module config.conversation")),
            "module.conversation.showNavigationBar" => BooleanText.Format(JsonPath.RequiredBoolean(conversation!, "showNavigationBar", "Module config.conversation")),
            "module.conversation.showTextInputBar" => BooleanText.Format(JsonPath.RequiredBoolean(conversation!, "showTextInputBar", "Module config.conversation")),
            "module.conversation.textInputBarVariant" => JsonPath.RequiredString(conversation!, "textInputBarVariant", "Module config.conversation"),
            "module.conversation.showKeyboard" => BooleanText.Format(JsonPath.RequiredBoolean(conversation!, "showKeyboard", "Module config.conversation")),
            "module.conversation.keyboardVariant" => JsonPath.RequiredString(conversation!, "keyboardVariant", "Module config.conversation"),
            "module.conversation.bubbleVariant" => JsonPath.RequiredString(conversation!, "bubbleVariant", "Module config.conversation"),
            "module.conversation.bubbleMaxWidth" => RequiredNumberString(conversation!, "bubbleMaxWidth"),
            "module.conversation.screenGutter" => JsonPath.RequiredString(conversation!, "screenGutter", "Module config.conversation"),
            "module.conversation.messageGap" => JsonPath.RequiredString(conversation!, "messageGap", "Module config.conversation"),
            "module.conversation.messageViewportMotion" => conversation!["messageViewportMotion"]?.ToJsonString()
                ?? (MotionVariantValue.Default with { Bounds = MotionVariantValue.Parent }).ToJsonString(),
            "module.lockScreen.statusBarVariant" => RequiredSlotReference(lockScreen!, "statusBarSlot"),
            "module.lockScreen.navigationBarVariant" => RequiredSlotReference(lockScreen!, "navigationBarSlot"),
            "module.lockScreen.stackVariant" => RequiredSlotReference(lockScreen!, "stackSlot"),
            "module.lockScreen.stackInputs" => JsonPath.RequiredObject(lockScreen!, "stackInputs", "Module config.lockScreen").ToJsonString(),
            "module.lockScreen.stackItems" => JsonPath.RequiredArray(
                JsonPath.RequiredObject(lockScreen!, "stackInputs", "Module config.lockScreen"),
                "items",
                "Module config.lockScreen.stackInputs").ToJsonString(),
            _ => throw new InvalidOperationException($"Unknown module config field '{fieldId}'."),
        };
    }

    private static string RequiredSlotReference(JsonObject owner, string key)
    {
        var slot = JsonPath.RequiredObject(owner, key, "Module config");
        return JsonPath.RequiredString(slot, "variantReference", $"Module config.{key}");
    }

    private static string RequiredNumberString(JsonObject owner, string key) =>
        JsonPath.RequiredNumber(owner, key, "Module config").ToString(CultureInfo.InvariantCulture);

    public void UpdateAppField(string appId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (fieldId.StartsWith("app.wallpaper.", StringComparison.Ordinal))
        {
            if (_appModuleRepository.GetApp(connection, appId).AppType == "system")
            {
                throw new InvalidOperationException("System apps inherit Actor wallpaper and cannot own wallpaper fields.");
            }
            UpdateAppConfigField(connection, appId, fieldId, value);
            return;
        }

        if (fieldId.StartsWith("app.icon.", StringComparison.Ordinal) || fieldId == "app.note")
        {
            UpdateAppMetadataField(connection, appId, fieldId, value);
            return;
        }

        _appModuleRepository.UpdateAppDirectField(connection, appId, fieldId, value);
    }

    public string GetAppConfigFieldValue(string appId, string fieldId)
    {
        var settings = GetAppSettings(appId);
        var config = ParseJsonObject(settings.ConfigJson);
        var context = $"App '{appId}' config_json";
        var lightWallpaperColor = JsonString(config, ["modes", "light", "wallpaper", "color"]);
        var darkWallpaperColor = JsonString(config, ["modes", "dark", "wallpaper", "color"]);
        return fieldId switch
        {
            "app.wallpaper.kind" => JsonString(config, ["wallpaper", "kind"]),
            "app.wallpaper.opacity" => JsonPath.RequiredNumberString(config, ["wallpaper", "opacity"], context),
            "app.wallpaper.color" => $"{lightWallpaperColor}|{darkWallpaperColor}",
            "app.wallpaper.images.light.filePath" => JsonString(config, ["wallpaper", "images", "light", "filePath"]),
            "app.wallpaper.images.dark.filePath" => JsonString(config, ["wallpaper", "images", "dark", "filePath"]),
            _ => throw new InvalidOperationException($"Unknown app config field '{fieldId}'."),
        };
    }

    public string GetAppMetadataFieldValue(string appId, string fieldId)
    {
        var settings = GetAppSettings(appId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        var context = $"App '{appId}' metadata_json";
        return fieldId switch
        {
            "app.note" => JsonString(metadata, ["note"]),
            "app.icon.filePath" => JsonString(metadata, ["icon", "filePath"]),
            "app.icon.scale" => JsonPath.RequiredNumberString(metadata, ["icon", "scale"], context),
            "app.icon.offset" => JsonPath.RequiredNumberPair(metadata, ["icon", "offsetX"], ["icon", "offsetY"], context),
            _ => throw new InvalidOperationException($"Unknown app metadata field '{fieldId}'."),
        };
    }

    private void UpdateAppConfigField(SqliteConnection connection, string appId, string fieldId, string value)
    {
        var config = ParseJsonObject(_appModuleRepository.GetApp(connection, appId).ConfigJson);
        switch (fieldId)
        {
            case "app.wallpaper.kind":
                SetJsonValue(config, ["wallpaper", "kind"], JsonValue.Create(value)!);
                break;
            case "app.wallpaper.opacity":
                SetJsonValue(config, ["wallpaper", "opacity"], NumberNode(value));
                break;
            case "app.wallpaper.color":
                SetPair(
                    config,
                    value,
                    ["modes", "light", "wallpaper", "color"],
                    ["modes", "dark", "wallpaper", "color"],
                    asNumber: false);
                break;
            case "app.wallpaper.images.light.filePath":
                SetJsonValue(config, ["wallpaper", "images", "light", "filePath"], JsonValue.Create(value)!);
                break;
            case "app.wallpaper.images.dark.filePath":
                SetJsonValue(config, ["wallpaper", "images", "dark", "filePath"], JsonValue.Create(value)!);
                break;
            default:
                throw new InvalidOperationException($"Unknown app config field '{fieldId}'.");
        }

        _appModuleRepository.UpdateAppConfig(connection, appId, config.ToJsonString());
    }

    private void UpdateAppMetadataField(SqliteConnection connection, string appId, string fieldId, string value)
    {
        var metadata = ParseJsonObject(_appModuleRepository.GetApp(connection, appId).MetadataJson);
        switch (fieldId)
        {
            case "app.note":
                SetJsonValue(metadata, ["note"], JsonValue.Create(value)!);
                break;
            case "app.icon.filePath":
                SetJsonValue(metadata, ["icon", "filePath"], JsonValue.Create(value)!);
                break;
            case "app.icon.scale":
                SetJsonValue(metadata, ["icon", "scale"], NumberNode(value));
                break;
            case "app.icon.offset":
                SetPair(metadata, value, ["icon", "offsetX"], ["icon", "offsetY"]);
                break;
            default:
                throw new InvalidOperationException($"Unknown app metadata field '{fieldId}'.");
        }

        _appModuleRepository.UpdateAppMetadata(connection, appId, metadata.ToJsonString());
    }

    private void UpdateModuleConfigField(SqliteConnection connection, string moduleId, string fieldId, string value)
    {
        var module = _appModuleRepository.GetModule(connection, moduleId);
        var config = ParseJsonObject(module.ConfigJson);

        UpdateModuleConfigFieldValue(connection, module.ProjectId, module.RecordClassId, config, fieldId, value);
        _appModuleRepository.UpdateModuleConfig(connection, moduleId, config.ToJsonString());
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
