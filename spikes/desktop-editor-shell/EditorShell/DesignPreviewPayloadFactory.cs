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
    string ComponentBaseConfigsJson = "{}",
    string AppConfigJson = "{}",
    string InstanceJson = "{}",
    string DeviceId = "",
    int FrameRate = 25,
    string ThemeMode = "",
    string ThemeStatusBarVariantReference = "",
    string ThemeNavigationBarVariantReference = "",
    int LocalFrame = 0);

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
            ProjectTreeNodeKind.ComponentClass => FromComponentClass(dataSource, node, theme),
            ProjectTreeNodeKind.ComponentVariant => FromComponentVariant(dataSource, node, theme),
            ProjectTreeNodeKind.Module => FromModule(dataSource, node, themeMode, theme),
            ProjectTreeNodeKind.ModuleVariant => FromModuleVariant(dataSource, node, themeMode, theme),
            ProjectTreeNodeKind.ModuleInstance => FromModuleInstance(dataSource, node.Id, theme.DeviceId, themeMode, theme, dataSource.ModuleInstanceLocalFrame(node.Id, timelineFrame)),
            ProjectTreeNodeKind.Shot => FromShot(dataSource, node, theme.DeviceId, themeMode, theme, timelineFrame),
            _ => null,
        };
        return payload is null
            ? null
            : payload with
            {
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
        int? localTimelineFrame = null)
    {
        var instance = dataSource.LoadModuleInstance(moduleInstanceId);
        var effectiveThemeMode = EffectiveThemeMode(instance.ConfigJson, themeMode);
        var runtimePreview = DesignPreviewTestValues.Parse(DesignPreviewTestValues.RuntimeJson(
            instance.RuntimePreviewJson));
        if (localTimelineFrame is not null
            && runtimePreview["timelineFrameJsonKey"]?.GetValue<string>() is { Length: > 0 } timelineFrameJsonKey)
        {
            runtimePreview[timelineFrameJsonKey] = Math.Max(0, localTimelineFrame.Value);
        }
        var runtimeActorId = runtimePreview["actorId"]?.GetValue<string>();
        var ownerActorId = string.IsNullOrWhiteSpace(runtimeActorId) ? instance.OwnerActorId : runtimeActorId;
        var ownerActor = string.IsNullOrWhiteSpace(ownerActorId)
            ? ActorPreviewInputFactory.CreateSample()
            : dataSource.CreateActorPreview(ownerActorId, effectiveThemeMode, theme.PaletteColors);
        runtimePreview["actor"] = ownerActor;
        if (runtimePreview["messages"] is JsonArray messages)
        {
            foreach (var message in messages.OfType<JsonObject>())
            {
                var actorId = message["actorId"]?.GetValue<string>() ?? "";
                message["actor"] = string.IsNullOrWhiteSpace(actorId)
                    ? ActorPreviewInputFactory.CreateSample()
                    : dataSource.CreateActorPreview(actorId, effectiveThemeMode, theme.PaletteColors);
            }
        }
        var instanceJson = new JsonObject
        {
            ["animation"] = JsonNode.Parse(instance.AnimationJson) ?? new JsonObject(),
            ["context"] = new JsonObject
            {
                ["shotId"] = instance.ShotId,
                ["moduleInstanceId"] = moduleInstanceId,
                ["localFrame"] = Math.Max(0, localTimelineFrame ?? 0),
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
            instance.ComponentBaseConfigsJson,
            instance.AppConfigJson,
            instanceJson.ToJsonString(),
            deviceId,
            instance.FrameRate,
            effectiveThemeMode,
            LocalFrame: Math.Max(0, localTimelineFrame ?? 0));
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

    private static DesignPreviewPayload FromModule(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode node,
        string themeMode,
        DesignPreviewThemeContext theme)
    {
        var settings = dataSource.LoadModule(node);
        var effectiveThemeMode = EffectiveThemeMode(settings.ConfigJson, themeMode);
        var config = DesignPreviewTestValues.Parse(settings.ConfigJson);
        var effectivePreview = RuntimeInputForwardingContract.EffectivePreview(
            DesignPreviewTestValues.Parse(settings.DesignPreviewJson),
            config);
        var runtimePreview = DesignPreviewTestValues.Parse(DesignPreviewTestValues.RuntimeJson(effectivePreview.ToJsonString()));
        var actorId = runtimePreview["actorId"]?.GetValue<string>() ?? "";
        runtimePreview["actor"] = string.IsNullOrWhiteSpace(actorId)
            ? ActorPreviewInputFactory.CreateSample()
            : dataSource.CreateActorPreview(actorId, effectiveThemeMode, theme.PaletteColors);
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
            settings.ComponentBaseConfigsJson,
            settings.AppConfigJson,
            ThemeMode: effectiveThemeMode);
    }

    private static DesignPreviewPayload FromModuleVariant(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode node,
        string themeMode,
        DesignPreviewThemeContext theme)
    {
        var settings = dataSource.LoadModuleVariant(node);
        var effectiveThemeMode = EffectiveThemeMode(settings.ConfigJson, themeMode);
        var effectivePreview = RuntimeInputForwardingContract.EffectivePreview(
            DesignPreviewTestValues.Parse(settings.DesignPreviewJson),
            DesignPreviewTestValues.Parse(settings.ConfigJson));
        var runtimePreview = DesignPreviewTestValues.Parse(DesignPreviewTestValues.RuntimeJson(effectivePreview.ToJsonString()));
        var actorId = runtimePreview["actorId"]?.GetValue<string>() ?? "";
        runtimePreview["actor"] = string.IsNullOrWhiteSpace(actorId)
            ? ActorPreviewInputFactory.CreateSample()
            : dataSource.CreateActorPreview(actorId, effectiveThemeMode, theme.PaletteColors);
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
            settings.ComponentBaseConfigsJson,
            settings.AppConfigJson,
            ThemeMode: effectiveThemeMode);
    }

    private static string EffectiveThemeMode(string configJson, string selectedThemeMode)
    {
        var config = JsonPath.ParseRequiredObject(configJson, "Preview config");
        var mode = config["appearanceMode"] is JsonValue modeValue
                   && modeValue.TryGetValue<string>(out var parsedMode)
            ? parsedMode
            : "";
        return mode is "light" or "dark"
            ? mode
            : selectedThemeMode is "dark" ? "dark" : "light";
    }

    private static DesignPreviewPayload FromComponentClass(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode node,
        DesignPreviewThemeContext theme)
    {
        var settings = dataSource.LoadComponentClass(node);
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
            settings.ComponentBaseConfigsJson);
    }

    private static DesignPreviewPayload FromComponentVariant(
        DesignPreviewPayloadDataSource dataSource,
        ProjectTreeNode node,
        DesignPreviewThemeContext theme)
    {
        var settings = dataSource.LoadComponentVariant(node);
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
            foreach (var action in actions.OfType<JsonObject>())
            {
                changed |= ResolveActionDuration(config, themeTokens, action);
            }
        }

        return changed ? preview.ToJsonString() : designPreviewJson;
    }

    private static bool ResolveActionDuration(JsonObject config, JsonObject themeTokens, JsonObject action)
    {
        var motionConfigPath = JsonPath.String(action, "durationMotionConfigPath", "");
        if (string.IsNullOrWhiteSpace(motionConfigPath))
        {
            return false;
        }

        var motion = JsonPath.Get(config, motionConfigPath.Split('.', StringSplitOptions.RemoveEmptyEntries)) as JsonObject;
        var transition = motion is null ? "" : JsonPath.String(motion, "transition", "");
        if (string.IsNullOrWhiteSpace(transition))
        {
            return false;
        }

        var durationMs = JsonPath.NumberDouble(
            themeTokens,
            ["motion", "transitions", transition, "durationMs"],
            0);
        var delayMs = JsonPath.NumberDouble(
            themeTokens,
            ["motion", "transitions", transition, "delayMs"],
            0);
        if (durationMs <= 0)
        {
            return false;
        }

        action["durationSeconds"] = (Math.Max(0, delayMs) + durationMs) / 1000.0;
        return true;
    }
}
