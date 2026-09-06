using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ProductionOutput;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record RenderQueueRouteOption(
    string EntryId,
    string RelativeDirectory,
    int VersionPadding,
    int FramePadding);

internal sealed record RenderSnapshotFreezeProgress(
    int Current,
    int Total,
    string Appearance,
    string ScreenName);

internal sealed record RenderPreparationScreenRange(
    string ScreenId,
    string ScreenName,
    int StartFrame,
    int DurationFrames,
    string DeviceOverridesJson);

internal sealed record RenderBatchSnapshotPreparation(
    RenderQueueShotDraft Draft,
    string DeviceId,
    string DeviceName,
    string ThemeStrategy,
    string ThemeId,
    string ThemeName,
    DevicePreviewMetrics Metrics,
    IReadOnlyList<RenderJobSummary> Summaries);

internal sealed record RenderBatchPlan(
    IReadOnlyList<RenderJobPlan> Plans,
    IReadOnlyList<RenderJobSummary> Summaries);

internal sealed record RenderQueueShotDraft(
    ProjectTreeNode Shot,
    string ProjectId,
    string ActorId,
    string ActorName,
    string DeviceId,
    string ThemeSelectionValue,
    int ShotNumber,
    int Fps,
    int TotalFrames,
    IReadOnlyList<RenderPreparationScreenRange> Screens,
    string SuggestedBaseName,
    string RootPath,
    string RouteStatusMessage,
    IReadOnlyList<FieldOption> Devices,
    IReadOnlyList<FieldOption> ThemeOptions,
    IReadOnlyList<RenderQueueRouteOption> Routes);

internal static class RenderThemeSelection
{
    public const string ScreenValue = "render-theme-selection::screen";
    public const string ScreenLabel = "Screen";

    public static IReadOnlyList<FieldOption> Options(
        IReadOnlyList<FieldOption> themes) =>
        [new FieldOption(ScreenValue, ScreenLabel), .. themes];

    public static (string Strategy, string ThemeId, string Label) Resolve(
        string value,
        IReadOnlyList<FieldOption> themes)
    {
        if (value.Equals(ScreenValue, StringComparison.Ordinal))
        {
            return (RenderThemeStrategy.Screen, "", ScreenLabel);
        }
        var theme = themes.SingleOrDefault((option) =>
            option.Value.Equals(value, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "Select Screen or a Theme from the Shot Project.");
        return (RenderThemeStrategy.Forced, theme.Value, theme.Label);
    }
}

internal sealed class RenderJobSnapshotFactory : IRenderJobPreparer
{
    private readonly IRenderSnapshotDataSource _database;
    private readonly ProductionOutputRootStore _roots;
    private readonly ProductionOutputPlanResolver _outputPlans;
    private readonly DesignPreviewPayloadDataSource _payloadData;
    private readonly ProductionPreviewPayloadPreparer _productionPayloads;

    public RenderJobSnapshotFactory(
        IRenderSnapshotDataSource database,
        IProjectPathResolver projectPaths,
        ProductionOutputRootStore? roots = null,
        ShotManagerDocumentStore? shotManagerDocuments = null)
    {
        _database = database;
        _roots = roots ?? new ProductionOutputRootStore();
        _outputPlans = new ProductionOutputPlanResolver(
            _roots,
            shotManagerDocuments ?? new ShotManagerDocumentStore());
        _payloadData = new DesignPreviewPayloadDataSource(
            database,
            database,
            database,
            database,
            projectPaths);
        _productionPayloads = new ProductionPreviewPayloadPreparer(
            _payloadData,
            new ProductionPreviewRuntimeResolver(database, projectPaths));
    }

    public Task<RenderQueueShotDraft> LoadDraftAsync(
        ProjectTreeNode shot,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => LoadDraft(shot, cancellationToken),
            cancellationToken);
    }

    private RenderQueueShotDraft LoadDraft(
        ProjectTreeNode shot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (shot.Kind != ProjectTreeNodeKind.Shot)
        {
            throw new InvalidOperationException(
                "Render Queue can only add a concrete Shot.");
        }
        var shotSettings = _database.GetShotSettings(shot.Id);
        var actor = _database.GetActorSettings(shotSettings.OwnerActorId);
        var deviceId = shotSettings.EffectiveDeviceId(actor.DefaultDeviceId);
        var actorThemeId = actor.DefaultThemeId;
        if (string.IsNullOrWhiteSpace(deviceId)
            || string.IsNullOrWhiteSpace(actorThemeId))
        {
            throw new InvalidOperationException(
                $"Actor '{actor.DisplayName}' must define a default Device and Theme before rendering.");
        }
        var resolvedOutput = _outputPlans.Resolve(
            _database.GetProductionOutputShotContext(shot.Id));
        var plan = resolvedOutput.Plan;
        var screenNodes = shot.Children
            .Where((node) =>
                node.Kind
                == ProjectTreeNodeKind.ModuleInstance)
            .ToDictionary(
                (node) => node.Id,
                StringComparer.Ordinal);
        var timeline = new ModuleInstanceTimelineDataSource(
            _database,
            _database);
        var screens = ModuleInstanceTimeline
            .ScreenRanges(timeline, shot.Id)
            .Select((range) =>
            {
                var screen = screenNodes.TryGetValue(
                    range.ScreenId,
                    out var exact)
                        ? exact
                        : throw new InvalidOperationException(
                            $"Render preparation Screen '{range.ScreenId}' is not an exact child of Shot '{shot.Id}'.");
                return new RenderPreparationScreenRange(
                    range.ScreenId,
                    screen.Name,
                    range.StartFrame,
                    range.EffectiveDurationFrames,
                    _database.GetModuleInstanceSettings(
                        range.ScreenId).DeviceOverridesJson);
            })
            .ToList();
        if (screens.Count == 0)
        {
            throw new InvalidOperationException(
                $"Render preparation Shot '{shot.Id}' has no Screens.");
        }
        var rootPath = resolvedOutput.RootPath;
        var status = string.IsNullOrWhiteSpace(rootPath)
            ? resolvedOutput.IsShotManaged
                ? "Choose this workstation's Shot Manager production.json before rendering."
                : "Configure this Project's local Production Output root before rendering."
            : "";
        cancellationToken.ThrowIfCancellationRequested();
        return new RenderQueueShotDraft(
            shot,
            shotSettings.ProjectId,
            shotSettings.OwnerActorId,
            actor.DisplayName,
            deviceId,
            RenderThemeSelection.ScreenValue,
            shotSettings.ShotNumber,
            shotSettings.Fps,
            shotSettings.DurationFrames,
            screens,
            plan.TechnicalName,
            rootPath,
            status,
            _database.GetDeviceOptions(shotSettings.ProjectId),
            RenderThemeSelection.Options(
                _database.GetThemeOptions(shotSettings.ProjectId)),
            [
                new RenderQueueRouteOption(
                    plan.RouteId,
                    plan.RelativeDirectory,
                    plan.VersionPadding,
                    plan.FramePadding),
            ]);
    }

    public RenderBatchPlan PlanBatch(
        RenderQueueShotDraft draft,
        string deviceId,
        string themeSelectionValue,
        string appearance,
        string outputModeId,
        string structureEntryId,
        string baseName,
        RenderOutputPlan outputPlan,
        bool overwriteExisting)
    {
        var device = draft.Devices.SingleOrDefault((option) =>
            option.Value.Equals(deviceId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "Select a Device from the Shot Project.");
        var theme = RenderThemeSelection.Resolve(
            themeSelectionValue,
            draft.ThemeOptions.Where((option) =>
                !option.Value.Equals(
                    RenderThemeSelection.ScreenValue,
                    StringComparison.Ordinal)).ToList());
        var route = draft.Routes.SingleOrDefault((candidate) =>
            candidate.EntryId.Equals(
                structureEntryId,
                StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "Select the configured Production Output route.");
        var appearances = RenderQueueAppearance.Expand(appearance);
        if (!appearances.ToHashSet(StringComparer.Ordinal)
                .SetEquals(outputPlan.OutputPaths.Keys))
        {
            throw new InvalidOperationException(
                "The output version plan does not match the selected appearances.");
        }
        var safeBaseName = RenderOutputPlanner.RequireBaseName(baseName);
        var context = new RenderShotContext(
            draft.ProjectId,
            draft.Shot.Id,
            draft.Shot.Name,
            draft.ActorId,
            draft.ActorName);
        var summaries = appearances.Select((requestedAppearance) =>
        {
            var output = new RenderOutputTarget(
                draft.ProjectId,
                route.EntryId,
                draft.RootPath,
                route.RelativeDirectory,
                safeBaseName,
                requestedAppearance,
                outputPlan.Version,
                route.VersionPadding,
                outputModeId,
                outputPlan.OutputPaths[requestedAppearance],
                route.FramePadding,
                overwriteExisting);
            return new RenderJobSummary(
                context,
                device.Label,
                theme.Label,
                requestedAppearance,
                draft.TotalFrames,
                output);
        }).ToList();
        var plans = summaries.Select((summary) =>
            new RenderJobPlan(
                RenderJobPlan.CurrentSchema,
                RenderJobPlan.CurrentVersion,
                draft.ProjectId,
                draft.Shot.Id,
                draft.Shot.Name,
                deviceId,
                theme.Strategy,
                theme.ThemeId,
                summary.Appearance,
                summary.Output)).ToList();
        foreach (var plan in plans) plan.Validate();
        return new RenderBatchPlan(plans, summaries);
    }

    public async Task<RenderJobSnapshot> PrepareAsync(
        RenderJobPlan plan,
        string temporaryRoot,
        IProgress<RenderSnapshotFreezeProgress> progress,
        CancellationToken cancellationToken)
    {
        var preparation = await ResolveCurrentPreparationAsync(
            plan,
            cancellationToken);
        return (await FreezeAsync(
            preparation,
            temporaryRoot,
            progress,
            cancellationToken)).Single();
    }

    internal async Task<RenderBatchSnapshotPreparation>
        ResolveCurrentPreparationAsync(
            RenderJobPlan plan,
            CancellationToken cancellationToken = default)
    {
        plan.Validate();
        var shot = _database.GetCurrentRenderShot(plan.ShotId);
        var draft = await LoadDraftAsync(
            shot,
            cancellationToken);
        if (!draft.ProjectId.Equals(
                plan.ProjectId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The current Shot no longer belongs to the planned Project.");
        }
        var device = draft.Devices.SingleOrDefault((option) =>
            option.Value.Equals(plan.DeviceId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "The planned Device is no longer available in the Shot Project.");
        var theme = plan.ThemeStrategy == RenderThemeStrategy.Screen
            ? (Strategy: RenderThemeStrategy.Screen, ThemeId: "", Label: RenderThemeSelection.ScreenLabel)
            : RenderThemeSelection.Resolve(
                plan.ThemeId,
                draft.ThemeOptions.Where((option) =>
                    !option.Value.Equals(
                        RenderThemeSelection.ScreenValue,
                        StringComparison.Ordinal)).ToList());
        var metrics = DeviceSettingsFieldContract.PreviewMetrics(
            _database.GetDeviceSettings(plan.DeviceId));
        var summary = new RenderJobSummary(
            new RenderShotContext(
                draft.ProjectId,
                draft.Shot.Id,
                draft.Shot.Name,
                draft.ActorId,
                draft.ActorName),
            device.Label,
            theme.Label,
            plan.RequestedAppearance,
            draft.TotalFrames,
            plan.Output);
        return new RenderBatchSnapshotPreparation(
            draft,
            plan.DeviceId,
            device.Label,
            theme.Strategy,
            plan.ThemeId,
            theme.Label,
            metrics,
            [summary]);
    }

    public async Task<IReadOnlyList<RenderJobSnapshot>> FreezeAsync(
        RenderBatchSnapshotPreparation preparation,
        string batchRoot,
        IProgress<RenderSnapshotFreezeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var draft = preparation.Draft;
        var store = new RenderSnapshotStore(batchRoot, create: true);
        var storedAssets = new HashSet<string>(StringComparer.Ordinal);
        var snapshots = new List<RenderJobSnapshot>();
        foreach (var summary in preparation.Summaries)
        {
            var requestedAppearance = summary.Appearance;
            progress?.Report(new RenderSnapshotFreezeProgress(
                0,
                draft.TotalFrames,
                requestedAppearance,
                ScreenName(draft, 0)));
            using var manifest = store.CreateManifest(
                requestedAppearance);
            for (var frame = 0; frame < draft.TotalFrames; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var activeScreen = draft.Screens.FirstOrDefault(
                    (candidate) =>
                        frame >= candidate.StartFrame
                        && frame < candidate.StartFrame
                            + candidate.DurationFrames);
                var frameMetrics = activeScreen is null
                    ? preparation.Metrics
                    : DeviceSettingsFieldContract.PreviewMetrics(
                        DeviceSettingsFieldContract.ApplyScreenOverrides(
                            _database.GetDeviceSettings(
                                preparation.DeviceId),
                            activeScreen.DeviceOverridesJson,
                            $"Screen '{activeScreen.ScreenId}' Device overrides"));
                var payload = _productionPayloads.PrepareRender(
                    draft.Shot,
                    preparation.ThemeStrategy,
                    preparation.ThemeId,
                    preparation.DeviceId,
                    requestedAppearance,
                    frame);
                var html = payload is null
                    ? DesignWebPreviewPane.BuildTransparentRasterHtml(
                        preparation.Metrics)
                    : await DesignWebPreviewPane.BuildRasterHtmlAsync(
                        frameMetrics,
                        payload);
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var key in PreviewAssetRegistry.Keys(html))
                {
                    if (!PreviewAssetRegistry.TryResolve(key, out var uri))
                    {
                        throw new InvalidOperationException(
                            $"Render snapshot could not freeze Preview asset '{key}'.");
                    }
                    if (storedAssets.Add(key))
                    {
                        store.WriteAsset(key, uri);
                    }
                }
                var documentKey = store.WriteDocument(html);
                manifest.Write(frame, documentKey);
                if ((frame + 1) % 4 == 0
                    || frame + 1 == draft.TotalFrames)
                {
                    progress?.Report(new RenderSnapshotFreezeProgress(
                        frame + 1,
                        draft.TotalFrames,
                        requestedAppearance,
                        ScreenName(draft, frame)));
                    await Task.Yield();
                }
            }
            manifest.Commit();
            var frameStore = new RenderFrameStoreReference(
                Path.GetFullPath(batchRoot),
                $"{requestedAppearance}.frames",
                requestedAppearance,
                draft.TotalFrames);
            var snapshot = new RenderJobSnapshot(
                RenderJobSnapshot.CurrentSchema,
                RenderJobSnapshot.CurrentVersion,
                summary.Context,
                preparation.DeviceId,
                preparation.DeviceName,
                preparation.ThemeStrategy,
                preparation.ThemeId,
                preparation.ThemeName,
                requestedAppearance,
                preparation.Metrics,
                draft.Fps,
                frameStore,
                summary.Output);
            snapshot.Validate();
            snapshots.Add(snapshot);
        }
        return snapshots;
    }

    private static string ScreenName(
        RenderQueueShotDraft draft,
        int frame)
    {
        var screen = draft.Screens.FirstOrDefault((candidate) =>
            frame >= candidate.StartFrame
            && frame < candidate.StartFrame + candidate.DurationFrames);
        return screen?.ScreenName ?? "Transparent gap";
    }
}
