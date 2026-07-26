using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ProductionScreenPresentationSource(
    string Module,
    string Variant,
    int DurationFrames,
    string Transition);

internal sealed class ProductionScreenPresentationDataSource
{
    private readonly SpikeDatabase _database;
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;

    public ProductionScreenPresentationDataSource(SpikeDatabase database)
    {
        _database = database;
        _timelineDataSource = new ModuleInstanceTimelineDataSource(database);
    }

    public ProductionScreenPresentationSource Load(string moduleInstanceId)
    {
        return new ProductionScreenPresentationSource(
            _database.GetModuleInstanceModuleName(moduleInstanceId),
            _database.GetModuleInstanceVariantName(moduleInstanceId),
            ModuleInstanceTimeline.DurationFrames(_timelineDataSource, moduleInstanceId),
            _database.GetModuleInstanceTransitionType(moduleInstanceId));
    }
}
