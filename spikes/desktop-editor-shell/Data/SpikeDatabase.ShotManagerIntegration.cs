using Mockups.DesktopEditorShell.Integrations.ShotManager;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public ShotManagerProjectAssociationRecord? GetShotManagerAssociation(
        string projectId)
    {
        return _shotManagerIntegrationRepository.GetAssociation(projectId);
    }

    public ShotManagerEpisodeBindingRecord? GetShotManagerEpisodeBinding(
        string episodeId)
    {
        return _shotManagerIntegrationRepository.GetEpisodeBinding(episodeId);
    }

    public ShotManagerShotStructureRecord? GetShotManagerShotStructure(
        string shotId)
    {
        return _shotManagerIntegrationRepository.GetShotStructure(shotId);
    }

    public int SuggestShotManagerShotNumber(string episodeId)
    {
        return _shotManagerIntegrationRepository.SuggestShotNumber(episodeId);
    }

    public IReadOnlyList<ShotManagerLocalEpisodeRecord>
        LoadShotManagerLocalEpisodes(string projectId)
    {
        return _shotManagerIntegrationRepository.LoadLocalEpisodes(projectId);
    }

    public void ApplyShotManagerAssociation(
        ShotManagerAssociationWritePlan plan)
    {
        _shotManagerIntegrationRepository.ApplyAssociation(plan);
    }

    public void DisconnectShotManager(string projectId)
    {
        _shotManagerIntegrationRepository.Disconnect(projectId);
    }

    public ProjectTreeNode AddShotFromShotManager(
        ProjectTreeNode episode,
        string actorId,
        ShotManagerExternalShotPlan plan,
        string? duplicateSourceShotId = null)
    {
        if (episode.Kind != ProjectTreeNodeKind.Episode)
        {
            throw new InvalidOperationException(
                "Shot Manager Shots can only be added to an Episode.");
        }
        var portableStructure = plan.ToPortableStructure();
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            _moduleInstanceThemeContextService.RequireEpisodeActor(
                connection,
                episode.Id,
                actorId);
            _shotManagerIntegrationRepository.ValidateGovernedShotContext(
                connection,
                episode.Id,
                plan.Production.Id,
                plan.Season.Id,
                plan.Episode.Id);
            var shot = duplicateSourceShotId is null
                ? _shotRepository.PrepareGoverned(
                    connection,
                    episode.Id,
                    actorId,
                    plan.FullName,
                    plan.ShotCode)
                : _shotRepository.PrepareGovernedDuplicate(
                    connection,
                    duplicateSourceShotId,
                    actorId,
                    plan.FullName,
                    plan.ShotCode);
            if (!shot.EpisodeId.Equals(
                episode.Id,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A governed Shot can only be duplicated inside its original Episode.");
            }
            var timestamp = DateTimeOffset.UtcNow.ToString("O");
            using var transaction = connection.BeginTransaction();
            _shotRepository.InsertPrepared(connection, transaction, shot);
            _shotManagerIntegrationRepository.InsertShotStructure(
                connection,
                transaction,
                new ShotManagerShotStructureRecord(
                    shot.Id,
                    plan.PlanVersion,
                    plan.Production.Id,
                    plan.Season.Id,
                    plan.Episode.Id,
                    plan.ShotNumber,
                    plan.ShotCode,
                    plan.FullName,
                    portableStructure.ToJson(),
                    timestamp));
            transaction.Commit();
            return new ProjectTreeNode(
                ProjectTreeNodeKind.Shot,
                shot.Id,
                shot.Name,
                shot.Notes,
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Shot),
                episode);
        }
    }
}
