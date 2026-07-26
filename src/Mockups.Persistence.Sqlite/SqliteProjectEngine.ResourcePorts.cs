using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public ActorSettings GetActorSettings(string actorId) =>
        _resourceOwner.GetActorSettings(actorId);

    public void UpdateActorField(
        string actorId,
        string fieldId,
        string value) =>
        _resourceOwner.UpdateActorField(actorId, fieldId, value);

    public string GetActorFieldValue(string actorId, string fieldId) =>
        _resourceOwner.GetActorFieldValue(actorId, fieldId);

    public IReadOnlyList<FieldOption> GetActorOptions(string projectId) =>
        _resourceOwner.GetActorOptions(projectId);

    public IReadOnlyList<FieldOption> GetRequiredActorOptions(
        string projectId) =>
        _resourceOwner.GetRequiredActorOptions(projectId);

    public DevicePreviewMetrics GetDevicePreviewMetrics(string deviceId) =>
        _resourceOwner.GetDevicePreviewMetrics(deviceId);

    public DeviceSettings GetDeviceSettings(string deviceId) =>
        _resourceOwner.GetDeviceSettings(deviceId);

    public void UpdateDeviceField(
        string deviceId,
        string fieldId,
        string value) =>
        _resourceOwner.UpdateDeviceField(deviceId, fieldId, value);

    public string GetDeviceMetricFieldValue(
        string deviceId,
        string fieldId) =>
        _resourceOwner.GetDeviceMetricFieldValue(deviceId, fieldId);

    public IReadOnlyList<FieldOption> GetDeviceOptions(string projectId) =>
        _resourceOwner.GetDeviceOptions(projectId);

    public PaletteColorSettings GetPaletteColorSettings(string colorId) =>
        _resourceOwner.GetPaletteColorSettings(colorId);

    public void UpdatePaletteColorField(
        string colorId,
        string fieldId,
        string value) =>
        _resourceOwner.UpdatePaletteColorField(colorId, fieldId, value);

    public IReadOnlyList<FieldOption> GetPaletteColorOptions(
        string projectId) =>
        _resourceOwner.GetPaletteColorOptions(projectId);

    public IReadOnlyDictionary<string, string> GetPaletteColorMap(
        string projectId) =>
        _resourceOwner.GetPaletteColorMap(projectId);

    public IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(
        string projectId) =>
        _resourceOwner.GetPaletteNeutralMap(projectId);

    public IReadOnlyList<FieldOption> GetThemeOptions(string projectId) =>
        _resourceOwner.GetThemeOptions(projectId);

    public IReadOnlyList<ThemeTokenOption> GetThemeTokenOptions(
        string projectId,
        string themeId) =>
        _resourceOwner.GetThemeTokenOptions(projectId, themeId);

    public ThemeSettings GetThemeSettings(string themeId) =>
        _resourceOwner.GetThemeSettings(themeId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId) =>
        _resourceOwner.GetModuleInstanceThemeTokensJson(moduleInstanceId);

    public string GetThemeFieldValue(string themeId, string fieldId) =>
        _resourceOwner.GetThemeFieldValue(themeId, fieldId);

    public void UpdateThemeField(
        string themeId,
        string fieldId,
        string value) =>
        _resourceOwner.UpdateThemeField(themeId, fieldId, value);

    public IReadOnlyList<FieldOption> GetProductionFontOptions(
        string projectId,
        string? category = null) =>
        _resourceOwner.GetProductionFontOptions(projectId, category);

    public IReadOnlyList<ProductionFontFace> GetProductionFontFaces(
        string projectId) =>
        _resourceOwner.GetProductionFontFaces(projectId);

    public ProjectTreeNode ImportProductionFont(
        ProjectTreeNode fontsRoot,
        IReadOnlyList<string> selectedFilePaths) =>
        _resourceOwner.ImportProductionFont(fontsRoot, selectedFilePaths);

    public ProductionFontSettings GetProductionFontSettings(string fontId) =>
        _resourceOwner.GetProductionFontSettings(fontId);

    public string GetProductionFontFieldValue(
        string fontId,
        string fieldId) =>
        _resourceOwner.GetProductionFontFieldValue(fontId, fieldId);

    public void UpdateProductionFontField(
        string fontId,
        string fieldId,
        string value) =>
        _resourceOwner.UpdateProductionFontField(fontId, fieldId, value);

    public IReadOnlyList<FieldOption> GetIconThemeOptions(
        string projectId) =>
        _resourceOwner.GetIconThemeOptions(projectId);

    public IconThemeSettings GetIconThemeSettings(string iconThemeId) =>
        _resourceOwner.GetIconThemeSettings(iconThemeId);

    public string GetIconThemeFieldValue(
        string iconThemeId,
        string fieldId) =>
        _resourceOwner.GetIconThemeFieldValue(iconThemeId, fieldId);

    public IReadOnlyList<IconThemeToken> GetIconThemeTokens(
        string iconThemeId) =>
        _resourceOwner.GetIconThemeTokens(iconThemeId);

    public IReadOnlyList<FieldOption> GetIconTokenOptions(
        string projectId,
        string? currentToken = null) =>
        _resourceOwner.GetIconTokenOptions(projectId, currentToken);

    public string ResolveIconThemeAssetPath(
        string iconThemeId,
        string file) =>
        _resourceOwner.ResolveIconThemeAssetPath(iconThemeId, file);

    public IconThemeRefreshResult RefreshIconThemeSets(
        ProjectTreeNode iconThemesRoot) =>
        _resourceOwner.RefreshIconThemeSets(iconThemesRoot);

    public IconThemeRefreshResult RefreshIconThemeSetsForTheme(
        string iconThemeId) =>
        _resourceOwner.RefreshIconThemeSetsForTheme(iconThemeId);

    public void DeleteIconThemeToken(
        string iconThemeId,
        string token) =>
        _resourceOwner.DeleteIconThemeToken(iconThemeId, token);

    public IconThemeTokenSvg ReadIconThemeTokenSvg(
        string iconThemeId,
        string token) =>
        _resourceOwner.ReadIconThemeTokenSvg(iconThemeId, token);

    public IconThemeReplaceSvgResult ReplaceIconThemeTokenSvg(
        string iconThemeId,
        string token,
        string svgText) =>
        _resourceOwner.ReplaceIconThemeTokenSvg(
            iconThemeId,
            token,
            svgText);

    public IconThemeWriteAllSvgResult WriteIconThemeTokenSvgToAllSets(
        string iconThemeId,
        string token,
        string svgText,
        string description) =>
        _resourceOwner.WriteIconThemeTokenSvgToAllSets(
            iconThemeId,
            token,
            svgText,
            description);

    public IconThemeReplaceSvgResult ReplaceIconThemeTokenSvgFromFile(
        string iconThemeId,
        string token,
        string sourcePath) =>
        _resourceOwner.ReplaceIconThemeTokenSvgFromFile(
            iconThemeId,
            token,
            sourcePath);

    public IconThemeSearchResult SearchIconThemeSources(
        string query,
        CancellationToken cancellationToken = default) =>
        _resourceOwner.SearchIconThemeSources(query, cancellationToken);

    public IconThemeGenerateResult GenerateIconThemeToken(
        string iconThemeId,
        string token,
        string category,
        string description,
        string lucideSource,
        string materialSource,
        CancellationToken cancellationToken = default) =>
        _resourceOwner.GenerateIconThemeToken(
            iconThemeId,
            token,
            category,
            description,
            lucideSource,
            materialSource,
            cancellationToken);
}
