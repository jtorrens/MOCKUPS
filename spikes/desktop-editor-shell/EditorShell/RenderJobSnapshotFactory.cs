using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record RenderQueueRouteOption(
    string EntryId,
    string RelativeDirectory,
    int VersionPadding);

internal sealed record RenderQueueShotDraft(
    ProjectTreeNode Shot,
    string ProjectId,
    string ProductionId,
    string ActorId,
    string ActorName,
    string DefaultDeviceId,
    string DefaultThemeId,
    int ShotNumber,
    int Fps,
    int TotalFrames,
    string SuggestedBaseName,
    string RootPath,
    bool UsesCachedRoot,
    string RouteStatusMessage,
    IReadOnlyList<FieldOption> Devices,
    IReadOnlyList<FieldOption> Themes,
    IReadOnlyList<RenderQueueRouteOption> Routes);

internal sealed class RenderJobSnapshotFactory
{
    private sealed record RouteResolution(
        string ProductionId,
        string SuggestedBaseName,
        string RootPath,
        bool UsesCachedRoot,
        string StatusMessage,
        IReadOnlyList<RenderQueueRouteOption> Routes);

    private readonly SpikeDatabase _database;
    private readonly IShotManagerIntegrationClient _shotManager;
    private readonly ShotManagerWorkstationRootStore _roots;
    private readonly DesignPreviewPayloadDataSource _payloadData;

    public RenderJobSnapshotFactory(
        SpikeDatabase database,
        IShotManagerIntegrationClient? shotManager = null,
        ShotManagerWorkstationRootStore? roots = null)
    {
        _database = database;
        _shotManager = shotManager ?? new ShotManagerIntegrationClient();
        _roots = roots ?? new ShotManagerWorkstationRootStore();
        _payloadData = new DesignPreviewPayloadDataSource(database);
    }

    public async Task<RenderQueueShotDraft> LoadDraftAsync(
        ProjectTreeNode shot,
        CancellationToken cancellationToken = default)
    {
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
        var routeResolution = await ResolveRoutesAsync(
            shot,
            shotSettings,
            cancellationToken);

        return new RenderQueueShotDraft(
            shot,
            shotSettings.ProjectId,
            routeResolution.ProductionId,
            shotSettings.OwnerActorId,
            actor.DisplayName,
            actor.DefaultDeviceId,
            actor.DefaultThemeId,
            shotSettings.ShotNumber,
            shotSettings.Fps,
            shotSettings.DurationFrames,
            routeResolution.SuggestedBaseName,
            routeResolution.RootPath,
            routeResolution.UsesCachedRoot,
            routeResolution.StatusMessage,
            _database.GetDeviceOptions(shotSettings.ProjectId),
            _database.GetThemeOptions(shotSettings.ProjectId),
            routeResolution.Routes);
    }

    private async Task<RouteResolution> ResolveRoutesAsync(
        ProjectTreeNode shot,
        SpikeDatabase.ShotSettings shotSettings,
        CancellationToken cancellationToken)
    {
        var association = _database.GetShotManagerAssociation(
            shotSettings.ProjectId);
        var binding = _database.GetShotManagerEpisodeBinding(
            shotSettings.EpisodeId);
        var cachedRecord = _database.GetShotManagerShotStructure(shot.Id);
        Exception? planError = null;

        if (association is not null && binding is not null)
        {
            try
            {
                var plan = await _shotManager.PlanShotAsync(
                    association.ProductionId,
                    binding.ExternalEpisodeId,
                    shotSettings.ShotNumber,
                    cancellationToken);
                if (plan.OutputContracts.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Shot Manager did not define an output route for this Shot.");
                }
                var stored = _database.StoreShotManagerPlan(
                    shot.Id,
                    plan);
                _roots.Remember(plan.Production.Id, plan.RootPath);
                return Resolution(
                    stored,
                    plan.RootPath,
                    usesCachedRoot: false,
                    statusMessage: "");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                planError = exception;
            }
        }

        var usableCache = CachedRecordMatchesCurrentContext(
            cachedRecord,
            shotSettings,
            association,
            binding)
            ? cachedRecord
            : null;
        if (usableCache is not null)
        {
            var rootPath = _roots.Get(usableCache.ProductionId);
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                try
                {
                    var snapshot = await _shotManager.GetSnapshotAsync(
                        usableCache.ProductionId,
                        cancellationToken);
                    rootPath = snapshot.RootPath;
                    _roots.Remember(
                        usableCache.ProductionId,
                        rootPath);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    planError ??= exception;
                }
            }
            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                try
                {
                    return Resolution(
                        usableCache,
                        rootPath,
                        usesCachedRoot: true,
                        statusMessage:
                            "Using the last Shot Manager route and workstation root.");
                }
                catch (Exception exception)
                {
                    planError ??= exception;
                }
            }
        }

        var status = association is null
            ? "Associate this Project with Shot Manager to resolve its output route."
            : binding is null
                ? "Synchronize this Episode with Shot Manager to resolve its output route."
                : planError?.Message
                    ?? "Shot Manager could not resolve an output route for this Shot.";
        return new RouteResolution(
            association?.ProductionId
                ?? usableCache?.ProductionId
                ?? "",
            RenderOutputPlanner.SuggestedBaseName(shot.Name),
            "",
            false,
            status,
            []);
    }

    private static bool CachedRecordMatchesCurrentContext(
        ShotManagerShotStructureRecord? record,
        SpikeDatabase.ShotSettings shotSettings,
        ShotManagerProjectAssociationRecord? association,
        ShotManagerEpisodeBindingRecord? binding)
    {
        if (record is null
            || record.ShotNumber != shotSettings.ShotNumber)
        {
            return false;
        }
        if (association is null)
        {
            return true;
        }
        return binding is not null
            && record.ProductionId.Equals(
                association.ProductionId,
                StringComparison.Ordinal)
            && record.SeasonId.Equals(
                association.SeasonId,
                StringComparison.Ordinal)
            && record.EpisodeId.Equals(
                binding.ExternalEpisodeId,
                StringComparison.Ordinal);
    }

    private static RouteResolution Resolution(
        ShotManagerShotStructureRecord record,
        string rootPath,
        bool usesCachedRoot,
        string statusMessage)
    {
        var structure = ShotManagerPortableStructure.Parse(
            record.StructureJson,
            $"Shot Manager Shot '{record.ShotId}' structure");
        if (structure.OutputContracts.Count == 0)
        {
            throw new InvalidOperationException(
                "Shot Manager did not define an output route for this Shot.");
        }
        return new RouteResolution(
            record.ProductionId,
            RenderOutputPlanner.SuggestedBaseName(record.FullName),
            rootPath,
            usesCachedRoot,
            statusMessage,
            structure.OutputContracts
                .Select((output) => new RenderQueueRouteOption(
                    output.EntryId,
                    output.RelativeDirectory,
                    output.VersionPadding))
                .OrderBy(
                    (output) => output.RelativeDirectory,
                    StringComparer.Ordinal)
                .ToList());
    }

    public async Task<IReadOnlyList<RenderJobSnapshot>> BuildAsync(
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
                "Select a stored Shot Manager output route.");
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
        var snapshots = new List<RenderJobSnapshot>();
        foreach (var requestedAppearance in appearances)
        {
            var frames = new List<RenderFrozenFrame>(draft.TotalFrames);
            var assets = new Dictionary<string, string>(
                StringComparer.Ordinal);
            for (var frame = 0; frame < draft.TotalFrames; frame++)
            {
                var payload = DesignPreviewPayloadFactory.CreateProductionRender(
                    _payloadData,
                    draft.Shot,
                    themeId,
                    deviceId,
                    requestedAppearance,
                    frame);
                var html = await DesignWebPreviewPane.BuildRasterHtmlAsync(
                    metrics,
                    payload);
                foreach (var key in PreviewAssetRegistry.Keys(html))
                {
                    if (!PreviewAssetRegistry.TryResolve(key, out var uri))
                    {
                        throw new InvalidOperationException(
                            $"Render snapshot could not freeze Preview asset '{key}'.");
                    }
                    assets[key] = uri;
                }
                frames.Add(new RenderFrozenFrame(frame, html));
            }
            var output = new RenderOutputTarget(
                draft.ProductionId,
                route.EntryId,
                draft.RootPath,
                route.RelativeDirectory,
                safeBaseName,
                requestedAppearance,
                outputPlan.Version,
                route.VersionPadding,
                outputModeId,
                outputPlan.OutputPaths[requestedAppearance]);
            var snapshot = new RenderJobSnapshot(
                RenderJobSnapshot.CurrentSchema,
                RenderJobSnapshot.CurrentVersion,
                context,
                deviceId,
                device.Label,
                themeId,
                theme.Label,
                requestedAppearance,
                metrics,
                draft.Fps,
                frames,
                assets,
                output);
            snapshot.Validate();
            snapshots.Add(snapshot);
        }
        return snapshots;
    }
}
