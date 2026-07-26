using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ProductionPreviewSessionDataSource
{
    private readonly IPreviewInputRepository _database;
    private readonly IModuleInstanceTimelineStore _timeline;

    public ProductionPreviewSessionDataSource(
        IPreviewInputRepository database,
        IModuleInstanceTimelineStore timeline)
    {
        _database = database;
        _timeline = timeline;
    }

    public string ModuleInstanceShotId(string moduleInstanceId)
    {
        return _timeline.GetModuleInstanceSettings(moduleInstanceId).ShotId;
    }

    public int ShotFrameRate(string shotId)
    {
        return _database.GetShotSettings(shotId).Fps;
    }

    public string ModuleInstanceVariantConfigJson(string moduleInstanceId)
    {
        return _timeline
            .GetModuleInstanceVariantSettings(moduleInstanceId)
            .ConfigJson;
    }
}
