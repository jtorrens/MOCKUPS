using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ScreenTransitionLayerPayload(
    DesignPreviewPayload Owner,
    string MotionJson,
    string Phase,
    double PhaseTimeMilliseconds);

internal sealed record ScreenTransitionPayload(
    IReadOnlyList<ScreenTransitionLayerPayload> Layers,
    int DurationFrames);

internal sealed record ScreenTimingPayload(
    int ScreenFrame,
    int TransitionFrameCount,
    int ActionDelayFrames,
    int ActionDurationFrames,
    int ScreenStartFrame,
    int ShotDurationFrames,
    string TransitionMotionJson)
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
    string ProjectId = "",
    string SystemPreviewFixtureRoot = "");

internal static class DesignPreviewPayloadLayers
{
    public static DesignPreviewPayload PrimaryOwner(
        DesignPreviewPayload payload) =>
        payload.ScreenTransition is not { } transition
            ? payload
            : RequiredLayers(transition)[^1].Owner;

    public static DesignPreviewPayload MapOwners(
        DesignPreviewPayload payload,
        Func<DesignPreviewPayload, DesignPreviewPayload> transform)
    {
        if (payload.ScreenTransition is not { } transition)
        {
            return transform(payload);
        }

        var layers = RequiredLayers(transition)
            .Select((layer) => layer with
            {
                Owner = transform(layer.Owner),
            })
            .ToArray();
        return Synchronize(payload, transition, layers);
    }

    public static DesignPreviewPayload MapPrimaryOwner(
        DesignPreviewPayload payload,
        Func<DesignPreviewPayload, DesignPreviewPayload> transform)
    {
        if (payload.ScreenTransition is not { } transition)
        {
            return transform(payload);
        }

        var layers = RequiredLayers(transition).ToArray();
        layers[^1] = layers[^1] with
        {
            Owner = transform(layers[^1].Owner),
        };
        return Synchronize(payload, transition, layers);
    }

    private static DesignPreviewPayload Synchronize(
        DesignPreviewPayload envelope,
        ScreenTransitionPayload transition,
        IReadOnlyList<ScreenTransitionLayerPayload> layers)
    {
        var primary = layers[^1].Owner;
        return primary with
        {
            Kind = envelope.Kind,
            Name = envelope.Name,
            OwnerId = envelope.OwnerId,
            ScreenTransition = transition with
            {
                Layers = layers,
            },
        };
    }

    private static IReadOnlyList<ScreenTransitionLayerPayload> RequiredLayers(
        ScreenTransitionPayload transition)
    {
        if (transition.Layers.Count == 0)
        {
            throw new InvalidOperationException(
                "A Screen transition payload requires at least one owner layer.");
        }
        return transition.Layers;
    }
}

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

        if (node.Kind is ProjectTreeNodeKind.ModuleInstance or ProjectTreeNodeKind.Shot)
            return PrepareProduction(dataSource, node, themeId, themeMode, timelineFrame, timelineFrame,
                CancellationToken.None).AtFrame(timelineFrame);
        var theme = dataSource.LoadThemeContext(node, themeId);
        if (theme is null) return null;
        var payload = node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => FromComponentSource(dataSource, dataSource.LoadComponentClass(node), themeMode, theme),
            ProjectTreeNodeKind.ComponentVariant => FromComponentSource(dataSource, dataSource.LoadComponentVariant(node), themeMode, theme),
            ProjectTreeNodeKind.Module => FromModuleSource(dataSource, dataSource.LoadModule(node), themeMode, theme),
            ProjectTreeNodeKind.ModuleVariant => FromModuleSource(dataSource, dataSource.LoadModuleVariant(node), themeMode, theme),
            _ => null,
        };
        return payload is null ? null : payload with
        {
            OwnerId = node.Id,
            ThemeStatusBarVariantReference = theme.StatusBarVariantReference,
            ThemeNavigationBarVariantReference = theme.NavigationBarVariantReference,
            LocalFrame = Math.Max(0, timelineFrame),
        };
    }

    public static PreparedProductionPreview PrepareProduction(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode node,
        string? themeId,
        string themeMode,
        int startFrame,
        int endFrame,
        CancellationToken cancellationToken) =>
        PrepareProduction(dataSource, node, themeMode, startFrame, endFrame,
            screenId => dataSource.LoadThemeContext(ScreenNode(screenId), themeId)
                ?? throw new InvalidOperationException($"Screen '{screenId}' has no Preview Theme context."),
            respectAuthoredAppearance: false, cancellationToken);

    public static PreparedProductionPreview PrepareProductionRender(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode shot,
        string themeStrategy,
        string themeId,
        string deviceId,
        string requestedThemeMode,
        int startFrame,
        int endFrame,
        CancellationToken cancellationToken)
    {
        if (shot.Kind != ProjectTreeNodeKind.Shot)
            throw new InvalidOperationException("A Production render payload requires a Shot.");
        return PrepareProduction(dataSource, shot,
            ModuleAppearanceModeContract.RequireResolved(requestedThemeMode, $"Shot '{shot.Id}' render appearance"),
            startFrame, endFrame,
            screenId => dataSource.LoadProductionRenderThemeContext(shot, screenId, themeStrategy, themeId, deviceId),
            respectAuthoredAppearance: true, cancellationToken);
    }

    public static DesignPreviewPayload? CreateProductionRender(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode shot,
        string themeStrategy,
        string themeId,
        string deviceId,
        string requestedThemeMode,
        int shotFrame) =>
        PrepareProductionRender(dataSource, shot, themeStrategy, themeId, deviceId, requestedThemeMode,
            shotFrame, shotFrame, CancellationToken.None).AtFrame(shotFrame);

    private static PreparedProductionPreview PrepareProduction(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode node,
        string themeMode,
        int startFrame,
        int endFrame,
        Func<string, DesignPreviewThemeContext> themeForScreen,
        bool respectAuthoredAppearance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var isScreen = node.Kind == ProjectTreeNodeKind.ModuleInstance;
        if (!isScreen && node.Kind != ProjectTreeNodeKind.Shot)
            throw new InvalidOperationException("Production preparation requires an exact Shot or Screen.");
        var shotId = dataSource.ShotIdFor(node);
        var shot = dataSource.LoadShotSettings(shotId);
        var slots = dataSource.LoadShotSlots(shotId);
        var selected = isScreen ? new[] { slots.Single(slot => slot.Id == node.Id) }
            : slots.Where(slot => Math.Max(0, startFrame) < slot.StartFrame + slot.EffectiveDurationFrames
                && Math.Min(shot.DurationFrames - 1, Math.Max(startFrame, endFrame)) >= slot.StartFrame).ToArray();
        var componentBaseConfigs = dataSource.LoadComponentBaseConfigs(shot.ProjectId);
        var screens = new List<DesignPreviewPayload>();
        foreach (var slot in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var theme = themeForScreen(slot.Id);
            var owner = FromModuleInstance(dataSource, slot.Id, theme.DeviceId, themeMode, theme,
                0, respectAuthoredAppearance, componentBaseConfigs) with
            {
                ComponentBaseConfigsJson = componentBaseConfigs,
                OwnerId = slot.Id,
                ThemeStatusBarVariantReference = theme.StatusBarVariantReference,
                ThemeNavigationBarVariantReference = theme.NavigationBarVariantReference,
                ScreenTiming = new ScreenTimingPayload(0, slot.TransitionFrameCount, slot.ActionDelayFrames,
                    slot.ActionDurationFrames, slot.StartFrame, shot.DurationFrames, slot.TransitionJson),
            };
            var preview = DesignPreviewTestValues.Parse(owner.DesignPreviewJson);
            preview.Remove("actions");
            screens.Add(owner with { DesignPreviewJson = preview.ToJsonString() });
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new PreparedProductionPreview(node.Id, isScreen, shot.DurationFrames, screens);
    }

    private static DesignPreviewPayload FromModuleInstance(
        DesignPreviewPayloadDataSource dataSource,
        string moduleInstanceId,
        string deviceId,
        string themeMode,
        DesignPreviewThemeContext theme,
        int? screenFrame,
        bool respectAuthoredAppearance,
        string componentBaseConfigsJson)
    {
        var instance = dataSource.LoadModuleInstance(moduleInstanceId, componentBaseConfigsJson);
        var effectiveThemeMode = ResolveEffectiveThemeMode(
            instance.ConfigJson,
            themeMode,
            $"Module Instance '{moduleInstanceId}' Variant config",
            respectAuthoredAppearance);
        var runtimePreview = DesignPreviewTestValues.Parse(DesignPreviewTestValues.RuntimeJson(
            instance.RuntimePreviewJson));
        var animation = JsonPath.ParseRequiredObject(
            instance.AnimationJson,
            $"Module Instance '{moduleInstanceId}' animation_json");
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
            ["animation"] = animation,
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
            PreviewMediaDirectoryCatalog.Resolve(
                theme.ProjectMediaRoot,
                runtimePreviewJson),
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

    private static ProjectTreeNode ScreenNode(string screenId) =>
        new(
            ProjectTreeNodeKind.ModuleInstance,
            screenId,
            screenId,
            "",
            ProjectTreeNode.DefaultRecordClassId(
                ProjectTreeNodeKind.ModuleInstance));

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
            ComponentVariantConfigResolver(settings.ComponentBaseConfigsJson),
            dataSource.ComponentVariantRuntimeContract);
        var runtimeContractJson = DesignPreviewTestValues.RuntimeJson(effectivePreview.ToJsonString());
        var runtimePreview = DesignPreviewTestValues.Parse(runtimeContractJson);
        var actorId = runtimePreview["actorId"]?.GetValue<string>() ?? "";
        runtimePreview["actor"] = string.IsNullOrWhiteSpace(actorId)
            ? ActorPreviewInputFactory.CreateSample()
            : dataSource.CreateActorPreview(
                actorId,
                effectiveThemeMode,
                theme.PaletteColors,
                allowSystemPreviewFixtures: true);
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
            PreviewMediaDirectoryCatalog.Resolve(
                theme.ProjectMediaRoot,
                runtimePreviewJson,
                SystemPreviewFixtureCatalog.Root),
            theme.IconAssetRoot,
            theme.IconMappingJson,
            theme.FontFaces,
            settings.RecordClassId,
            runtimePreviewJson,
            runtimeContractJson,
            effectiveThemeMode,
            settings.ComponentBaseConfigsJson,
            settings.AppConfigJson,
            ProjectId: settings.ProjectId,
            SystemPreviewFixtureRoot: SystemPreviewFixtureCatalog.Root);
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
            ComponentVariantConfigResolver(settings.ComponentBaseConfigsJson),
            dataSource.ComponentVariantRuntimeContract);
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
            PreviewMediaDirectoryCatalog.Resolve(
                theme.ProjectMediaRoot,
                designPreviewJson,
                SystemPreviewFixtureCatalog.Root),
            theme.IconAssetRoot,
            theme.IconMappingJson,
            theme.FontFaces,
            settings.ComponentType,
            designPreviewJson,
            runtimeContractJson,
            effectiveThemeMode,
            settings.ComponentBaseConfigsJson,
            ProjectId: settings.ProjectId,
            SystemPreviewFixtureRoot: SystemPreviewFixtureCatalog.Root);
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
        Func<string, JsonObject> componentVariantConfig,
        Func<string, JsonObject> componentRuntimeValues)
    {
        return RuntimePreviewDocumentContract.PrepareFixture(
            preview,
            config,
            componentVariantConfig,
            componentRuntimeValues);
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
