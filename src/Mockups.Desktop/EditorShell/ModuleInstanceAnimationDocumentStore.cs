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
    int ActionStartFrame,
    int DurationFrames);

internal sealed class ModuleInstanceAnimationDocumentStore
{
    private readonly IModuleInstanceAnimationStore _database;
    private readonly IModuleInstanceTimelineStore _timeline;
    private readonly IModuleInstanceThemeTokenQuery _themeTokens;
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;
    private readonly EditorOperationCoordinator _operations;

    public ModuleInstanceAnimationDocumentStore(
        IModuleInstanceAnimationStore database,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery themeTokens,
        ModuleInstanceTimelineDataSource timelineDataSource,
        EditorOperationCoordinator operations)
    {
        _database = database;
        _timeline = timeline;
        _themeTokens = themeTokens;
        _timelineDataSource = timelineDataSource;
        _operations = operations;
    }

    public ModuleInstanceAnimationSource Load(string moduleInstanceId)
    {
        var timeline = _timelineDataSource.Load(moduleInstanceId);
        return new ModuleInstanceAnimationSource(
            _timeline.GetModuleInstanceVariantSettings(
                moduleInstanceId).ConfigJson,
            timeline.AnimationJson,
            timeline.RuntimePreviewJson,
            _themeTokens.GetModuleInstanceThemeTokensJson(
                moduleInstanceId),
            timeline.EffectiveContractJson);
    }

    public ModuleInstanceAnimationSnapshot LoadSnapshot(
        string moduleInstanceId)
    {
        var range =
            ModuleInstanceTimeline.ScreenRange(
                _timelineDataSource,
                moduleInstanceId);
        return new ModuleInstanceAnimationSnapshot(
            moduleInstanceId,
            Load(moduleInstanceId),
            range.StartFrame,
            range.StartFrame
                + range.ActionStartFrame,
            System.Math.Max(
                1,
                range.ActionDurationFrames));
    }

    public Task<ModuleInstanceAnimationSnapshot>
        ExecuteMutationAsync(
            string moduleInstanceId,
            System.Func<ModuleInstanceAnimationDocument, bool>
                mutation) =>
        _operations.ExecuteAsync(
            () =>
            {
                var current = LoadSnapshot(moduleInstanceId);
                var candidate =
                    new ModuleInstanceAnimationDocument(
                        current.Source.AnimationJson);
                if (!mutation(candidate))
                {
                    return current;
                }
                _database.UpdateModuleInstanceAnimationJson(
                    moduleInstanceId,
                    candidate.ToJson());
                return LoadSnapshot(moduleInstanceId);
            });
}
