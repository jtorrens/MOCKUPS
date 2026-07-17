using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorThemeNavigationSource(
    string Family,
    string IconThemeId,
    string StatusBarId,
    string NavigationBarId);

internal sealed class EditorPresentationContextDataSource
{
    private readonly SpikeDatabase _database;

    public EditorPresentationContextDataSource(SpikeDatabase database)
    {
        _database = database;
    }

    public string ProjectMediaRoot(string projectId)
    {
        return _database.GetProjectSettings(projectId).MediaRoot;
    }

    public EditorThemeNavigationSource ThemeNavigation(string themeId)
    {
        var settings = _database.GetThemeSettings(themeId);
        return new EditorThemeNavigationSource(
            settings.Family,
            settings.IconThemeId,
            settings.StatusBarId,
            settings.NavigationBarId);
    }

    public string ProductionFontFiles(string productionFontId)
    {
        return _database.GetProductionFontFieldValue(productionFontId, "font.files");
    }
}
