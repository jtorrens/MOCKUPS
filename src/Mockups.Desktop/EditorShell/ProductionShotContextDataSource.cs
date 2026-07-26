using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ProductionThemeContextSource(
    string Name,
    string DefaultMode);

internal sealed class ProductionShotContextDataSource
{
    private readonly SpikeDatabase _database;
    private readonly ActorPreviewDataSource _actorDataSource;

    public ProductionShotContextDataSource(SpikeDatabase database)
    {
        _database = database;
        _actorDataSource = new ActorPreviewDataSource(database);
    }

    public string LoadShotOwnerActorId(string shotId)
    {
        return _database.GetShotSettings(shotId).OwnerActorId;
    }

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
