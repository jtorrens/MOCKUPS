using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ProductionScreenPresentationSource(
    string Module,
    string Variant,
    int DurationFrames,
    string Transition);

internal sealed class ProductionScreenPresentationDataSource
{
    private readonly IPreviewInputRepository _database;
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;
    private readonly IModuleInstanceTimelineStore _timeline;

    public ProductionScreenPresentationDataSource(
        IPreviewInputRepository database,
        IModuleInstanceTimelineStore timeline)
    {
        _database = database;
        _timeline = timeline;
        _timelineDataSource =
            new ModuleInstanceTimelineDataSource(timeline);
    }

    public ProductionScreenPresentationSource Load(string moduleInstanceId)
    {
        return new ProductionScreenPresentationSource(
            _timeline.GetModuleInstanceModuleName(moduleInstanceId),
            _database.GetModuleInstanceVariantName(moduleInstanceId),
            ModuleInstanceTimeline.DurationFrames(_timelineDataSource, moduleInstanceId),
            _timeline.GetModuleInstanceTransitionType(moduleInstanceId));
    }
}
