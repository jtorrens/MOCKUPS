namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public AppSettings GetAppSettings(string appId) =>
        _designOwner.GetAppSettings(appId);

    public void UpdateAppField(
        string appId,
        string fieldId,
        string value) =>
        _designOwner.UpdateAppField(appId, fieldId, value);

    public string GetAppConfigFieldValue(
        string appId,
        string fieldId) =>
        _designOwner.GetAppConfigFieldValue(appId, fieldId);

    public string GetAppMetadataFieldValue(
        string appId,
        string fieldId) =>
        _designOwner.GetAppMetadataFieldValue(appId, fieldId);

    public ModuleSettings GetModuleSettings(string moduleId) =>
        _designOwner.GetModuleSettings(moduleId);

    public void UpdateModuleDesignPreviewJson(
        string moduleId,
        string designPreviewJson) =>
        _designOwner.UpdateModuleDesignPreviewJson(
            moduleId,
            designPreviewJson);

    public AppSettings GetModuleAppSettings(string moduleId) =>
        _designOwner.GetModuleAppSettings(moduleId);

    public string GetModuleConfigFieldValue(
        string moduleId,
        string fieldId) =>
        _designOwner.GetModuleConfigFieldValue(moduleId, fieldId);

    public void UpdateModuleField(
        string moduleId,
        string fieldId,
        string value) =>
        _designOwner.UpdateModuleField(
            moduleId,
            fieldId,
            value);
}
