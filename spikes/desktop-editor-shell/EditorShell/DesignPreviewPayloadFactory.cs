using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;

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
    string ComponentBaseConfigsJson = "{}");

internal static class DesignPreviewPayloadFactory
{
    public static DesignPreviewPayload? Create(
        SpikeDatabase database,
        ProjectTreeNode? node,
        string? themeId)
    {
        if (node is null || string.IsNullOrWhiteSpace(themeId))
        {
            return null;
        }

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
        var projectMediaRoot = database.GetProjectSettings(theme.ProjectId).MediaRoot;
        var fontFaces = database.GetProductionFontFaces(theme.ProjectId);
        var iconTheme = !string.IsNullOrWhiteSpace(theme.IconThemeId)
            ? database.GetIconThemeSettings(theme.IconThemeId)
            : null;
        return node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => FromComponentClass(database, node, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces),
            ProjectTreeNodeKind.ComponentPreset => FromComponentPreset(database, node, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme, fontFaces),
            _ => null,
        };
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
        var configJson = database.NormalizeComponentConfigJsonForPreview(settings.ProjectId, settings.ConfigJson);
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
            settings.DesignPreviewJson,
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
        var configJson = database.NormalizeComponentConfigJsonForPreview(settings.ProjectId, settings.ConfigJson);
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
            settings.DesignPreviewJson,
            componentBaseConfigsJson);
    }
}
