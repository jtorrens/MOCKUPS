using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ModuleInstanceAnimationSource(
    string VariantConfigJson,
    string AnimationJson,
    string RuntimePreviewJson,
    string ThemeTokensJson,
    string EffectiveContractJson);

internal sealed class ModuleInstanceAnimationDocumentStore
{
    private readonly SpikeDatabase _database;
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;

    public ModuleInstanceAnimationDocumentStore(
        SpikeDatabase database,
        ModuleInstanceTimelineDataSource timelineDataSource)
    {
        _database = database;
        _timelineDataSource = timelineDataSource;
    }

    public ModuleInstanceAnimationSource Load(string moduleInstanceId)
    {
        var timeline = _timelineDataSource.Load(moduleInstanceId);
        return new ModuleInstanceAnimationSource(
            _database.GetModuleInstanceVariantSettings(moduleInstanceId).ConfigJson,
            timeline.AnimationJson,
            timeline.RuntimePreviewJson,
            timeline.ThemeTokensJson,
            timeline.EffectiveContractJson);
    }

    public string SaveAnimationJson(string moduleInstanceId, string animationJson)
    {
        _database.UpdateModuleInstanceAnimationJson(moduleInstanceId, animationJson);
        return _timelineDataSource.Load(moduleInstanceId).AnimationJson;
    }
}
