using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record DesignPreviewPayload(
    string Kind,
    string Name,
    string ConfigJson,
    string ThemeTokensJson,
    IReadOnlyDictionary<string, string> PaletteColors,
    IReadOnlyDictionary<string, bool> PaletteNeutralColors,
    string ProjectMediaRoot,
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
    string OwnerId = "");

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

        var theme = dataSource.LoadThemeContext(node, themeId);
        if (theme is null) return null;
        var payload = node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => FromComponentSource(dataSource.LoadComponentClass(node), themeMode, theme),
            ProjectTreeNodeKind.ComponentVariant => FromComponentSource(dataSource.LoadComponentVariant(node), themeMode, theme),
            ProjectTreeNodeKind.Module => FromModuleSource(dataSource, dataSource.LoadModule(node), themeMode, theme),
            ProjectTreeNodeKind.ModuleVariant => FromModuleSource(dataSource, dataSource.LoadModuleVariant(node), themeMode, theme),
            ProjectTreeNodeKind.ModuleInstance => FromModuleInstance(dataSource, node.Id, theme.DeviceId, themeMode, theme, dataSource.ModuleInstanceScreenFrame(node.Id, timelineFrame)),
            ProjectTreeNodeKind.Shot => FromShot(dataSource, node, theme.DeviceId, themeMode, theme, timelineFrame),
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

    private static DesignPreviewPayload FromModuleInstance(
        DesignPreviewPayloadDataSource dataSource,
        string moduleInstanceId,
        string deviceId,
        string themeMode,
        DesignPreviewThemeContext theme,
        int? screenFrame = null)
    {
        var instance = dataSource.LoadModuleInstance(moduleInstanceId);
        var effectiveThemeMode = ResolveEffectiveThemeMode(
            instance.ConfigJson,
            themeMode,
            $"Module Instance '{moduleInstanceId}' Variant config");
        var runtimePreview = DesignPreviewTestValues.Parse(DesignPreviewTestValues.RuntimeJson(
            instance.RuntimePreviewJson));
        if (screenFrame is not null
            && runtimePreview["timelineFrameJsonKey"]?.GetValue<string>() is { Length: > 0 } timelineFrameJsonKey)
        {
            runtimePreview[timelineFrameJsonKey] = Math.Max(0, screenFrame.Value);
        }
        var runtimeActorId = runtimePreview["actorId"]?.GetValue<string>();
        var ownerActorId = string.IsNullOrWhiteSpace(runtimeActorId) ? instance.OwnerActorId : runtimeActorId;
        if (string.IsNullOrWhiteSpace(ownerActorId))
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no effective Production Actor.");
        }
        runtimePreview["actor"] = dataSource.CreateActorPreview(
            ownerActorId,
            effectiveThemeMode,
            theme.PaletteColors);
        ModuleRuntimeDocumentContracts.PrepareProduction(
            instance.RecordClassId,
            $"Module Instance '{moduleInstanceId}' Production payload",
            runtimePreview,
            instance.OwnerActorId);
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
            theme.IconAssetRoot,
            theme.IconMappingJson,
            theme.FontFaces,
            instance.RecordClassId,
            runtimePreviewJson,
            runtimePreviewJson,
            effectiveThemeMode,
            instance.ComponentBaseConfigsJson,
            instance.AppConfigJson,
            instanceJson.ToJsonString(),
            deviceId,
            instance.FrameRate,
            LocalFrame: Math.Max(0, screenFrame ?? 0));
    }

    private static DesignPreviewPayload? FromShot(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode shotNode,
        string deviceId,
        string themeMode,
        DesignPreviewThemeContext theme,
        int shotFrame)
    {
        var slots = dataSource.LoadShotSlots(shotNode.Id);
        if (slots.Count == 0) return null;
        var boundedFrame = Math.Max(0, Math.Min(slots.Sum((slot) => slot.DurationFrames) - 1, shotFrame));
        var startFrame = 0;
        var active = slots[^1];
        foreach (var slot in slots)
        {
            if (boundedFrame < startFrame + slot.DurationFrames)
            {
                active = slot;
                break;
            }
            startFrame += slot.DurationFrames;
        }
        var payload = FromModuleInstance(
            dataSource,
            active.Id,
            deviceId,
            themeMode,
            theme,
            boundedFrame - startFrame);
        var shotPreview = DesignPreviewTestValues.Parse(payload.DesignPreviewJson);
        shotPreview.Remove("actions");
        return payload with
        {
            Name = active.Name,
            DesignPreviewJson = shotPreview.ToJsonString(),
        };
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
            $"Module '{settings.RecordClassId}' Variant config");
        var config = DesignPreviewTestValues.Parse(settings.ConfigJson);
        var effectivePreview = RuntimeInputForwardingContract.EffectivePreview(
            DesignPreviewTestValues.Parse(settings.DesignPreviewJson),
            config);
        var runtimePreview = DesignPreviewTestValues.Parse(DesignPreviewTestValues.RuntimeJson(effectivePreview.ToJsonString()));
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
            theme.IconAssetRoot,
            theme.IconMappingJson,
            theme.FontFaces,
            settings.RecordClassId,
            runtimePreviewJson,
            runtimePreviewJson,
            effectiveThemeMode,
            settings.ComponentBaseConfigsJson,
            settings.AppConfigJson);
    }

    private static string ResolveEffectiveThemeMode(
        string configJson,
        string selectedThemeMode,
        string owner)
    {
        var config = JsonPath.ParseRequiredObject(configJson, owner);
        return ModuleAppearanceModeContract.Resolve(config, selectedThemeMode, owner);
    }

    private static DesignPreviewPayload FromComponentSource(
        DesignPreviewComponentSource settings,
        string themeMode,
        DesignPreviewThemeContext theme)
    {
        var effectiveThemeMode = ModuleAppearanceModeContract.RequireResolved(
            themeMode,
            $"Component '{settings.ComponentType}' Preview Theme mode");
        var configJson = settings.ConfigJson;
        var effectivePreview = RuntimeInputForwardingContract.EffectivePreview(
            DesignPreviewTestValues.Parse(settings.DesignPreviewJson),
            DesignPreviewTestValues.Parse(configJson));
        var designPreviewJson = ResolveActionDurationsJson(
            configJson,
            theme.TokensJson,
            DesignPreviewTestValues.RuntimeJson(effectivePreview.ToJsonString()));
        return new DesignPreviewPayload(
            "componentClass",
            settings.Name,
            configJson,
            theme.TokensJson,
            theme.PaletteColors,
            theme.PaletteNeutralColors,
            theme.ProjectMediaRoot,
            theme.IconAssetRoot,
            theme.IconMappingJson,
            theme.FontFaces,
            settings.ComponentType,
            designPreviewJson,
            designPreviewJson,
            effectiveThemeMode,
            settings.ComponentBaseConfigsJson);
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
