using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.IO;
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
    string StatusBarVariantReference,
    string NavigationBarVariantReference,
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
    string ProjectId,
    string ShotId,
    string RecordClassId,
    string ConfigJson,
    string RuntimePreviewJson,
    string ComponentBaseConfigsJson,
    string AppConfigJson,
    string AnimationJson,
    int FrameRate);

internal sealed record DesignPreviewShotSlot(
    string Id,
    string Name,
    string ModuleName,
    int StartFrame,
    int EffectiveDurationFrames,
    int TransitionFrameCount,
    int ActionDelayFrames,
    int ActionDurationFrames,
    string TransitionJson);

internal sealed class DesignPreviewPayloadDataSource
{
    private readonly IPreviewInputRepository _database;
    private readonly IProjectPathResolver _projectPaths;
    private readonly IModuleInstanceTimelineStore _timeline;
    private readonly IActorPreviewRepository _actors;
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;
    private readonly ActorPreviewDataSource _actorDataSource;
    private readonly NestedRuntimeRecordReferenceResolver _nestedRuntimeRecordReferenceResolver;

    public DesignPreviewPayloadDataSource(
        IPreviewInputRepository database,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes,
        IActorPreviewRepository actors,
        IProjectPathResolver projectPaths)
    {
        _database = database;
        _timeline = timeline;
        _actors = actors;
        _projectPaths = projectPaths;
        _timelineDataSource =
            new ModuleInstanceTimelineDataSource(
                timeline,
                moduleInstanceThemes);
        _actorDataSource = new ActorPreviewDataSource(actors);
        _nestedRuntimeRecordReferenceResolver =
            new NestedRuntimeRecordReferenceResolver(
                _actorDataSource,
                projectPaths);
    }

    public void ApplyProductionMediaFallback(
        JsonObject runtime,
        JsonObject config,
        JsonObject animation,
        string projectMediaRoot)
    {
        RuntimePreviewDocumentContract.ApplyProductionMediaFallback(
            runtime,
            config,
            animation,
            (valueKind, reference) => MediaExists(
                valueKind,
                reference,
                projectMediaRoot));
    }

    private bool MediaExists(
        ValueKind valueKind,
        string reference,
        string projectMediaRoot)
    {
        if (reference.StartsWith(
                SystemPreviewFixtureCatalog.MediaScheme,
                StringComparison.Ordinal))
        {
            if (!SystemPreviewFixtureCatalog.IsMediaReference(reference))
            {
                return false;
            }
            var relative = reference[
                SystemPreviewFixtureCatalog.MediaScheme.Length..];
            var root = Path.GetFullPath(SystemPreviewFixtureCatalog.Root);
            var path = Path.GetFullPath(Path.Combine(root, relative));
            var withinRoot = Path.GetRelativePath(root, path);
            if (withinRoot.StartsWith("..", StringComparison.Ordinal)
                || Path.IsPathFullyQualified(withinRoot))
            {
                return false;
            }
            return valueKind == ValueKind.MediaDirectoryPath
                ? Directory.Exists(path)
                : File.Exists(path);
        }

        if (Uri.TryCreate(reference, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "data")
        {
            return true;
        }
        var localPath = _projectPaths.ResolveLocalPath(
            reference,
            projectMediaRoot);
        return !string.IsNullOrWhiteSpace(localPath)
            && (valueKind == ValueKind.MediaDirectoryPath
                ? Directory.Exists(localPath)
                : File.Exists(localPath));
    }

    public DesignPreviewThemeContext? LoadThemeContext(
        ProjectTreeNode node,
        string? selectedThemeId)
    {
        var themeId = ResolveThemeId(node, selectedThemeId);
        if (string.IsNullOrWhiteSpace(themeId)) return null;
        return CreateThemeContext(themeId, ResolveDeviceId(node));
    }

    public DesignPreviewThemeContext LoadProductionRenderThemeContext(
        ProjectTreeNode shot,
        string screenId,
        string themeStrategy,
        string themeId,
        string deviceId)
    {
        if (shot.Kind != ProjectTreeNodeKind.Shot)
        {
            throw new InvalidOperationException(
                "Production render context requires a Shot.");
        }
        if (!RenderThemeStrategy.IsValid(themeStrategy, themeId))
        {
            throw new InvalidOperationException(
                "Production render Theme selection is incomplete or unsupported.");
        }
        var settings = _database.GetShotSettings(shot.Id);
        var screen = _timeline.GetModuleInstanceSettings(screenId);
        if (!screen.ShotId.Equals(shot.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Render Screen '{screenId}' does not belong to Shot '{shot.Id}'.");
        }
        var effectiveThemeId = themeStrategy switch
        {
            RenderThemeStrategy.Screen => screen.ThemeId,
            RenderThemeStrategy.Forced => themeId,
            _ => throw new InvalidOperationException(
                $"Unsupported render Theme strategy '{themeStrategy}'."),
        };
        if (!_database.GetThemeOptions(settings.ProjectId)
                .Any((option) => option.Value.Equals(
                    effectiveThemeId,
                    StringComparison.Ordinal))
            || !_database.GetDeviceOptions(settings.ProjectId)
                .Any((option) => option.Value.Equals(
                    deviceId,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Render Theme and Device must belong to the Shot Project.");
        }
        return CreateThemeContext(effectiveThemeId, deviceId);
    }

    private DesignPreviewThemeContext CreateThemeContext(
        string themeId,
        string deviceId)
    {
        var theme = _database.GetThemeSettings(themeId);

        var iconTheme = !string.IsNullOrWhiteSpace(theme.IconThemeId)
            ? _database.GetIconThemeSettings(theme.IconThemeId)
            : null;
        return new DesignPreviewThemeContext(
            theme.TokensJson,
            _database.GetPaletteColorMap(theme.ProjectId),
            _database.GetPaletteNeutralMap(theme.ProjectId),
            _projectPaths.ResolveProjectPath(
                _actors.GetProjectSettings(theme.ProjectId).MediaRoot),
            iconTheme is null
                ? ""
                : _database.ResolveIconThemeAssetDirectory(theme.IconThemeId),
            iconTheme?.MappingJson ?? "{}",
            _database.GetProductionFontFaces(theme.ProjectId),
            theme.StatusBarId,
            theme.NavigationBarId,
            deviceId);
    }

    public string? ResolveThemeId(ProjectTreeNode node, string? selectedThemeId)
    {
        if (node.Kind is not ProjectTreeNodeKind.ModuleInstance and not ProjectTreeNodeKind.Shot)
        {
            return selectedThemeId;
        }

        var (_, actor) = RequiredProductionContext(node);
        var screen = node.Kind == ProjectTreeNodeKind.ModuleInstance
            ? _timeline.GetModuleInstanceSettings(node.Id)
            : null;
        var themeId = screen?.ThemeId ?? selectedThemeId;
        if (string.IsNullOrWhiteSpace(themeId))
        {
            throw new InvalidOperationException(
                $"Production Preview for Actor '{actor.DisplayName}' has no exact Screen Theme.");
        }

        _database.GetThemeSettings(themeId);
        return themeId;
    }

    public DesignPreviewComponentSource LoadComponentClass(ProjectTreeNode node)
    {
        var settings = _database.GetComponentClassSettings(node.Id);
        return ComponentSource(settings, ProjectAncestor(node).Id);
    }

    public DesignPreviewComponentSource LoadComponentVariant(ProjectTreeNode node)
    {
        var settings = _database.GetComponentVariantSettings(node);
        return ComponentSource(settings, ProjectAncestor(node).Id);
    }

    public DesignPreviewModuleSource LoadModule(ProjectTreeNode node)
    {
        var settings = _database.GetModuleSettings(node.Id);
        return ModuleSource(
            settings,
            node.Name,
            node.Id,
            ProjectAncestor(node).Id);
    }

    public DesignPreviewModuleSource LoadModuleVariant(ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var moduleId,
                out _))
        {
            throw new InvalidOperationException(
                $"Invalid Module Variant reference '{node.Id}'.");
        }
        var settings = _database.GetModuleVariantSettings(node);
        return ModuleSource(
            settings,
            node.Name,
            moduleId,
            ProjectAncestor(node).Id);
    }

    public DesignPreviewModuleInstanceSource LoadModuleInstance(string moduleInstanceId)
    {
        var instance = _timeline.GetModuleInstanceSettings(moduleInstanceId);
        var module =
            _timeline.GetModuleInstanceVariantSettings(moduleInstanceId);
        var app = _database.GetAppSettings(instance.AppId);
        var shot = _database.GetShotSettings(instance.ShotId);
        return new DesignPreviewModuleInstanceSource(
            instance.Name,
            shot.ProjectId,
            instance.ShotId,
            module.RecordClassId,
            module.ConfigJson,
            _timeline.GetModuleInstanceRuntimePreviewJson(moduleInstanceId),
            _database.GetComponentClassBaseConfigsJson(shot.ProjectId),
            app.ConfigJson,
            instance.AnimationJson,
            shot.Fps);
    }

    public IReadOnlyList<DesignPreviewShotSlot> LoadShotSlots(string shotId)
    {
        var shot = _timeline.GetTimelineShotSettings(shotId);
        var slots =
            _timeline.GetShotModuleInstanceSlots(
                shotId)
                .ToDictionary(
                    (slot) => slot.Id,
                    StringComparer.Ordinal);
        return ModuleInstanceTimeline
            .ScreenRanges(
                _timelineDataSource,
                shotId)
            .Select((range) =>
            {
                var slot =
                    slots[range.ScreenId];
                return new DesignPreviewShotSlot(
                    slot.Id,
                    slot.Name,
                    slot.ModuleName,
                    range.StartFrame,
                    range.EffectiveDurationFrames,
                    range.TransitionFrameCount,
                    range.ActionDelayFrames,
                    range.ActionDurationFrames,
                    shot.TransitionJson);
            })
            .ToList();
    }

    public ShotSettings LoadShotSettings(string shotId) =>
        _timeline.GetTimelineShotSettings(shotId);

    public string ActiveShotScreenId(string shotId, int shotFrame) =>
        ProductionScreenPlaybackState.ActiveScreenId(
            ModuleInstanceTimeline.ScreenRanges(_timelineDataSource, shotId)
                .Select((range) => new ProductionScreenFrameRange(
                    range.ScreenId,
                    range.StartFrame,
                    range.EffectiveDurationFrames))
                .ToArray(),
            shotFrame);

    public ScreenTimelineRange ModuleInstanceScreenRange(
        string moduleInstanceId) =>
        ModuleInstanceTimeline.ScreenRange(
            _timelineDataSource,
            moduleInstanceId);

    public JsonObject CreateActorPreview(
        string actorId,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors,
        bool allowSystemPreviewFixtures = false)
    {
        if (SystemPreviewFixtureCatalog.IsActor(actorId))
        {
            if (!allowSystemPreviewFixtures)
            {
                throw new InvalidOperationException(
                    $"Production Preview cannot reference System Preview Actor '{actorId}'.");
            }
            return SystemPreviewFixtureCatalog.ActorPreview(actorId);
        }
        return ActorPreviewInputFactory.Create(
            _actorDataSource,
            _projectPaths,
            actorId,
            themeMode,
            paletteColors);
    }

    public void ResolveNestedRuntimeRecordReferences(
        JsonNode? runtime,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        _nestedRuntimeRecordReferenceResolver.Resolve(
            runtime,
            themeMode,
            paletteColors,
            allowSystemPreviewFixtures: true);
    }

    private DesignPreviewComponentSource ComponentSource(
        ComponentClassSettings settings,
        string projectId)
    {
        return new DesignPreviewComponentSource(
            settings.Name,
            projectId,
            settings.ComponentType,
            _database.ValidateComponentVariantReferencesForPreview(projectId, settings.ConfigJson),
            settings.DesignPreviewJson,
            _database.GetComponentClassBaseConfigsJson(projectId));
    }

    private DesignPreviewModuleSource ModuleSource(
        ModuleSettings settings,
        string name,
        string moduleId,
        string projectId)
    {
        return new DesignPreviewModuleSource(
            name,
            projectId,
            settings.RecordClassId,
            settings.ConfigJson,
            settings.DesignPreviewJson,
            _database.GetComponentClassBaseConfigsJson(projectId),
            _database.GetModuleAppSettings(moduleId).ConfigJson);
    }

    private string ResolveDeviceId(ProjectTreeNode node)
    {
        if (node.Kind is not ProjectTreeNodeKind.ModuleInstance and not ProjectTreeNodeKind.Shot) return "";
        var (shot, actor) = RequiredProductionContext(node);
        var deviceId = shot.EffectiveDeviceId(actor.DefaultDeviceId);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new InvalidOperationException(
                $"Actor '{actor.DisplayName}' has no explicit default Device for Production Preview.");
        }

        _database.GetDeviceSettings(deviceId);
        return deviceId;
    }

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent
                ?? throw new InvalidOperationException(
                    $"{node.Kind} has no Project context.");
        }
        return current;
    }

    private (ShotSettings Shot, ActorPreviewContextSource Actor)
        RequiredProductionContext(ProjectTreeNode node)
    {
        var shotId = ShotIdFor(node);
        var shot = _database.GetShotSettings(shotId);
        if (string.IsNullOrWhiteSpace(shot.OwnerActorId))
        {
            throw new InvalidOperationException(
                $"Shot '{shotId}' has no explicit owner Actor for Production Preview.");
        }

        return (shot, _actorDataSource.LoadContext(shot.OwnerActorId));
    }

    private string ShotIdFor(ProjectTreeNode node) =>
        node.Kind == ProjectTreeNodeKind.Shot
            ? node.Id
            : _timeline.GetModuleInstanceSettings(node.Id).ShotId;
}
