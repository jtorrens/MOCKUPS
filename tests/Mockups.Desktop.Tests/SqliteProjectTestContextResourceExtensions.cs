using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteProjectTestContextResourceExtensions
{
    internal static ActorSettings GetActorSettings(
        this SqliteProjectTestContext engine,
        string actorId) =>
        engine.Resources.GetActorSettings(actorId);

    internal static void UpdateActorField(
        this SqliteProjectTestContext engine,

        string actorId,
        string fieldId,
        string value) =>
        engine.Resources.UpdateActorField(actorId, fieldId, value);

    internal static string GetActorFieldValue(
        this SqliteProjectTestContext engine,
        string actorId, string fieldId) =>
        engine.Resources.GetActorFieldValue(actorId, fieldId);

    internal static IReadOnlyList<FieldOption> GetActorOptions(
        this SqliteProjectTestContext engine,
        string projectId) =>
        engine.Resources.GetActorOptions(projectId);

    internal static IReadOnlyList<FieldOption> GetRequiredActorOptions(
        this SqliteProjectTestContext engine,

        string projectId) =>
        engine.Resources.GetRequiredActorOptions(projectId);

    internal static DevicePreviewMetrics GetDevicePreviewMetrics(
        this SqliteProjectTestContext engine,
        string deviceId) =>
        engine.Resources.GetDevicePreviewMetrics(deviceId);

    internal static DeviceSettings GetDeviceSettings(
        this SqliteProjectTestContext engine,
        string deviceId) =>
        engine.Resources.GetDeviceSettings(deviceId);

    internal static void UpdateDeviceField(
        this SqliteProjectTestContext engine,

        string deviceId,
        string fieldId,
        string value) =>
        engine.Resources.UpdateDeviceField(deviceId, fieldId, value);

    internal static string GetDeviceMetricFieldValue(
        this SqliteProjectTestContext engine,

        string deviceId,
        string fieldId) =>
        engine.Resources.GetDeviceMetricFieldValue(deviceId, fieldId);

    internal static IReadOnlyList<FieldOption> GetDeviceOptions(
        this SqliteProjectTestContext engine,
        string projectId) =>
        engine.Resources.GetDeviceOptions(projectId);

    internal static PaletteColorSettings GetPaletteColorSettings(
        this SqliteProjectTestContext engine,
        string colorId) =>
        engine.Resources.GetPaletteColorSettings(colorId);

    internal static void UpdatePaletteColorField(
        this SqliteProjectTestContext engine,

        string colorId,
        string fieldId,
        string value) =>
        engine.Resources.UpdatePaletteColorField(colorId, fieldId, value);

    internal static IReadOnlyList<FieldOption> GetPaletteColorOptions(
        this SqliteProjectTestContext engine,

        string projectId) =>
        engine.Resources.GetPaletteColorOptions(projectId);

    internal static IReadOnlyDictionary<string, string> GetPaletteColorMap(
        this SqliteProjectTestContext engine,

        string projectId) =>
        engine.Resources.GetPaletteColorMap(projectId);

    internal static IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(
        this SqliteProjectTestContext engine,

        string projectId) =>
        engine.Resources.GetPaletteNeutralMap(projectId);

    internal static IReadOnlyList<FieldOption> GetThemeOptions(
        this SqliteProjectTestContext engine,
        string projectId) =>
        engine.Resources.GetThemeOptions(projectId);

    internal static IReadOnlyList<ThemeTokenOption> GetThemeTokenOptions(
        this SqliteProjectTestContext engine,

        string projectId,
        string themeId) =>
        engine.Resources.GetThemeTokenOptions(projectId, themeId);

    internal static ThemeSettings GetThemeSettings(
        this SqliteProjectTestContext engine,
        string themeId) =>
        engine.Resources.GetThemeSettings(themeId);

    internal static string GetModuleInstanceThemeTokensJson(
        this SqliteProjectTestContext engine,

        string moduleInstanceId) =>
        engine.Resources.GetModuleInstanceThemeTokensJson(moduleInstanceId);

    internal static string GetThemeFieldValue(
        this SqliteProjectTestContext engine,
        string themeId, string fieldId) =>
        engine.Resources.GetThemeFieldValue(themeId, fieldId);

    internal static void UpdateThemeField(
        this SqliteProjectTestContext engine,

        string themeId,
        string fieldId,
        string value) =>
        engine.Resources.UpdateThemeField(themeId, fieldId, value);

    internal static IReadOnlyList<FieldOption> GetProductionFontOptions(
        this SqliteProjectTestContext engine,

        string projectId,
        string? category = null) =>
        engine.Resources.GetProductionFontOptions(projectId, category);

    internal static IReadOnlyList<ProductionFontFace> GetProductionFontFaces(
        this SqliteProjectTestContext engine,

        string projectId) =>
        engine.Resources.GetProductionFontFaces(projectId);

    internal static ProjectTreeNode ImportProductionFont(
        this SqliteProjectTestContext engine,

        ProjectTreeNode fontsRoot,
        IReadOnlyList<string> selectedFilePaths) =>
        engine.Resources.ImportProductionFont(fontsRoot, selectedFilePaths);

    internal static ProductionFontSettings GetProductionFontSettings(
        this SqliteProjectTestContext engine,
        string fontId) =>
        engine.Resources.GetProductionFontSettings(fontId);

    internal static string GetProductionFontFieldValue(
        this SqliteProjectTestContext engine,

        string fontId,
        string fieldId) =>
        engine.Resources.GetProductionFontFieldValue(fontId, fieldId);

    internal static void UpdateProductionFontField(
        this SqliteProjectTestContext engine,

        string fontId,
        string fieldId,
        string value) =>
        engine.Resources.UpdateProductionFontField(fontId, fieldId, value);

    internal static IReadOnlyList<FieldOption> GetIconThemeOptions(
        this SqliteProjectTestContext engine,

        string projectId) =>
        engine.Resources.GetIconThemeOptions(projectId);

    internal static IconThemeSettings GetIconThemeSettings(
        this SqliteProjectTestContext engine,
        string iconThemeId) =>
        engine.Resources.GetIconThemeSettings(iconThemeId);

    internal static string GetIconThemeFieldValue(
        this SqliteProjectTestContext engine,

        string iconThemeId,
        string fieldId) =>
        engine.Resources.GetIconThemeFieldValue(iconThemeId, fieldId);

    internal static IReadOnlyList<IconThemeToken> GetIconThemeTokens(
        this SqliteProjectTestContext engine,

        string iconThemeId) =>
        engine.Resources.GetIconThemeTokens(iconThemeId);

    internal static IReadOnlyList<FieldOption> GetIconTokenOptions(
        this SqliteProjectTestContext engine,

        string projectId,
        string? currentToken = null) =>
        engine.Resources.GetIconTokenOptions(projectId, currentToken);

    internal static string ResolveIconThemeAssetPath(
        this SqliteProjectTestContext engine,

        string iconThemeId,
        string file) =>
        engine.Resources.ResolveIconThemeAssetPath(iconThemeId, file);

    internal static IconThemeRefreshResult RefreshIconThemeSets(
        this SqliteProjectTestContext engine,

        ProjectTreeNode iconThemesRoot) =>
        engine.Resources.RefreshIconThemeSets(iconThemesRoot);

    internal static IconThemeRefreshResult RefreshIconThemeSetsForTheme(
        this SqliteProjectTestContext engine,

        string iconThemeId) =>
        engine.Resources.RefreshIconThemeSetsForTheme(iconThemeId);

    internal static void DeleteIconThemeToken(
        this SqliteProjectTestContext engine,

        string iconThemeId,
        string token) =>
        engine.Resources.DeleteIconThemeToken(iconThemeId, token);

    internal static IconThemeTokenSvg ReadIconThemeTokenSvg(
        this SqliteProjectTestContext engine,

        string iconThemeId,
        string token) =>
        engine.Resources.ReadIconThemeTokenSvg(iconThemeId, token);

    internal static IconThemeReplaceSvgResult ReplaceIconThemeTokenSvg(
        this SqliteProjectTestContext engine,

        string iconThemeId,
        string token,
        string svgText) =>
        engine.Resources.ReplaceIconThemeTokenSvg(
            iconThemeId,
            token,
            svgText);

    internal static IconThemeWriteAllSvgResult WriteIconThemeTokenSvgToAllSets(
        this SqliteProjectTestContext engine,

        string iconThemeId,
        string token,
        string svgText,
        string description) =>
        engine.Resources.WriteIconThemeTokenSvgToAllSets(
            iconThemeId,
            token,
            svgText,
            description);

    internal static IconThemeReplaceSvgResult ReplaceIconThemeTokenSvgFromFile(
        this SqliteProjectTestContext engine,

        string iconThemeId,
        string token,
        string sourcePath) =>
        engine.Resources.ReplaceIconThemeTokenSvgFromFile(
            iconThemeId,
            token,
            sourcePath);

    internal static IconThemeSearchResult SearchIconThemeSources(
        this SqliteProjectTestContext engine,

        string query,
        CancellationToken cancellationToken = default) =>
        engine.Resources.SearchIconThemeSources(query, cancellationToken);

    internal static IconThemeGenerateResult GenerateIconThemeToken(
        this SqliteProjectTestContext engine,

        string iconThemeId,
        string token,
        string category,
        string description,
        string lucideSource,
        string materialSource,
        CancellationToken cancellationToken = default) =>
        engine.Resources.GenerateIconThemeToken(
            iconThemeId,
            token,
            category,
            description,
            lucideSource,
            materialSource,
            cancellationToken);
}
