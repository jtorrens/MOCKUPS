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
    string Appearance);

internal sealed record RenderBatchSnapshotPreparation(
    RenderQueueShotDraft Draft,
    string DeviceId,
    string DeviceName,
    string ThemeId,
    string ThemeName,
    DevicePreviewMetrics Metrics,
    IReadOnlyList<RenderJobSummary> Summaries);

internal sealed record RenderQueueShotDraft(
    ProjectTreeNode Shot,
    string ProjectId,
    string ActorId,
    string ActorName,
    string DefaultDeviceId,
    string DefaultThemeId,
    int ShotNumber,
    int Fps,
    int TotalFrames,
    string SuggestedBaseName,
    string RootPath,
    string RouteStatusMessage,
    IReadOnlyList<FieldOption> Devices,
    IReadOnlyList<FieldOption> Themes,
    IReadOnlyList<RenderQueueRouteOption> Routes);

internal sealed class RenderJobSnapshotFactory
{
    private readonly IRenderSnapshotDataSource _database;
    private readonly ProductionOutputRootStore _roots;
    private readonly DesignPreviewPayloadDataSource _payloadData;

    public RenderJobSnapshotFactory(
        IRenderSnapshotDataSource database,
        IProjectPathResolver projectPaths,
        ProductionOutputRootStore? roots = null)
    {
        _database = database;
        _roots = roots ?? new ProductionOutputRootStore();
        _payloadData = new DesignPreviewPayloadDataSource(
            database,
            projectPaths);
    }

    public Task<RenderQueueShotDraft> LoadDraftAsync(
        ProjectTreeNode shot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (shot.Kind != ProjectTreeNodeKind.Shot)
        {
            throw new InvalidOperationException(
                "Render Queue can only add a concrete Shot.");
        }
        var shotSettings = _database.GetShotSettings(shot.Id);
        var actor = _database.GetActorSettings(shotSettings.OwnerActorId);
        if (string.IsNullOrWhiteSpace(actor.DefaultDeviceId)
            || string.IsNullOrWhiteSpace(actor.DefaultThemeId))
        {
            throw new InvalidOperationException(
                $"Actor '{actor.DisplayName}' must define a default Device and Theme before rendering.");
        }
        var plan = _database.GetProductionOutputShotPlan(shot.Id);
        var rootPath = _roots.Get(shotSettings.ProjectId) ?? "";
        var status = string.IsNullOrWhiteSpace(rootPath)
            ? "Configure this Project's local Production Output root before rendering."
            : "";
        return Task.FromResult(new RenderQueueShotDraft(
            shot,
            shotSettings.ProjectId,
            shotSettings.OwnerActorId,
            actor.DisplayName,
            actor.DefaultDeviceId,
            actor.DefaultThemeId,
            shotSettings.ShotNumber,
            shotSettings.Fps,
            shotSettings.DurationFrames,
            plan.TechnicalName,
            rootPath,
            status,
            _database.GetDeviceOptions(shotSettings.ProjectId),
            _database.GetThemeOptions(shotSettings.ProjectId),
            [
                new RenderQueueRouteOption(
                    plan.RouteId,
                    plan.RelativeDirectory,
                    plan.VersionPadding,
                    plan.FramePadding),
            ]));
    }

    public RenderBatchSnapshotPreparation PlanBatch(
        RenderQueueShotDraft draft,
        string deviceId,
        string themeId,
        string appearance,
        string outputModeId,
        string structureEntryId,
        string baseName,
        RenderOutputPlan outputPlan)
    {
        var device = draft.Devices.SingleOrDefault((option) =>
            option.Value.Equals(deviceId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "Select a Device from the Shot Project.");
        var theme = draft.Themes.SingleOrDefault((option) =>
            option.Value.Equals(themeId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "Select a Theme from the Shot Project.");
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
        var metrics = _database.GetDevicePreviewMetrics(deviceId);
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
                route.FramePadding);
            return new RenderJobSummary(
                context,
                device.Label,
                theme.Label,
                requestedAppearance,
                draft.TotalFrames,
                output);
        }).ToList();
        return new RenderBatchSnapshotPreparation(
            draft,
            deviceId,
            device.Label,
            themeId,
            theme.Label,
            metrics,
            summaries);
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
                requestedAppearance));
            using var manifest = store.CreateManifest(
                requestedAppearance);
            for (var frame = 0; frame < draft.TotalFrames; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var payload = DesignPreviewPayloadFactory.CreateProductionRender(
                    _payloadData,
                    draft.Shot,
                    preparation.ThemeId,
                    preparation.DeviceId,
                    requestedAppearance,
                    frame);
                var html = await DesignWebPreviewPane.BuildRasterHtmlAsync(
                    preparation.Metrics,
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
                        requestedAppearance));
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
}
