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
    string InstanceJson = "{}");

internal static class DesignPreviewPayloadFactory
{
    public static DesignPreviewPayload? Create(
        SpikeDatabase database,
        ProjectTreeNode? node,
        string? themeId,
        string themeMode = "light")
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
        return node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => FromComponentClass(database, node, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces),
            ProjectTreeNodeKind.ComponentPreset => FromComponentPreset(database, node, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces),
            ProjectTreeNodeKind.Module => FromModule(database, node, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces),
            ProjectTreeNodeKind.ModuleInstance => FromModuleInstance(database, node, themeMode, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces),
            _ => null,
        };
    }

    private static string? ResolveThemeId(SpikeDatabase database, ProjectTreeNode node, string? selectedThemeId)
    {
        if (node.Kind != ProjectTreeNodeKind.ModuleInstance)
        {
            return selectedThemeId;
        }

        var instance = database.GetModuleInstanceSettings(node.Id);
        var shot = database.GetShotSettings(instance.ShotId);
        if (string.IsNullOrWhiteSpace(shot.OwnerActorId)) return selectedThemeId;

        var actor = database.GetActorSettings(shot.OwnerActorId);
        return string.IsNullOrWhiteSpace(actor.DefaultThemeId)
            ? selectedThemeId
            : actor.DefaultThemeId;
    }

    private static DesignPreviewPayload FromModuleInstance(
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
        var instance = database.GetModuleInstanceSettings(node.Id);
        var module = database.GetModuleSettings(instance.ModuleId);
        var app = database.GetAppSettings(instance.AppId);
        var shot = database.GetShotSettings(instance.ShotId);
        var ownerActor = string.IsNullOrWhiteSpace(shot.OwnerActorId)
            ? ActorPreviewInputFactory.CreateSample()
            : ActorPreviewInputFactory.Create(database, shot.OwnerActorId, themeMode, paletteColors);
        var instanceJson = new JsonObject
        {
            ["content"] = JsonNode.Parse(instance.ContentJson) ?? new JsonObject(),
            ["behavior"] = JsonNode.Parse(instance.BehaviorJson) ?? new JsonObject(),
            ["animation"] = JsonNode.Parse(instance.AnimationJson) ?? new JsonObject(),
            ["context"] = new JsonObject
            {
                ["shotId"] = instance.ShotId,
                ["ownerActor"] = ownerActor,
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
            module.DesignPreviewJson,
            database.GetComponentClassBaseConfigsJson(module.ProjectId),
            app.ConfigJson,
            instanceJson.ToJsonString());
    }

    private static DesignPreviewPayload FromModule(
        SpikeDatabase database,
        ProjectTreeNode node,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        IReadOnlyDictionary<string, bool> paletteNeutralColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme,
        IReadOnlyList<SpikeDatabase.ProductionFontFace> fontFaces)
    {
        var settings = database.GetModuleSettings(node.Id);
        var appSettings = database.GetModuleAppSettings(node.Id);
        var componentBaseConfigsJson = database.GetComponentClassBaseConfigsJson(settings.ProjectId);
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
            settings.DesignPreviewJson,
            componentBaseConfigsJson,
            appSettings.ConfigJson);
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
        var designPreviewJson = ResolveActionDurationsJson(configJson, themeTokensJson, settings.DesignPreviewJson);
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
        var designPreviewJson = ResolveActionDurationsJson(configJson, themeTokensJson, settings.DesignPreviewJson);
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
        if (durationMs <= 0)
        {
            return false;
        }

        action["durationSeconds"] = durationMs / 1000.0;
        return true;
    }
}
