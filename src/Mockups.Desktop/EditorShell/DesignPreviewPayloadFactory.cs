using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ScreenTransitionPayload(
    DesignPreviewPayload Outgoing,
    DesignPreviewPayload Incoming,
    string OutgoingMotionJson,
    string IncomingMotionJson,
    double ElapsedMilliseconds,
    int DurationFrames);

internal sealed record ScreenTimingPayload(
    int ScreenFrame,
    int TransitionFrameCount,
    int ActionDelayFrames,
    int ActionDurationFrames)
{
    public int ActionStartFrame =>
        TransitionFrameCount
        + ActionDelayFrames;
}

internal sealed record DesignPreviewPayload(
    string Kind,
    string Name,
    string ConfigJson,
    string ThemeTokensJson,
    IReadOnlyDictionary<string, string> PaletteColors,
    IReadOnlyDictionary<string, bool> PaletteNeutralColors,
    string ProjectMediaRoot,
    IReadOnlyList<string> ProjectMediaFiles,
    string IconAssetRoot,
    string IconMappingJson,
    IReadOnlyList<ProductionFontFace> FontFaces,
    string ComponentType,
    string DesignPreviewJson,
    string RuntimeContractJson,
    string ThemeMode,
    string ComponentBaseConfigsJson = "{}",
    string AppConfigJson = "{}",
    string InstanceJson = "{}",
    string DeviceId = "",
    int FrameRate = 25,
    string ThemeStatusBarVariantReference = "",
    string ThemeNavigationBarVariantReference = "",
    int LocalFrame = 0,
    string OwnerId = "",
    ScreenTimingPayload? ScreenTiming = null,
    ScreenTransitionPayload? ScreenTransition = null,
    string RuntimeRecordReferencesJson = "{}",
    string ProjectId = "");

internal static class DesignPreviewPayloadFactory
{
    public static DesignPreviewPayload? Create(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode? node,
        string? themeId,
        string themeMode = "light",
        int timelineFrame = 0)
    {
        if (node is null)
        {
            return null;
        }

        var contextNode = node.Kind == ProjectTreeNodeKind.Shot
            ? dataSource.ActiveShotScreenId(node.Id, timelineFrame) is { Length: > 0 } screenId
                ? new ProjectTreeNode(
                    ProjectTreeNodeKind.ModuleInstance,
                    screenId,
                    screenId,
                    "",
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance))
                : null
            : node;
        if (contextNode is null) return null;
        var theme = dataSource.LoadThemeContext(contextNode, themeId);
        if (theme is null) return null;
        var payload = node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => FromComponentSource(dataSource, dataSource.LoadComponentClass(node), themeMode, theme),
            ProjectTreeNodeKind.ComponentVariant => FromComponentSource(dataSource, dataSource.LoadComponentVariant(node), themeMode, theme),
            ProjectTreeNodeKind.Module => FromModuleSource(dataSource, dataSource.LoadModule(node), themeMode, theme),
            ProjectTreeNodeKind.ModuleVariant => FromModuleSource(dataSource, dataSource.LoadModuleVariant(node), themeMode, theme),
            ProjectTreeNodeKind.ModuleInstance =>
                FromModuleInstanceAtShotFrame(
                    dataSource,
                    node.Id,
                    theme.DeviceId,
                    themeMode,
                    theme,
                    timelineFrame,
                    respectAuthoredAppearance: false),
            ProjectTreeNodeKind.Shot => FromShot(
                dataSource,
                node,
                theme.DeviceId,
                themeMode,
                theme,
                timelineFrame,
                respectAuthoredAppearance: false),
            _ => null,
        };
        return payload is null
            ? null
            : payload with
            {
                OwnerId = node.Id,
                ThemeStatusBarVariantReference = theme.StatusBarVariantReference,
                ThemeNavigationBarVariantReference = theme.NavigationBarVariantReference,
                LocalFrame = node.Kind is ProjectTreeNodeKind.ModuleInstance or ProjectTreeNodeKind.Shot
                    ? payload.LocalFrame
                    : Math.Max(0, timelineFrame),
            };
    }

    public static DesignPreviewPayload? CreateProductionRender(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode shot,
        string themeId,
        string deviceId,
        string requestedThemeMode,
        int shotFrame)
    {
        if (shot.Kind != ProjectTreeNodeKind.Shot)
        {
            throw new InvalidOperationException(
                "A Production render payload requires a Shot.");
        }
        var screenId = dataSource.ActiveShotScreenId(
            shot.Id,
            shotFrame);
        if (screenId is null) return null;
        var theme = dataSource.LoadProductionRenderThemeContext(
            shot,
            screenId,
            themeId,
            deviceId);
        var payload = FromShot(
            dataSource,
            shot,
            deviceId,
            ModuleAppearanceModeContract.RequireResolved(
                requestedThemeMode,
                $"Shot '{shot.Id}' render appearance"),
            theme,
            shotFrame,
            respectAuthoredAppearance: true)
            ?? throw new InvalidOperationException(
                $"Shot '{shot.Name}' did not resolve its active Screen '{screenId}'.");
        return payload with
        {
            OwnerId = shot.Id,
            ThemeStatusBarVariantReference =
                theme.StatusBarVariantReference,
            ThemeNavigationBarVariantReference =
                theme.NavigationBarVariantReference,
        };
    }

    private static DesignPreviewPayload FromModuleInstance(
        DesignPreviewPayloadDataSource dataSource,
        string moduleInstanceId,
        string deviceId,
        string themeMode,
        DesignPreviewThemeContext theme,
        int? screenFrame,
        bool respectAuthoredAppearance)
    {
        var instance = dataSource.LoadModuleInstance(moduleInstanceId);
        var effectiveThemeMode = ResolveEffectiveThemeMode(
            instance.ConfigJson,
            themeMode,
            $"Module Instance '{moduleInstanceId}' Variant config",
            respectAuthoredAppearance);
        var runtimePreview = DesignPreviewTestValues.Parse(DesignPreviewTestValues.RuntimeJson(
            instance.RuntimePreviewJson));
        if (screenFrame is not null
            && runtimePreview["timelineFrameJsonKey"]?.GetValue<string>() is { Length: > 0 } timelineFrameJsonKey)
        {
            runtimePreview[timelineFrameJsonKey] = Math.Max(0, screenFrame.Value);
        }
        var runtimeContractJson = runtimePreview.ToJsonString();
        var runtimeActorId = runtimePreview["actorId"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(runtimeActorId))
        {
            runtimePreview["actor"] = dataSource.CreateActorPreview(
                runtimeActorId,
                effectiveThemeMode,
                theme.PaletteColors);
        }
        ModuleRuntimeDocumentContracts.PrepareProduction(
            instance.RecordClassId,
            $"Module Instance '{moduleInstanceId}' Production payload",
            runtimePreview);
        var instanceJson = new JsonObject
        {
            ["animation"] = JsonPath.ParseRequiredObject(
                instance.AnimationJson,
                $"Module Instance '{moduleInstanceId}' animation_json"),
            ["context"] = new JsonObject
            {
                ["shotId"] = instance.ShotId,
                ["moduleInstanceId"] = moduleInstanceId,
                ["screenFrame"] = Math.Max(0, screenFrame ?? 0),
            },
        };
        var runtimePreviewJson = runtimePreview.ToJsonString();
        return new DesignPreviewPayload(
            "moduleInstance",
            instance.Name,
            instance.ConfigJson,
            theme.TokensJson,
            theme.PaletteColors,
            theme.PaletteNeutralColors,
            theme.ProjectMediaRoot,
            PreviewMediaDirectoryCatalog.Resolve(theme.ProjectMediaRoot, runtimePreviewJson),
            theme.IconAssetRoot,
            theme.IconMappingJson,
            theme.FontFaces,
            instance.RecordClassId,
            runtimePreviewJson,
            runtimeContractJson,
            effectiveThemeMode,
            instance.ComponentBaseConfigsJson,
            instance.AppConfigJson,
            instanceJson.ToJsonString(),
            deviceId,
            instance.FrameRate,
            LocalFrame: Math.Max(0, screenFrame ?? 0),
            ProjectId: instance.ProjectId);
    }

    private static DesignPreviewPayload
        FromModuleInstanceAtShotFrame(
            DesignPreviewPayloadDataSource dataSource,
            string moduleInstanceId,
            string deviceId,
            string themeMode,
            DesignPreviewThemeContext theme,
            int shotFrame,
            bool respectAuthoredAppearance)
    {
        var range =
            dataSource.ModuleInstanceScreenRange(
                moduleInstanceId);
        var screenFrame =
            Math.Clamp(
                shotFrame
                - range.StartFrame,
                0,
                range.EffectiveDurationFrames - 1);
        var actionFrame =
            Math.Clamp(
                screenFrame
                - range.ActionStartFrame,
                0,
                range.ActionDurationFrames - 1);
        return FromModuleInstance(
            dataSource,
            moduleInstanceId,
            deviceId,
            themeMode,
            theme,
            actionFrame,
            respectAuthoredAppearance)
            with
            {
                ScreenTiming =
                    new ScreenTimingPayload(
                        screenFrame,
                        range.TransitionFrameCount,
                        range.ActionDelayFrames,
                        range.ActionDurationFrames),
            };
    }

    private static DesignPreviewPayload? FromShot(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode shotNode,
        string deviceId,
        string themeMode,
        DesignPreviewThemeContext theme,
        int shotFrame,
        bool respectAuthoredAppearance)
    {
        var slots = dataSource.LoadShotSlots(shotNode.Id);
        if (slots.Count == 0) return null;
        var active = slots.FirstOrDefault((slot) =>
            shotFrame >= slot.StartFrame
            && shotFrame < slot.StartFrame + slot.EffectiveDurationFrames);
        if (active is null) return null;
        var screenFrame =
            shotFrame - active.StartFrame;
        var actionStartFrame =
            active.TransitionFrameCount
            + active.ActionDelayFrames;
        var actionFrame =
            Math.Clamp(
                screenFrame
                - actionStartFrame,
                0,
                active.ActionDurationFrames - 1);
        var incoming = FromModuleInstance(
            dataSource,
            active.Id,
            deviceId,
            themeMode,
            theme,
            actionFrame,
            respectAuthoredAppearance);
        var shotPreview = DesignPreviewTestValues.Parse(incoming.DesignPreviewJson);
        shotPreview.Remove("actions");
        incoming = incoming with
        {
            Name = active.Name,
            DesignPreviewJson = shotPreview.ToJsonString(),
            ThemeStatusBarVariantReference =
                theme.StatusBarVariantReference,
            ThemeNavigationBarVariantReference =
                theme.NavigationBarVariantReference,
            ScreenTiming =
                new ScreenTimingPayload(
                    screenFrame,
                    active.TransitionFrameCount,
                    active.ActionDelayFrames,
                    active.ActionDurationFrames),
        };
        return incoming;
    }

    private static DesignPreviewPayload FromModuleSource(
        DesignPreviewPayloadDataSource dataSource,
        DesignPreviewModuleSource settings,
        string themeMode,
        DesignPreviewThemeContext theme)
    {
        var effectiveThemeMode = ResolveEffectiveThemeMode(
            settings.ConfigJson,
            themeMode,
            $"Module '{settings.RecordClassId}' Variant config",
            respectAuthoredAppearance: false);
        var config = DesignPreviewTestValues.Parse(settings.ConfigJson);
        var effectivePreview = EffectiveRuntimeContract(
            DesignPreviewTestValues.Parse(settings.DesignPreviewJson),
            config,
            ComponentVariantConfigResolver(settings.ComponentBaseConfigsJson));
        var runtimeContractJson = DesignPreviewTestValues.RuntimeJson(effectivePreview.ToJsonString());
        var runtimePreview = DesignPreviewTestValues.Parse(runtimeContractJson);
        var actorId = runtimePreview["actorId"]?.GetValue<string>() ?? "";
        runtimePreview["actor"] = string.IsNullOrWhiteSpace(actorId)
            ? ActorPreviewInputFactory.CreateSample()
            : dataSource.CreateActorPreview(actorId, effectiveThemeMode, theme.PaletteColors);
        dataSource.ResolveNestedRuntimeRecordReferences(
            runtimePreview,
            effectiveThemeMode,
            theme.PaletteColors);
        var runtimePreviewJson = runtimePreview.ToJsonString();
        return new DesignPreviewPayload(
            "module",
            settings.Name,
            settings.ConfigJson,
            theme.TokensJson,
            theme.PaletteColors,
            theme.PaletteNeutralColors,
            theme.ProjectMediaRoot,
            PreviewMediaDirectoryCatalog.Resolve(theme.ProjectMediaRoot, runtimePreviewJson),
            theme.IconAssetRoot,
            theme.IconMappingJson,
            theme.FontFaces,
            settings.RecordClassId,
            runtimePreviewJson,
            runtimeContractJson,
            effectiveThemeMode,
            settings.ComponentBaseConfigsJson,
            settings.AppConfigJson,
            ProjectId: settings.ProjectId);
    }

    private static string ResolveEffectiveThemeMode(
        string configJson,
        string selectedThemeMode,
        string owner,
        bool respectAuthoredAppearance)
    {
        var config = JsonPath.ParseRequiredObject(configJson, owner);
        return respectAuthoredAppearance
            ? ModuleAppearanceModeContract.Resolve(config, selectedThemeMode, owner)
            : ModuleAppearanceModeContract.RequireResolved(
                selectedThemeMode,
                $"{owner} Preview Theme mode");
    }

    private static DesignPreviewPayload FromComponentSource(
        DesignPreviewPayloadDataSource dataSource,
        DesignPreviewComponentSource settings,
        string themeMode,
        DesignPreviewThemeContext theme)
    {
        var effectiveThemeMode = ModuleAppearanceModeContract.RequireResolved(
            themeMode,
            $"Component '{settings.ComponentType}' Preview Theme mode");
        var configJson = settings.ConfigJson;
        var effectivePreview = EffectiveRuntimeContract(
            DesignPreviewTestValues.Parse(settings.DesignPreviewJson),
            DesignPreviewTestValues.Parse(configJson),
            ComponentVariantConfigResolver(settings.ComponentBaseConfigsJson));
        var runtimeContractJson = ResolveActionDurationsJson(
            configJson,
            theme.TokensJson,
            DesignPreviewTestValues.RuntimeJson(effectivePreview.ToJsonString()));
        var runtimePreview = DesignPreviewTestValues.Parse(runtimeContractJson);
        dataSource.ResolveNestedRuntimeRecordReferences(
            runtimePreview,
            effectiveThemeMode,
            theme.PaletteColors);
        var designPreviewJson = runtimePreview.ToJsonString();
        return new DesignPreviewPayload(
            "componentClass",
            settings.Name,
            configJson,
            theme.TokensJson,
            theme.PaletteColors,
            theme.PaletteNeutralColors,
            theme.ProjectMediaRoot,
            PreviewMediaDirectoryCatalog.Resolve(theme.ProjectMediaRoot, designPreviewJson),
            theme.IconAssetRoot,
            theme.IconMappingJson,
            theme.FontFaces,
            settings.ComponentType,
            designPreviewJson,
            runtimeContractJson,
            effectiveThemeMode,
            settings.ComponentBaseConfigsJson,
            ProjectId: settings.ProjectId);
    }

    private static string ResolveActionDurationsJson(
        string configJson,
        string themeTokensJson,
        string designPreviewJson)
    {
        var preview = JsonPath.ParseRequiredObject(designPreviewJson, "Design Preview contract");
        var changed = false;
        var config = JsonPath.ParseRequiredObject(configJson, "Preview owner config");
        var themeTokens = JsonPath.ParseRequiredObject(themeTokensJson, "Theme tokens");

        if (preview["actions"] is JsonArray actions)
        {
            for (var index = 0; index < actions.Count; index++)
            {
                var action = actions[index] as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Design Preview action at index {index} must be an object.");
                changed |= ResolveActionDuration(config, themeTokens, action);
            }
        }

        return changed ? preview.ToJsonString() : designPreviewJson;
    }

    private static JsonObject EffectiveRuntimeContract(
        JsonObject preview,
        JsonObject config,
        Func<string, JsonObject> componentVariantConfig)
    {
        return RuntimePreviewDocumentContract.PrepareFixture(
            preview,
            config,
            componentVariantConfig);
    }

    private static Func<string, JsonObject> ComponentVariantConfigResolver(
        string componentBaseConfigsJson)
    {
        var catalog = JsonPath.ParseRequiredObject(
            componentBaseConfigsJson,
            "Component Variant config catalog");
        var variants = JsonPath.RequiredObject(
            catalog,
            "variants",
            "Component Variant config catalog");
        return variantReference => variants[variantReference] is JsonObject config
            ? config.DeepClone().AsObject()
            : throw new InvalidOperationException(
                $"Component Variant config catalog is missing '{variantReference}'.");
    }

    private static bool ResolveActionDuration(JsonObject config, JsonObject themeTokens, JsonObject action)
    {
        if (action["durationMotionConfigPath"] is null) return false;
        var motionConfigPath = JsonPath.RequiredString(
            action,
            "durationMotionConfigPath",
            "Design Preview action");
        if (string.IsNullOrWhiteSpace(motionConfigPath))
        {
            return false;
        }

        var motion = JsonPath.Get(config, motionConfigPath.Split('.', StringSplitOptions.RemoveEmptyEntries)) as JsonObject
            ?? throw new InvalidOperationException(
                $"Design Preview action Motion path '{motionConfigPath}' must resolve to an object.");
        var durationMs = MotionTimingDuration.RequirePositiveMilliseconds(
            themeTokens,
            motion,
            $"Design Preview action Motion path '{motionConfigPath}'");
        action["durationSeconds"] = durationMs / 1000.0;
        return true;
    }
}
