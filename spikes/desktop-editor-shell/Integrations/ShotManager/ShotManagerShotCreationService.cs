using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.Integrations.ShotManager;

internal sealed class ShotManagerShotCreationService
{
    private readonly SpikeDatabase _database;
    private readonly IShotManagerIntegrationClient _client;
    private readonly ShotManagerWorkstationRootStore _roots;

    public ShotManagerShotCreationService(
        SpikeDatabase database,
        IShotManagerIntegrationClient? client = null,
        ShotManagerWorkstationRootStore? roots = null)
    {
        _database = database;
        _client = client ?? new ShotManagerIntegrationClient();
        _roots = roots ?? new ShotManagerWorkstationRootStore();
    }

    public async Task<ProjectTreeNode> CreateAsync(
        ProjectTreeNode episode,
        string actorId,
        int shotNumber,
        string? duplicateSourceShotId = null,
        CancellationToken cancellationToken = default)
    {
        var binding = _database.GetShotManagerEpisodeBinding(episode.Id)
            ?? throw new InvalidOperationException(
                "Synchronize this Episode with Shot Manager before resolving an official Shot route.");
        var association = _database.GetShotManagerAssociation(binding.ProjectId)
            ?? throw new InvalidOperationException(
                "The Project is no longer associated with Shot Manager.");
        var plan = await _client.PlanShotAsync(
            association.ProductionId,
            binding.ExternalEpisodeId,
            shotNumber,
            cancellationToken);
        if (!plan.Production.Id.Equals(
                association.ProductionId,
                StringComparison.Ordinal)
            || !plan.Season.Id.Equals(
                association.SeasonId,
                StringComparison.Ordinal)
            || !plan.Episode.Id.Equals(
                binding.ExternalEpisodeId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Shot Manager returned a plan outside the associated Production, Season or Episode.");
        }
        _roots.Remember(plan.Production.Id, plan.RootPath);
        return _database.AddShotFromShotManager(
            episode,
            actorId,
            plan,
            duplicateSourceShotId);
    }

}
