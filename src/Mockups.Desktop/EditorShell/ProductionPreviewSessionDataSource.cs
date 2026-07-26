using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ProductionPreviewSessionDataSource
{
    private readonly SpikeDatabase _database;

    public ProductionPreviewSessionDataSource(SpikeDatabase database)
    {
        _database = database;
    }

    public string ModuleInstanceShotId(string moduleInstanceId)
    {
        return _database.GetModuleInstanceSettings(moduleInstanceId).ShotId;
    }

    public int ShotFrameRate(string shotId)
    {
        return _database.GetShotSettings(shotId).Fps;
    }

    public string ModuleInstanceVariantConfigJson(string moduleInstanceId)
    {
        return _database.GetModuleInstanceVariantSettings(moduleInstanceId).ConfigJson;
    }
}
