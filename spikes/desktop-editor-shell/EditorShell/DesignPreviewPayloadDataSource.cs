using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record DesignPreviewThemeContext(
    string TokensJson,
    IReadOnlyDictionary<string, string> PaletteColors,
    IReadOnlyDictionary<string, bool> PaletteNeutralColors,
    string ProjectMediaRoot,
    string IconAssetRoot,
    string IconMappingJson,
    IReadOnlyList<ProductionFontFace> FontFaces,
    string StatusBarPresetId,
    string NavigationBarPresetId,
    string DeviceId);

internal sealed record DesignPreviewComponentSource(
    string Name,
    string ProjectId,
    string ComponentType,
    string ConfigJson,
    string DesignPreviewJson,
    string ComponentBaseConfigsJson);

internal sealed record DesignPreviewModuleSource(
    string Name,
    string ProjectId,
    string RecordClassId,
    string ConfigJson,
    string DesignPreviewJson,
    string ComponentBaseConfigsJson,
    string AppConfigJson);

internal sealed record DesignPreviewModuleInstanceSource(
    string Name,
    string ShotId,
    string RecordClassId,
    string ConfigJson,
    string RuntimePreviewJson,
    string ComponentBaseConfigsJson,
    string AppConfigJson,
    string AnimationJson,
    string OwnerActorId,
    int FrameRate);

internal sealed record DesignPreviewShotSlot(
    string Id,
    string Name,
    string ModuleName,
    int DurationFrames);

internal sealed class DesignPreviewPayloadDataSource
{
    private readonly SpikeDatabase _database;
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;
    private readonly ActorPreviewDataSource _actorDataSource;

    public DesignPreviewPayloadDataSource(SpikeDatabase database)
    {
        _database = database;
        _timelineDataSource = new ModuleInstanceTimelineDataSource(database);
        _actorDataSource = new ActorPreviewDataSource(database);
    }

    public DesignPreviewThemeContext? LoadThemeContext(
        ProjectTreeNode node,
        string? selectedThemeId)
    {
        var themeId = ResolveThemeId(node, selectedThemeId);
        if (string.IsNullOrWhiteSpace(themeId)) return null;

        SpikeDatabase.ThemeSettings theme;
        try
        {
            theme = _database.GetThemeSettings(themeId);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var iconTheme = !string.IsNullOrWhiteSpace(theme.IconThemeId)
            ? _database.GetIconThemeSettings(theme.IconThemeId)
            : null;
        return new DesignPreviewThemeContext(
            theme.TokensJson,
            _database.GetPaletteColorMap(theme.ProjectId),
            _database.GetPaletteNeutralMap(theme.ProjectId),
            ProjectPathService.ResolveProjectPath(_database.GetProjectSettings(theme.ProjectId).MediaRoot),
            iconTheme?.AssetRoot ?? "",
            iconTheme?.MappingJson ?? "{}",
            _database.GetProductionFontFaces(theme.ProjectId),
            theme.StatusBarId,
            theme.NavigationBarId,
            ResolveDeviceId(node));
    }

    public string? ResolveThemeId(ProjectTreeNode node, string? selectedThemeId)
    {
        if (node.Kind is not ProjectTreeNodeKind.ModuleInstance and not ProjectTreeNodeKind.Shot)
        {
            return selectedThemeId;
        }

        var shot = ShotFor(node);
        if (string.IsNullOrWhiteSpace(shot.OwnerActorId)) return selectedThemeId;

        var actor = _actorDataSource.LoadContext(shot.OwnerActorId);
        return string.IsNullOrWhiteSpace(actor.DefaultThemeId)
            ? selectedThemeId
            : actor.DefaultThemeId;
    }

    public DesignPreviewComponentSource LoadComponentClass(ProjectTreeNode node)
    {
        var settings = _database.GetComponentClassSettings(node.Id);
        return ComponentSource(settings);
    }

    public DesignPreviewComponentSource LoadComponentVariant(ProjectTreeNode node)
    {
        var settings = _database.GetComponentPresetSettings(node);
        return ComponentSource(settings);
    }

    public DesignPreviewModuleSource LoadModule(ProjectTreeNode node)
    {
        var settings = _database.GetModuleSettings(node.Id);
        return ModuleSource(settings, node.Name, node.Id);
    }

    public DesignPreviewModuleSource LoadModuleVariant(ProjectTreeNode node)
    {
        var settings = _database.GetModuleVariantSettings(node);
        var moduleId = node.Parent?.Id
            ?? throw new InvalidOperationException("Module variant has no parent module.");
        return ModuleSource(settings, node.Name, moduleId);
    }

    public DesignPreviewModuleInstanceSource LoadModuleInstance(string moduleInstanceId)
    {
        var instance = _database.GetModuleInstanceSettings(moduleInstanceId);
        var module = _database.GetModuleInstanceVariantSettings(moduleInstanceId);
        var app = _database.GetAppSettings(instance.AppId);
        var shot = _database.GetShotSettings(instance.ShotId);
        return new DesignPreviewModuleInstanceSource(
            instance.Name,
            instance.ShotId,
            module.RecordClassId,
            module.ConfigJson,
            _database.GetModuleInstanceRuntimePreviewJson(moduleInstanceId),
            _database.GetComponentClassBaseConfigsJson(module.ProjectId),
            app.ConfigJson,
            instance.AnimationJson,
            shot.OwnerActorId,
            shot.Fps);
    }

    public IReadOnlyList<DesignPreviewShotSlot> LoadShotSlots(string shotId)
    {
        return _database.GetShotModuleInstanceSlots(shotId)
            .Select((slot) => new DesignPreviewShotSlot(
                slot.Id,
                slot.Name,
                slot.ModuleName,
                ModuleInstanceTimeline.DurationFrames(_timelineDataSource, slot.Id)))
            .ToList();
    }

    public int ModuleInstanceLocalFrame(string moduleInstanceId, int shotFrame)
    {
        var instance = _database.GetModuleInstanceSettings(moduleInstanceId);
        var startFrame = 0;
        foreach (var slot in LoadShotSlots(instance.ShotId))
        {
            if (slot.Id == moduleInstanceId) return Math.Max(0, shotFrame - startFrame);
            startFrame += slot.DurationFrames;
        }
        return 0;
    }

    public JsonObject CreateActorPreview(
        string actorId,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        return ActorPreviewInputFactory.Create(_actorDataSource, actorId, themeMode, paletteColors);
    }

    private DesignPreviewComponentSource ComponentSource(SpikeDatabase.ComponentClassSettings settings)
    {
        return new DesignPreviewComponentSource(
            settings.Name,
            settings.ProjectId,
            settings.ComponentType,
            _database.ValidateComponentPresetReferencesForPreview(settings.ProjectId, settings.ConfigJson),
            settings.DesignPreviewJson,
            _database.GetComponentClassBaseConfigsJson(settings.ProjectId));
    }

    private DesignPreviewModuleSource ModuleSource(
        SpikeDatabase.ModuleSettings settings,
        string name,
        string moduleId)
    {
        return new DesignPreviewModuleSource(
            name,
            settings.ProjectId,
            settings.RecordClassId,
            settings.ConfigJson,
            settings.DesignPreviewJson,
            _database.GetComponentClassBaseConfigsJson(settings.ProjectId),
            _database.GetModuleAppSettings(moduleId).ConfigJson);
    }

    private string ResolveDeviceId(ProjectTreeNode node)
    {
        if (node.Kind is not ProjectTreeNodeKind.ModuleInstance and not ProjectTreeNodeKind.Shot) return "";
        var shot = ShotFor(node);
        if (string.IsNullOrWhiteSpace(shot.OwnerActorId)) return "";
        return _actorDataSource.LoadContext(shot.OwnerActorId).DefaultDeviceId;
    }

    private SpikeDatabase.ShotSettings ShotFor(ProjectTreeNode node)
    {
        return node.Kind == ProjectTreeNodeKind.Shot
            ? _database.GetShotSettings(node.Id)
            : _database.GetShotSettings(_database.GetModuleInstanceSettings(node.Id).ShotId);
    }
}
