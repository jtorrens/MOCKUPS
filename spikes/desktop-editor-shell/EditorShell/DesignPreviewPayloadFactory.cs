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
    string ProjectMediaRoot,
    string IconAssetRoot,
    string IconMappingJson);

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

        var theme = database.GetThemeSettings(themeId);
        var paletteColors = database.GetPaletteColorMap(theme.ProjectId);
        var projectMediaRoot = database.GetProjectSettings(theme.ProjectId).MediaRoot;
        var iconTheme = !string.IsNullOrWhiteSpace(theme.IconThemeId)
            ? database.GetIconThemeSettings(theme.IconThemeId)
            : null;
        return node.Kind switch
        {
            ProjectTreeNodeKind.StatusBar => FromStatusBar(database, node, theme.TokensJson, paletteColors, projectMediaRoot, iconTheme),
            ProjectTreeNodeKind.NavigationBar => FromNavigationBar(database, node, theme.TokensJson, paletteColors, projectMediaRoot, iconTheme),
            _ => null,
        };
    }

    private static DesignPreviewPayload FromStatusBar(
        SpikeDatabase database,
        ProjectTreeNode node,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme)
    {
        var settings = database.GetStatusBarSettings(node.Id);
        return new DesignPreviewPayload(
            "statusBar",
            settings.Name,
            settings.ConfigJson,
            themeTokensJson,
            paletteColors,
            projectMediaRoot,
            iconTheme?.AssetRoot ?? "",
            iconTheme?.MappingJson ?? "{}");
    }

    private static DesignPreviewPayload FromNavigationBar(
        SpikeDatabase database,
        ProjectTreeNode node,
        string themeTokensJson,
        IReadOnlyDictionary<string, string> paletteColors,
        string projectMediaRoot,
        SpikeDatabase.IconThemeSettings? iconTheme)
    {
        var settings = database.GetNavigationBarSettings(node.Id);
        return new DesignPreviewPayload(
            "navigationBar",
            settings.Name,
            settings.ConfigJson,
            themeTokensJson,
            paletteColors,
            projectMediaRoot,
            iconTheme?.AssetRoot ?? "",
            iconTheme?.MappingJson ?? "{}");
    }
}
