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
    int Fps,
    int TotalFrames,
    string SuggestedBaseName,
    string RootPath,
    bool UsesCachedRoot,
    IReadOnlyList<FieldOption> Devices,
    IReadOnlyList<FieldOption> Themes,
    IReadOnlyList<RenderQueueRouteOption> Routes);

internal sealed class RenderJobSnapshotFactory
{
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
        var structureRecord = _database.GetShotManagerShotStructure(shot.Id)
            ?? throw new InvalidOperationException(
                "This Shot has no stored Shot Manager output routes.");
        var structure = ShotManagerPortableStructure.Parse(
            structureRecord.StructureJson,
            $"Shot Manager Shot '{shot.Id}' structure");
        if (structure.OutputContracts.Count == 0)
        {
            throw new InvalidOperationException(
                "Shot Manager did not define an output route for this Shot.");
        }
        var shotSettings = _database.GetShotSettings(shot.Id);
        var actor = _database.GetActorSettings(shotSettings.OwnerActorId);
        if (string.IsNullOrWhiteSpace(actor.DefaultDeviceId)
            || string.IsNullOrWhiteSpace(actor.DefaultThemeId))
        {
            throw new InvalidOperationException(
                $"Actor '{actor.DisplayName}' must define a default Device and Theme before rendering.");
        }

        var cachedRoot = _roots.Get(structureRecord.ProductionId);
        var rootPath = "";
        var usesCachedRoot = false;
        try
        {
            var snapshot = await _shotManager.GetSnapshotAsync(
                structureRecord.ProductionId,
                cancellationToken);
            if (!snapshot.Production.Id.Equals(
                    structureRecord.ProductionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Shot Manager returned another Production while resolving the render root.");
            }
            rootPath = snapshot.RootPath;
            _roots.Remember(structureRecord.ProductionId, rootPath);
        }
        catch (Exception) when (
            !cancellationToken.IsCancellationRequested
            && !string.IsNullOrWhiteSpace(cachedRoot))
        {
            rootPath = cachedRoot;
            usesCachedRoot = true;
        }
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException(
                "Shot Manager is unavailable and this workstation has no last known Production root.");
        }

        return new RenderQueueShotDraft(
            shot,
            shotSettings.ProjectId,
            structureRecord.ProductionId,
            shotSettings.OwnerActorId,
            actor.DisplayName,
            actor.DefaultDeviceId,
            actor.DefaultThemeId,
            shotSettings.Fps,
            shotSettings.DurationFrames,
            RenderOutputPlanner.SuggestedBaseName(structureRecord.FullName),
            rootPath,
            usesCachedRoot,
            _database.GetDeviceOptions(shotSettings.ProjectId),
            _database.GetThemeOptions(shotSettings.ProjectId),
            structure.OutputContracts
                .Select((output) => new RenderQueueRouteOption(
                    output.EntryId,
                    output.RelativeDirectory,
                    output.VersionPadding))
                .OrderBy((output) => output.RelativeDirectory, StringComparer.Ordinal)
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
