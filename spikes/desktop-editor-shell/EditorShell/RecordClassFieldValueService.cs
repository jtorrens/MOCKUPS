using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RecordClassFieldValueService
{
    private readonly SpikeDatabase _database;

    public RecordClassFieldValueService(SpikeDatabase database)
    {
        _database = database;
    }

    public bool CanHandle(ProjectTreeNodeKind nodeKind, string fieldId)
    {
        return nodeKind switch
        {
            ProjectTreeNodeKind.Project => fieldId.StartsWith("project.", StringComparison.Ordinal),
            ProjectTreeNodeKind.App => fieldId.StartsWith("app.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Module => fieldId.StartsWith("module.", StringComparison.Ordinal),
            ProjectTreeNodeKind.ModuleInstance => fieldId.StartsWith("moduleInstance.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Episode => fieldId.StartsWith("episode.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Shot => fieldId.StartsWith("shot.", StringComparison.Ordinal),
            ProjectTreeNodeKind.RenderPreset => fieldId.StartsWith("renderPreset.", StringComparison.Ordinal),
            ProjectTreeNodeKind.PaletteColor => fieldId.StartsWith("palette.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Device => fieldId.StartsWith("device.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Theme => fieldId.StartsWith("theme.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Actor => fieldId.StartsWith("actor.", StringComparison.Ordinal),
            ProjectTreeNodeKind.ProductionFont => fieldId.StartsWith("font.", StringComparison.Ordinal),
            ProjectTreeNodeKind.IconTheme => fieldId.StartsWith("iconTheme.", StringComparison.Ordinal),
            _ => false,
        };
    }

    public FieldValue CreateFieldValue(ProjectTreeNode node, string fieldId)
    {
        var field = RecordClassFieldCatalog.Get(fieldId);
        var value = node.Kind switch
        {
            ProjectTreeNodeKind.Project => ProjectFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.App => AppFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Module => ModuleFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.ModuleInstance => ModuleInstanceFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Episode => EpisodeFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Shot => ShotFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.RenderPreset => RenderPresetFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.PaletteColor => PaletteColorFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Device => DeviceFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Theme => ThemeFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Actor => ActorFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.ProductionFont => ProductionFontFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.IconTheme => IconThemeFieldValue(node.Id, field.Id),
            _ => throw new InvalidOperationException($"Record class field '{fieldId}' is not supported for '{node.Kind}'."),
        };
        var options = node.Kind switch
        {
            ProjectTreeNodeKind.Theme => ThemeFieldOptions(node.Id, field),
            ProjectTreeNodeKind.Actor => ActorFieldOptions(node.Id, field),
            ProjectTreeNodeKind.App => AppFieldOptions(node.Id, field),
            ProjectTreeNodeKind.Module => ModuleFieldOptions(node.Id, field),
            ProjectTreeNodeKind.ModuleInstance => field.Options,
            ProjectTreeNodeKind.Shot => ShotFieldOptions(node.Id, field),
            ProjectTreeNodeKind.RenderPreset => RenderPresetFieldOptions(field),
            ProjectTreeNodeKind.ProductionFont => ProductionFontFieldOptions(field),
            _ => field.Options,
        };

        if (node.Kind == ProjectTreeNodeKind.Shot && field.Id == "shot.fps")
        {
            var settings = _database.GetShotSettings(node.Id);
            var inheritedValue = settings.ProjectDefaultFps.ToString();
            return new FieldValue(
                new FieldDefinition(
                    field.Id,
                    field.Label,
                    field.ValueKind,
                    IsEditable: field.IsEditable,
                    DefaultValue: inheritedValue,
                    CommitAsDefault: false,
                    CanInherit: true,
                    InheritedValue: inheritedValue,
                    Options: options,
                    PairLabels: field.PairLabels,
                    ImagePreview: field.ImagePreview,
                    Number: field.Number,
                    RecordReference: field.RecordReference),
                settings.FpsOverride?.ToString() ?? inheritedValue,
                IsInherited: settings.FpsOverride is null);
        }

        return new FieldValue(
            new FieldDefinition(
                field.Id,
                field.Label,
                field.ValueKind,
                IsEditable: field.IsEditable,
                DefaultValue: DefaultValue(node.Kind, field, value),
                CommitAsDefault: CommitAsDefault(node.Kind, field),
                Options: options,
                PairLabels: field.PairLabels,
                ImagePreview: field.ImagePreview,
                Number: field.Number,
                RecordReference: field.RecordReference),
            value);
    }

    public void CommitFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        switch (node.Kind)
        {
            case ProjectTreeNodeKind.Project when fieldId.StartsWith("project.", StringComparison.Ordinal):
                _database.UpdateProjectField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.App when fieldId.StartsWith("app.", StringComparison.Ordinal):
                _database.UpdateAppField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Module when fieldId.StartsWith("module.", StringComparison.Ordinal):
                _database.UpdateModuleField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.ModuleInstance when fieldId.StartsWith("moduleInstance.", StringComparison.Ordinal):
                _database.UpdateModuleInstanceField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Episode when fieldId.StartsWith("episode.", StringComparison.Ordinal):
                _database.UpdateEpisodeField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Shot when fieldId.StartsWith("shot.", StringComparison.Ordinal):
                _database.UpdateShotField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.RenderPreset when fieldId.StartsWith("renderPreset.", StringComparison.Ordinal):
                _database.UpdateRenderPresetField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.PaletteColor when fieldId.StartsWith("palette.", StringComparison.Ordinal):
                _database.UpdatePaletteColorField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Device when fieldId.StartsWith("device.", StringComparison.Ordinal):
                _database.UpdateDeviceField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Theme when fieldId.StartsWith("theme.", StringComparison.Ordinal):
                _database.UpdateThemeField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Actor when fieldId.StartsWith("actor.", StringComparison.Ordinal):
                _database.UpdateActorField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.ProductionFont when fieldId.StartsWith("font.", StringComparison.Ordinal):
                _database.UpdateProductionFontField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.IconTheme when fieldId.StartsWith("iconTheme.", StringComparison.Ordinal):
                return;
            default:
                throw new InvalidOperationException($"Record class field '{fieldId}' is not supported for '{node.Kind}'.");
        }
    }

    private string ProjectFieldValue(string projectId, string fieldId)
    {
        var settings = _database.GetProjectSettings(projectId);
        return fieldId switch
        {
            "project.slug" => settings.Slug,
            "project.defaultFps" => settings.DefaultFps.ToString(),
            "project.mediaRoot" => settings.MediaRoot,
            _ => throw new InvalidOperationException($"Unknown project field '{fieldId}'."),
        };
    }

    private string EpisodeFieldValue(string episodeId, string fieldId)
    {
        var settings = _database.GetEpisodeSettings(episodeId);
        return fieldId switch
        {
            "episode.slug" => settings.Slug,
            "episode.sortOrder" => settings.SortOrder.ToString(),
            _ => throw new InvalidOperationException($"Unknown episode field '{fieldId}'."),
        };
    }

    private string ModuleFieldValue(string moduleId, string fieldId)
    {
        var settings = _database.GetModuleSettings(moduleId);
        return fieldId switch
        {
            "module.recordClassId" => settings.RecordClassId,
            "module.sortOrder" => settings.SortOrder.ToString(),
            "module.metadata" => settings.MetadataJson,
            _ when fieldId.StartsWith("module.conversation.", StringComparison.Ordinal) =>
                _database.GetModuleConfigFieldValue(moduleId, fieldId),
            _ => throw new InvalidOperationException($"Unknown module field '{fieldId}'."),
        };
    }

    private string ModuleInstanceFieldValue(string moduleInstanceId, string fieldId)
    {
        var settings = _database.GetModuleInstanceSettings(moduleInstanceId);
        return fieldId switch
        {
            "moduleInstance.module" => _database.GetModuleInstanceModuleName(moduleInstanceId),
            "moduleInstance.sortOrder" => settings.SortOrder.ToString(),
            "moduleInstance.durationFrames" => settings.DurationFrames.ToString(),
            "moduleInstance.transition" => _database.GetModuleInstanceTransitionType(moduleInstanceId),
            _ => throw new InvalidOperationException($"Unknown module instance field '{fieldId}'."),
        };
    }

    private string ShotFieldValue(string shotId, string fieldId)
    {
        var settings = _database.GetShotSettings(shotId);
        return fieldId switch
        {
            "shot.slug" => settings.Slug,
            "shot.version" => settings.Version.ToString(),
            "shot.sortOrder" => settings.SortOrder.ToString(),
            "shot.durationFrames" => settings.DurationFrames.ToString(),
            "shot.fps" => settings.Fps.ToString(),
            "shot.ownerActorId" => settings.OwnerActorId,
            "shot.ownerDevice" => _database.GetShotOwnerDeviceName(shotId),
            "shot.renderPresetId" => settings.RenderPresetId,
            "shot.renderName" => _database.GetShotRenderName(shotId),
            "shot.canvas" => settings.CanvasJson,
            "shot.metadata" => settings.MetadataJson,
            _ => throw new InvalidOperationException($"Unknown shot field '{fieldId}'."),
        };
    }

    private string AppFieldValue(string appId, string fieldId)
    {
        var settings = _database.GetAppSettings(appId);
        return fieldId switch
        {
            "app.bundleKey" => settings.BundleKey,
            "app.appType" => settings.AppType,
            "app.config" => settings.ConfigJson,
            "app.metadata" => settings.MetadataJson,
            "app.wallpaper.kind" => _database.GetAppConfigFieldValue(appId, fieldId),
            "app.wallpaper.opacity" => _database.GetAppConfigFieldValue(appId, fieldId),
            "app.wallpaper.color" => _database.GetAppConfigFieldValue(appId, fieldId),
            "app.wallpaper.image.filePath" => _database.GetAppConfigFieldValue(appId, fieldId),
            "app.note" => _database.GetAppMetadataFieldValue(appId, fieldId),
            "app.icon.filePath" => _database.GetAppMetadataFieldValue(appId, fieldId),
            "app.icon.scale" => _database.GetAppMetadataFieldValue(appId, fieldId),
            "app.icon.offset" => _database.GetAppMetadataFieldValue(appId, fieldId),
            _ => throw new InvalidOperationException($"Unknown app field '{fieldId}'."),
        };
    }

    private string RenderPresetFieldValue(string renderPresetId, string fieldId)
    {
        var settings = _database.GetRenderPresetSettings(renderPresetId);
        return fieldId switch
        {
            "renderPreset.width" => settings.Width.ToString(),
            "renderPreset.height" => settings.Height.ToString(),
            "renderPreset.fps" => settings.Fps.ToString(),
            "renderPreset.format" => settings.Format,
            "renderPreset.codec" => settings.CodecJson,
            "renderPreset.color" => settings.ColorJson,
            "renderPreset.quality" => settings.QualityJson,
            "renderPreset.export" => settings.ExportJson,
            "renderPreset.export.ffmpegArgs" => ExportFfmpegArgs(settings.ExportJson),
            _ => throw new InvalidOperationException($"Unknown render preset field '{fieldId}'."),
        };
    }

    private string PaletteColorFieldValue(string colorId, string fieldId)
    {
        var settings = _database.GetPaletteColorSettings(colorId);
        return fieldId switch
        {
            "palette.token" => settings.Token,
            "palette.valueHex" => settings.ValueHex,
            "palette.isNeutral" => BoolToString(settings.IsNeutral),
            "palette.source" => settings.Source,
            "palette.protected" => BoolToString(settings.IsProtected),
            "palette.hiddenFromPickers" => BoolToString(settings.HiddenFromPickers),
            "palette.note" => settings.Note,
            _ => throw new InvalidOperationException($"Unknown palette field '{fieldId}'."),
        };
    }

    private string DeviceFieldValue(string deviceId, string fieldId)
    {
        var settings = _database.GetDeviceSettings(deviceId);
        return fieldId switch
        {
            "device.manufacturer" => settings.Manufacturer,
            "device.model" => settings.Model,
            "device.osFamily" => settings.OsFamily,
            _ => _database.GetDeviceMetricFieldValue(deviceId, fieldId),
        };
    }

    private string ActorFieldValue(string actorId, string fieldId)
    {
        return _database.GetActorFieldValue(actorId, fieldId);
    }

    private string ThemeFieldValue(string themeId, string fieldId)
    {
        return _database.GetThemeFieldValue(themeId, fieldId);
    }

    private string ProductionFontFieldValue(string fontId, string fieldId)
    {
        return _database.GetProductionFontFieldValue(fontId, fieldId);
    }

    private string IconThemeFieldValue(string iconThemeId, string fieldId)
    {
        return _database.GetIconThemeFieldValue(iconThemeId, fieldId);
    }

    private static IReadOnlyList<FieldOption>? ProductionFontFieldOptions(RecordClassFieldDescriptor field)
    {
        return field.Id == "font.category"
            ?
            [
                new FieldOption("text", "Text"),
                new FieldOption("emoji", "Emoji"),
            ]
            : field.Options;
    }

    private IReadOnlyList<FieldOption>? AppFieldOptions(string appId, RecordClassFieldDescriptor field)
    {
        return field.Id switch
        {
            "app.appType" =>
            [
                new FieldOption("chat", "Chat"),
                new FieldOption("media", "Media"),
                new FieldOption("system", "System"),
                new FieldOption("custom", "Custom"),
            ],
            "app.wallpaper.kind" =>
            [
                new FieldOption("solid", "Solid"),
                new FieldOption("image", "Image"),
            ],
            "app.wallpaper.color" => _database.GetPaletteColorOptions(_database.GetAppSettings(appId).ProjectId),
            _ => field.Options,
        };
    }

    private IReadOnlyList<FieldOption>? ShotFieldOptions(string shotId, RecordClassFieldDescriptor field)
    {
        var settings = _database.GetShotSettings(shotId);
        return field.Id switch
        {
            "shot.ownerActorId" => _database.GetActorOptions(settings.ProjectId),
            "shot.renderPresetId" => _database.GetRenderPresetOptions(settings.ProjectId),
            _ => field.Options,
        };
    }

    private IReadOnlyList<FieldOption>? ModuleFieldOptions(string moduleId, RecordClassFieldDescriptor field)
    {
        var settings = _database.GetModuleSettings(moduleId);
        if (field.ValueKind == ValueKind.ComponentPreset && !string.IsNullOrWhiteSpace(field.ComponentPresetType))
        {
            return _database.GetComponentPresetReferenceOptionsByType(settings.ProjectId, field.ComponentPresetType);
        }

        return field.Options;
    }

    private static IReadOnlyList<FieldOption>? RenderPresetFieldOptions(RecordClassFieldDescriptor field)
    {
        return field.Id switch
        {
            "renderPreset.format" =>
            [
                new FieldOption("mov", "MOV"),
                new FieldOption("image", "Image"),
            ],
            _ => field.Options,
        };
    }

    private IReadOnlyList<FieldOption>? ThemeFieldOptions(string themeId, RecordClassFieldDescriptor field)
    {
        var settings = _database.GetThemeSettings(themeId);
        return field.Id switch
        {
            "theme.family" =>
            [
                new FieldOption("ios", "iOS"),
                new FieldOption("android", "Android"),
                new FieldOption("custom", "Custom"),
            ],
            "theme.iconThemeId" => _database.GetIconThemeOptions(settings.ProjectId),
            "theme.statusBarId" => _database.GetStatusBarComponentPresetOptions(settings.ProjectId),
            "theme.navigationBarId" => _database.GetNavigationBarComponentPresetOptions(settings.ProjectId),
            "theme.defaultMode" =>
            [
                new FieldOption("light", "Light"),
                new FieldOption("dark", "Dark"),
            ],
            "theme.typography.fontFamilyId" => _database.GetProductionFontOptions(settings.ProjectId, "text"),
            "theme.typography.emojiFontFamilyId" => _database.GetProductionFontOptions(settings.ProjectId, "emoji"),
            "theme.typography.weight" =>
            [
                new FieldOption("100", "100"),
                new FieldOption("200", "200"),
                new FieldOption("300", "300"),
                new FieldOption("400", "400"),
                new FieldOption("500", "500"),
                new FieldOption("600", "600"),
                new FieldOption("700", "700"),
                new FieldOption("800", "800"),
                new FieldOption("900", "900"),
            ],
            "theme.typography.style" =>
            [
                new FieldOption("normal", "Normal"),
                new FieldOption("italic", "Italic"),
            ],
            _ => field.ValueKind is ValueKind.PaletteColorToken or ValueKind.PaletteColorPair or ValueKind.PaletteColorAlphaPair
                ? _database.GetPaletteColorOptions(settings.ProjectId)
                : field.Options,
        };
    }

    private IReadOnlyList<FieldOption>? ActorFieldOptions(string actorId, RecordClassFieldDescriptor field)
    {
        var settings = _database.GetActorSettings(actorId);
        return field.Id switch
        {
            "actor.defaultDeviceId" => _database.GetDeviceOptions(settings.ProjectId),
            "actor.defaultThemeId" => _database.GetThemeOptions(settings.ProjectId),
            _ => field.ValueKind is ValueKind.PaletteColorToken or ValueKind.PaletteColorPair or ValueKind.PaletteColorAlphaPair
                ? _database.GetPaletteColorOptions(settings.ProjectId)
                : field.Options,
        };
    }

    private static string DefaultValue(ProjectTreeNodeKind nodeKind, RecordClassFieldDescriptor field, string currentValue)
    {
        if (nodeKind != ProjectTreeNodeKind.Actor)
        {
            return currentValue;
        }

        return field.Id switch
        {
            "actor.avatar.filePath" => "",
            "actor.avatar.scale" => "1",
            "actor.avatar.offset" => "0|0",
            "actor.avatar.useInitials" => "false",
            "actor.avatar.initialsPadding" => "96",
            _ => currentValue,
        };
    }

    private static bool CommitAsDefault(ProjectTreeNodeKind nodeKind, RecordClassFieldDescriptor field)
    {
        if (nodeKind != ProjectTreeNodeKind.Actor)
        {
            return true;
        }

        return !field.Id.StartsWith("actor.avatar.", StringComparison.Ordinal)
            && field.ValueKind != ValueKind.PaletteColorPair;
    }

    private static string BoolToString(bool value) => BooleanText.Format(value);

    private static string ExportFfmpegArgs(string exportJson)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(exportJson) ? "{}" : exportJson);
            return document.RootElement.TryGetProperty("ffmpegArgs", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
        }
        catch (System.Text.Json.JsonException)
        {
            return "";
        }
    }
}
