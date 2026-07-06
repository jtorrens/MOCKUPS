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
        var iconTheme = !string.IsNullOrWhiteSpace(theme.IconThemeId)
            ? database.GetIconThemeSettings(theme.IconThemeId)
            : null;
        return node.Kind switch
        {
            ProjectTreeNodeKind.StatusBar => FromStatusBar(database, node, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme),
            ProjectTreeNodeKind.NavigationBar => FromNavigationBar(database, node, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme),
            ProjectTreeNodeKind.ComponentClass => FromComponentClass(database, node, theme.TokensJson, paletteColors, paletteNeutralColors, projectMediaRoot, iconTheme),
            _ => null,
        };
    }

    private static DesignPreviewPayload FromStatusBar(
        SpikeDatabase database,
        ProjectTreeNode node,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        IReadOnlyDictionary<string, bool> paletteNeutralColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme)
    {
        var settings = database.GetStatusBarSettings(node.Id);
        return new DesignPreviewPayload(
            "componentClass",
            settings.Name,
            settings.ConfigJson,
            themeTokensJson,
            paletteColors,
            paletteNeutralColors,
            projectMediaRoot,
            iconTheme?.AssetRoot ?? "",
            iconTheme?.MappingJson ?? "{}",
            "statusBar");
    }

    private static DesignPreviewPayload FromNavigationBar(
        SpikeDatabase database,
        ProjectTreeNode node,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        IReadOnlyDictionary<string, bool> paletteNeutralColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme)
    {
        var settings = database.GetNavigationBarSettings(node.Id);
        return new DesignPreviewPayload(
            "componentClass",
            settings.Name,
            settings.ConfigJson,
            themeTokensJson,
            paletteColors,
            paletteNeutralColors,
            projectMediaRoot,
            iconTheme?.AssetRoot ?? "",
            iconTheme?.MappingJson ?? "{}",
            "navigationBar");
    }

    private static DesignPreviewPayload FromComponentClass(
        SpikeDatabase database,
        ProjectTreeNode node,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        IReadOnlyDictionary<string, bool> paletteNeutralColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme)
    {
        var settings = database.GetComponentClassSettings(node.Id);
        var componentBaseConfigsJson = database.GetComponentClassBaseConfigsJson(settings.ProjectId);
        return new DesignPreviewPayload(
            "componentClass",
            settings.Name,
            settings.ConfigJson,
            themeTokensJson,
            paletteColors,
            paletteNeutralColors,
            projectMediaRoot,
            iconTheme?.AssetRoot ?? "",
            iconTheme?.MappingJson ?? "{}",
            settings.ComponentType,
            settings.DesignPreviewJson,
            componentBaseConfigsJson);
    }
}
