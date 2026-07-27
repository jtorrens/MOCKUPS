using Mockups.DesktopEditorShell.Data;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ModuleInstanceAnimationSource(
    string VariantConfigJson,
    string AnimationJson,
    string RuntimePreviewJson,
    string ThemeTokensJson,
    string EffectiveContractJson);

internal sealed record ModuleInstanceAnimationSnapshot(
    string ModuleInstanceId,
    ModuleInstanceAnimationSource Source,
    int ScreenStartFrame,
    int DurationFrames);

internal sealed class ModuleInstanceAnimationDocumentStore
{
    private readonly IModuleInstanceAnimationStore _database;
    private readonly IModuleInstanceThemeTokenQuery _themeTokens;
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;
    private readonly EditorOperationCoordinator _operations;

    public ModuleInstanceAnimationDocumentStore(
        IModuleInstanceAnimationStore database,
        IModuleInstanceThemeTokenQuery themeTokens,
        ModuleInstanceTimelineDataSource timelineDataSource,
        EditorOperationCoordinator operations)
    {
        _database = database;
        _themeTokens = themeTokens;
        _timelineDataSource = timelineDataSource;
        _operations = operations;
    }

    public ModuleInstanceAnimationSource Load(string moduleInstanceId)
    {
        var timeline = _timelineDataSource.Load(moduleInstanceId);
        return new ModuleInstanceAnimationSource(
            _database.GetModuleInstanceVariantSettings(moduleInstanceId).ConfigJson,
            timeline.AnimationJson,
            timeline.RuntimePreviewJson,
            _themeTokens.GetModuleInstanceThemeTokensJson(
                moduleInstanceId),
            timeline.EffectiveContractJson);
    }

    public ModuleInstanceAnimationSnapshot LoadSnapshot(
        string moduleInstanceId)
    {
        return new ModuleInstanceAnimationSnapshot(
            moduleInstanceId,
            Load(moduleInstanceId),
            ModuleInstanceTimeline.ScreenStartFrame(
                _timelineDataSource,
                moduleInstanceId),
            System.Math.Max(
                1,
                ModuleInstanceTimeline.DurationFrames(
                    _timelineDataSource,
                    moduleInstanceId)));
    }

    public Task<string> SaveAnimationJsonAsync(
        string moduleInstanceId,
        string animationJson) =>
        _operations.ExecuteAsync(
            () =>
            {
                _database.UpdateModuleInstanceAnimationJson(
                    moduleInstanceId,
                    animationJson);
                return _timelineDataSource.Load(
                    moduleInstanceId).AnimationJson;
            });

    public Task<ModuleInstanceAnimationSnapshot>
        SaveAnimationSnapshotAsync(
            string moduleInstanceId,
            string animationJson) =>
        _operations.ExecuteAsync(
            () =>
            {
                _database.UpdateModuleInstanceAnimationJson(
                    moduleInstanceId,
                    animationJson);
                return LoadSnapshot(moduleInstanceId);
            });
}
