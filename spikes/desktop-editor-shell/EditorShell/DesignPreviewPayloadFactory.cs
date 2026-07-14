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
    IReadOnlyList<SpikeDatabase.ProductionFontFace> FontFaces,
    string ComponentType = "",
    string DesignPreviewJson = "",
    string ComponentBaseConfigsJson = "{}",
    string AppConfigJson = "{}",
    string InstanceJson = "{}",
    string DeviceId = "",
    int FrameRate = 25,
    string ThemeMode = "",
    string ThemeStatusBarPresetId = "",
    string ThemeNavigationBarPresetId = "",
    int LocalFrame = 0);

internal static class DesignPreviewPayloadFactory
{
    public static DesignPreviewPayload? Create(
        SpikeDatabase database,
        ProjectTreeNode? node,
        string? themeId,
        string themeMode = "light",
        int timelineFrame = 0)
    {
        if (node is null)
        {
            return null;
        }

        themeId = ResolveThemeId(database, node, themeId);
        if (string.IsNullOrWhiteSpace(themeId)) return null;

        SpikeDatabase.ThemeSettings theme;
        try
        {
            theme = database.GetThemeSettings(themeId);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        var paletteColors = database.GetPaletteColorMap(theme.ProjectId);
        var paletteNeutralColors = database.GetPaletteNeutralMap(theme.ProjectId);
        var projectMediaRoot = ProjectPathService.ResolveProjectPath(database.GetProjectSettings(theme.ProjectId).MediaRoot);
        var fontFaces = database.GetProductionFontFaces(theme.ProjectId);
        var iconTheme = !string.IsNullOrWhiteSpace(theme.IconThemeId)
            ? database.GetIconThemeSettings(theme.IconThemeId)
            : null;
        var payload = node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => FromComponentClass(database, node, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces),
            ProjectTreeNodeKind.ComponentPreset => FromComponentPreset(database, node, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces),
            ProjectTreeNodeKind.Module => FromModule(database, node, themeMode, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces),
            ProjectTreeNodeKind.ModuleInstance => FromModuleInstance(database, node, ResolveDeviceId(database, node), themeMode, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces, ModuleInstanceLocalFrame(database, node.Id, timelineFrame)),
            ProjectTreeNodeKind.Shot => FromShot(database, node, ResolveDeviceId(database, node), themeMode, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces, timelineFrame),
            _ => null,
        };
        return payload is null
            ? null
            : payload with
            {
                ThemeStatusBarPresetId = theme.StatusBarId,
                ThemeNavigationBarPresetId = theme.NavigationBarId,
                LocalFrame = node.Kind is ProjectTreeNodeKind.ModuleInstance or ProjectTreeNodeKind.Shot
                    ? payload.LocalFrame
                    : Math.Max(0, timelineFrame),
            };
    }

    private static int ModuleInstanceLocalFrame(SpikeDatabase database, string instanceId, int shotFrame)
    {
        var instance = database.GetModuleInstanceSettings(instanceId);
        var startFrame = 0;
        foreach (var slot in database.GetShotModuleInstanceSlots(instance.ShotId))
        {
            if (slot.Id == instanceId) return Math.Max(0, shotFrame - startFrame);
            startFrame += ModuleInstanceTimeline.DurationFrames(database, slot.Id);
        }
        return 0;
    }

    internal static string? ResolveThemeId(SpikeDatabase database, ProjectTreeNode node, string? selectedThemeId)
    {
        if (node.Kind is not ProjectTreeNodeKind.ModuleInstance and not ProjectTreeNodeKind.Shot)
        {
            return selectedThemeId;
        }

        var shot = node.Kind == ProjectTreeNodeKind.Shot
            ? database.GetShotSettings(node.Id)
            : database.GetShotSettings(database.GetModuleInstanceSettings(node.Id).ShotId);
        if (string.IsNullOrWhiteSpace(shot.OwnerActorId)) return selectedThemeId;

        var actor = database.GetActorSettings(shot.OwnerActorId);
        return string.IsNullOrWhiteSpace(actor.DefaultThemeId)
            ? selectedThemeId
            : actor.DefaultThemeId;
    }

    private static string ResolveDeviceId(SpikeDatabase database, ProjectTreeNode node)
    {
        if (node.Kind is not ProjectTreeNodeKind.ModuleInstance and not ProjectTreeNodeKind.Shot) return "";
        var shot = node.Kind == ProjectTreeNodeKind.Shot
            ? database.GetShotSettings(node.Id)
            : database.GetShotSettings(database.GetModuleInstanceSettings(node.Id).ShotId);
        if (string.IsNullOrWhiteSpace(shot.OwnerActorId)) return "";
        return database.GetActorSettings(shot.OwnerActorId).DefaultDeviceId;
    }

    private static DesignPreviewPayload FromModuleInstance(
        SpikeDatabase database,
        ProjectTreeNode node,
        string deviceId,
        string themeMode,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        IReadOnlyDictionary<string, bool> paletteNeutralColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme,
        IReadOnlyList<SpikeDatabase.ProductionFontFace> fontFaces,
        int? localTimelineFrame = null)
    {
        var instance = database.GetModuleInstanceSettings(node.Id);
        var module = database.GetModuleSettings(instance.ModuleId);
        var effectiveThemeMode = EffectiveThemeMode(module.ConfigJson, themeMode);
        var app = database.GetAppSettings(instance.AppId);
        var shot = database.GetShotSettings(instance.ShotId);
        var runtimePreview = DesignPreviewTestValues.Parse(database.GetModuleInstanceRuntimePreviewJson(node.Id));
        if (localTimelineFrame is not null
            && runtimePreview["timelineFrameJsonKey"]?.GetValue<string>() is { Length: > 0 } timelineFrameJsonKey)
        {
            runtimePreview[timelineFrameJsonKey] = Math.Max(0, localTimelineFrame.Value);
        }
        var runtimeActorId = runtimePreview["actorId"]?.GetValue<string>();
        var ownerActorId = string.IsNullOrWhiteSpace(runtimeActorId) ? shot.OwnerActorId : runtimeActorId;
        var ownerActor = string.IsNullOrWhiteSpace(ownerActorId)
            ? ActorPreviewInputFactory.CreateSample()
            : ActorPreviewInputFactory.Create(database, ownerActorId, effectiveThemeMode, paletteColors);
        runtimePreview["actor"] = ownerActor;
        if (runtimePreview["messages"] is JsonArray messages)
        {
            foreach (var message in messages.OfType<JsonObject>())
            {
                var actorId = message["actorId"]?.GetValue<string>() ?? "";
                message["actor"] = string.IsNullOrWhiteSpace(actorId)
                    ? ActorPreviewInputFactory.CreateSample()
                    : ActorPreviewInputFactory.Create(database, actorId, effectiveThemeMode, paletteColors);
            }
        }
        var instanceJson = new JsonObject
        {
            ["animation"] = JsonNode.Parse(instance.AnimationJson) ?? new JsonObject(),
            ["context"] = new JsonObject
            {
                ["shotId"] = instance.ShotId,
                ["moduleInstanceId"] = node.Id,
                ["localFrame"] = Math.Max(0, localTimelineFrame ?? 0),
            },
        };
        return new DesignPreviewPayload(
            "moduleInstance",
            instance.Name,
            module.ConfigJson,
            themeTokensJson,
            paletteColors,
            paletteNeutralColors,
            projectMediaRoot,
            iconTheme?.AssetRoot ?? "",
            iconTheme?.MappingJson ?? "{}",
            fontFaces,
            module.RecordClassId,
            runtimePreview.ToJsonString(),
            database.GetComponentClassBaseConfigsJson(module.ProjectId),
            app.ConfigJson,
            instanceJson.ToJsonString(),
            deviceId,
            shot.Fps,
            effectiveThemeMode,
            LocalFrame: Math.Max(0, localTimelineFrame ?? 0));
    }

    private static DesignPreviewPayload? FromShot(
        SpikeDatabase database,
        ProjectTreeNode shotNode,
        string deviceId,
        string themeMode,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        IReadOnlyDictionary<string, bool> paletteNeutralColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme,
        IReadOnlyList<SpikeDatabase.ProductionFontFace> fontFaces,
        int shotFrame)
    {
        var slots = database.GetShotModuleInstanceSlots(shotNode.Id);
        if (slots.Count == 0) return null;
        var boundedFrame = Math.Max(0, Math.Min(ModuleInstanceTimeline.ShotDurationFrames(database, shotNode.Id) - 1, shotFrame));
        var startFrame = 0;
        var active = slots[^1];
        foreach (var slot in slots)
        {
            var duration = ModuleInstanceTimeline.DurationFrames(database, slot.Id);
            if (boundedFrame < startFrame + duration)
            {
                active = slot;
                break;
            }
            startFrame += duration;
        }
        var instanceNode = new ProjectTreeNode(
            ProjectTreeNodeKind.ModuleInstance,
            active.Id,
            active.Name,
            active.ModuleName,
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance),
            shotNode);
        var payload = FromModuleInstance(
            database,
            instanceNode,
            deviceId,
            themeMode,
            themeTokensJson,
            paletteColors,
            paletteNeutralColors,
            projectMediaRoot,
            iconTheme,
            fontFaces,
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
        SpikeDatabase database,
        ProjectTreeNode node,
        string themeMode,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        IReadOnlyDictionary<string, bool> paletteNeutralColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme,
        IReadOnlyList<SpikeDatabase.ProductionFontFace> fontFaces)
    {
        var settings = database.GetModuleSettings(node.Id);
        var effectiveThemeMode = EffectiveThemeMode(settings.ConfigJson, themeMode);
        var appSettings = database.GetModuleAppSettings(node.Id);
        var componentBaseConfigsJson = database.GetComponentClassBaseConfigsJson(settings.ProjectId);
        var config = DesignPreviewTestValues.Parse(settings.ConfigJson);
        var effectivePreview = RuntimeInputForwardingContract.EffectivePreview(
            DesignPreviewTestValues.Parse(settings.DesignPreviewJson),
            config);
        var runtimePreview = DesignPreviewTestValues.Parse(DesignPreviewTestValues.RuntimeJson(effectivePreview.ToJsonString()));
        var actorId = runtimePreview["actorId"]?.GetValue<string>() ?? "";
        runtimePreview["actor"] = string.IsNullOrWhiteSpace(actorId)
            ? ActorPreviewInputFactory.CreateSample()
            : ActorPreviewInputFactory.Create(database, actorId, effectiveThemeMode, paletteColors);
        return new DesignPreviewPayload(
            "module",
            node.Name,
            settings.ConfigJson,
            themeTokensJson,
            paletteColors,
            paletteNeutralColors,
            projectMediaRoot,
            iconTheme?.AssetRoot ?? "",
            iconTheme?.MappingJson ?? "{}",
            fontFaces,
            settings.RecordClassId,
            runtimePreview.ToJsonString(),
            componentBaseConfigsJson,
            appSettings.ConfigJson,
            ThemeMode: effectiveThemeMode);
    }

    private static string EffectiveThemeMode(string configJson, string selectedThemeMode)
    {
        var config = string.IsNullOrWhiteSpace(configJson)
            ? new JsonObject()
            : JsonNode.Parse(configJson) as JsonObject ?? new JsonObject();
        var mode = config["appearanceMode"] is JsonValue modeValue
                   && modeValue.TryGetValue<string>(out var parsedMode)
            ? parsedMode
            : "";
        return mode is "light" or "dark"
            ? mode
            : selectedThemeMode is "dark" ? "dark" : "light";
    }

    private static DesignPreviewPayload FromComponentClass(
        SpikeDatabase database,
        ProjectTreeNode node,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        IReadOnlyDictionary<string, bool> paletteNeutralColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme,
        IReadOnlyList<SpikeDatabase.ProductionFontFace> fontFaces)
    {
        var settings = database.GetComponentClassSettings(node.Id);
        var componentBaseConfigsJson = database.GetComponentClassBaseConfigsJson(settings.ProjectId);
        var configJson = database.ValidateComponentPresetReferencesForPreview(settings.ProjectId, settings.ConfigJson);
        var effectivePreview = RuntimeInputForwardingContract.EffectivePreview(
            DesignPreviewTestValues.Parse(settings.DesignPreviewJson),
            DesignPreviewTestValues.Parse(configJson));
        var designPreviewJson = ResolveActionDurationsJson(
            configJson,
            themeTokensJson,
            DesignPreviewTestValues.RuntimeJson(effectivePreview.ToJsonString()));
        return new DesignPreviewPayload(
            "componentClass",
            settings.Name,
            configJson,
            themeTokensJson,
            paletteColors,
            paletteNeutralColors,
            projectMediaRoot,
            iconTheme?.AssetRoot ?? "",
            iconTheme?.MappingJson ?? "{}",
            fontFaces,
            settings.ComponentType,
            designPreviewJson,
            componentBaseConfigsJson);
    }

    private static DesignPreviewPayload FromComponentPreset(
        SpikeDatabase database,
        ProjectTreeNode node,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        IReadOnlyDictionary<string, bool> paletteNeutralColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme,
        IReadOnlyList<SpikeDatabase.ProductionFontFace> fontFaces)
    {
        var settings = database.GetComponentPresetSettings(node);
        var componentBaseConfigsJson = database.GetComponentClassBaseConfigsJson(settings.ProjectId);
        var configJson = database.ValidateComponentPresetReferencesForPreview(settings.ProjectId, settings.ConfigJson);
        var effectivePreview = RuntimeInputForwardingContract.EffectivePreview(
            DesignPreviewTestValues.Parse(settings.DesignPreviewJson),
            DesignPreviewTestValues.Parse(configJson));
        var designPreviewJson = ResolveActionDurationsJson(
            configJson,
            themeTokensJson,
            DesignPreviewTestValues.RuntimeJson(effectivePreview.ToJsonString()));
        return new DesignPreviewPayload(
            "componentClass",
            settings.Name,
            configJson,
            themeTokensJson,
            paletteColors,
            paletteNeutralColors,
            projectMediaRoot,
            iconTheme?.AssetRoot ?? "",
            iconTheme?.MappingJson ?? "{}",
            fontFaces,
            settings.ComponentType,
            designPreviewJson,
            componentBaseConfigsJson);
    }

    private static string ResolveActionDurationsJson(
        string configJson,
        string themeTokensJson,
        string designPreviewJson)
    {
        var preview = JsonPath.ParseObject(designPreviewJson);
        var changed = false;
        var config = JsonPath.ParseObject(configJson);
        var themeTokens = JsonPath.ParseObject(themeTokensJson);

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
