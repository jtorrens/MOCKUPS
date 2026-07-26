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
}
