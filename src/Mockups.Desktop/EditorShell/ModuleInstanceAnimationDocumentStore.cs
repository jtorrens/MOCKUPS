using Mockups.DesktopEditorShell.Data;
using System.Threading.Tasks;
using System.Diagnostics;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ModuleInstanceAnimationSource(
    string VariantConfigJson,
    string AnimationJson,
    string RuntimePreviewJson,
    string ThemeTokensJson,
    string EffectiveContractJson,
    int FrameRate);

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
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;
    private readonly EditorOperationCoordinator _operations;

    public ModuleInstanceAnimationDocumentStore(
        IModuleInstanceAnimationStore database,
        IModuleInstanceTimelineStore timeline,
        ModuleInstanceTimelineDataSource timelineDataSource,
        EditorOperationCoordinator operations)
    {
        _database = database;
        _timeline = timeline;
        _timelineDataSource = timelineDataSource;
        _operations = operations;
    }

    public ModuleInstanceAnimationSource Load(string moduleInstanceId)
    {
        var timeline = _timelineDataSource.Load(moduleInstanceId);
        return LoadSource(moduleInstanceId, timeline);
    }

    private ModuleInstanceAnimationSource LoadSource(
        string moduleInstanceId, ModuleInstanceTimelineSource timeline) =>
        new ModuleInstanceAnimationSource(
            _timeline.GetModuleInstanceVariantSettings(
                moduleInstanceId).ConfigJson,
            timeline.AnimationJson,
            timeline.RuntimePreviewJson,
            timeline.ThemeTokensJson,
            timeline.EffectiveContractJson,
            timeline.FrameRate);

    public ModuleInstanceAnimationSnapshot LoadSnapshot(
        string moduleInstanceId)
    {
        var timeline = _timelineDataSource.Load(moduleInstanceId);
        var range =
            ModuleInstanceTimeline.ScreenRange(
                _timelineDataSource,
                moduleInstanceId, timeline);
        return new ModuleInstanceAnimationSnapshot(
            moduleInstanceId,
            LoadSource(moduleInstanceId, timeline),
            range.StartFrame,
            range.StartFrame
                + range.ActionStartFrame,
            System.Math.Max(
                1,
                range.ActionDurationFrames));
    }

    public Task<ModuleInstanceAnimationSnapshot> ExecuteMutationAsync(
        string moduleInstanceId,
        System.Func<ModuleInstanceAnimationDocument, bool> mutation)
    {
        var timer = Stopwatch.StartNew();
        return _operations.ExecuteAsync(() =>
        {
            var queueMs = timer.Elapsed.TotalMilliseconds;
            var currentJson = _timeline.GetModuleInstanceSettings(moduleInstanceId).AnimationJson;
            var candidate = new ModuleInstanceAnimationDocument(currentJson);
            var changed = mutation(candidate);
            var preparedMs = timer.Elapsed.TotalMilliseconds;
            if (changed)
                _database.UpdateModuleInstanceAnimationJson(moduleInstanceId, candidate.ToJson());
            var savedMs = timer.Elapsed.TotalMilliseconds;
            var snapshot = LoadSnapshot(moduleInstanceId);
            PreviewDebugLog.Write("animation.mutation",
                ("screen", moduleInstanceId), ("queueMs", queueMs),
                ("prepareMs", preparedMs - queueMs),
                ("saveMs", savedMs - preparedMs),
                ("snapshotMs", timer.Elapsed.TotalMilliseconds - savedMs));
            return snapshot;
        });
    }
}
