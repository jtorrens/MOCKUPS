using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ProductionThemeContextSource(
    string Name,
    string DefaultMode);

internal sealed class ProductionShotContextDataSource
{
    private readonly IPreviewInputRepository _database;
    private readonly ActorPreviewDataSource _actorDataSource;

    public ProductionShotContextDataSource(
        IPreviewInputRepository database,
        IActorPreviewRepository actors)
    {
        _database = database;
        _actorDataSource = new ActorPreviewDataSource(actors);
    }

    public ShotSettings LoadShot(string shotId) =>
        _database.GetShotSettings(shotId);

    public ActorPreviewContextSource LoadActor(string actorId)
    {
        return _actorDataSource.LoadContext(actorId);
    }

    public string LoadDeviceName(string deviceId)
    {
        return _database.GetDeviceSettings(deviceId).Name;
    }

    public ProductionThemeContextSource LoadTheme(string themeId)
    {
        return new ProductionThemeContextSource(
            _database.GetThemeSettings(themeId).Name,
            _database.GetThemeFieldValue(themeId, "theme.defaultMode"));
    }
}
