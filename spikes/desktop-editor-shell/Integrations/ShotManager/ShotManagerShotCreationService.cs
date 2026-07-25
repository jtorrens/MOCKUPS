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
    private readonly ShotManagerFolderMaterializer _folders;

    public ShotManagerShotCreationService(
        SpikeDatabase database,
        IShotManagerIntegrationClient? client = null,
        ShotManagerFolderMaterializer? folders = null)
    {
        _database = database;
        _client = client ?? new ShotManagerIntegrationClient();
        _folders = folders ?? new ShotManagerFolderMaterializer();
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
                "Synchronize this Episode with Shot Manager before creating an official Shot.");
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
        var creation = await _folders.CreateAsync(plan, cancellationToken);
        try
        {
            return _database.AddShotFromShotManager(
                episode,
                actorId,
                plan,
                duplicateSourceShotId);
        }
        catch (Exception databaseError)
        {
            try
            {
                await _folders.RollbackAsync(
                    creation,
                    CancellationToken.None);
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException(
                    "The Shot could not be saved and its newly created folders could not be completely rolled back.",
                    databaseError,
                    rollbackError);
            }
            throw;
        }
    }

    public async Task<int> RepairAsync(
        string shotId,
        CancellationToken cancellationToken = default)
    {
        var record = _database.GetShotManagerShotStructure(shotId)
            ?? throw new InvalidOperationException(
                "This Shot has no stored Shot Manager folder snapshot.");
        var structure = ShotManagerPortableStructure.Parse(
            record.StructureJson,
            $"Shot Manager Shot '{shotId}' structure");
        var snapshot = await _client.GetSnapshotAsync(
            record.ProductionId,
            cancellationToken);
        var creation = await _folders.RepairAsync(
            snapshot.RootPath,
            structure,
            cancellationToken);
        return creation.CreatedDirectories.Count;
    }
}
