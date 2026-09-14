using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner
{
    internal ProjectTreeNode TransferProductionNode(
        SqliteConnection connection,
        ProjectTreeNode source,
        ProjectTreeNode target,
        ProductionHierarchyTransferMode mode)
    {
        ProductionHierarchyTransferContract.RequireTransfer(
            source,
            target,
            mode);

        lock (WriteGate)
        {
            using var transaction = connection.BeginTransaction();
            var result = source.Kind switch
            {
                ProjectTreeNodeKind.Shot => TransferShot(
                    connection,
                    transaction,
                    source,
                    target,
                    mode),
                ProjectTreeNodeKind.ModuleInstance => TransferScreen(
                    connection,
                    transaction,
                    source,
                    target,
                    mode),
                _ => throw new InvalidOperationException(
                    $"Cannot transfer {source.Kind}."),
            };
            transaction.Commit();
            return result;
        }
    }

    private ProjectTreeNode TransferShot(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProjectTreeNode sourceNode,
        ProjectTreeNode targetEpisodeNode,
        ProductionHierarchyTransferMode mode)
    {
        var source = _shotRepository.Get(
            connection,
            sourceNode.Id);
        var targetEpisode = _projectEpisodeRepository
            .QueryEpisodes(connection)
            .SingleOrDefault((episode) =>
                episode.Id.Equals(
                    targetEpisodeNode.Id,
                    StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Missing target Episode '{targetEpisodeNode.Id}'.");
        if (!source.ProjectId.Equals(
                targetEpisode.ProjectId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A Shot can only be transferred inside its current Project.");
        }

        ShotRecord transferred;
        if (mode == ProductionHierarchyTransferMode.Copy)
        {
            transferred = _shotRepository.DuplicateToEpisode(
                connection,
                source.Id,
                $"shot_{Guid.NewGuid():N}",
                targetEpisode.Id,
                $"{source.Name} copy",
                transaction);
            foreach (var screen in _moduleInstanceRepository
                         .QueryByShot(connection, source.Id))
            {
                _moduleInstanceRepository.Duplicate(
                    connection,
                    screen.Id,
                    $"module_instance_{Guid.NewGuid():N}",
                    transferred.Id,
                    screen.Name,
                    screen.SortOrder,
                    transaction);
            }
        }
        else
        {
            transferred = _shotRepository.MoveToEpisode(
                connection,
                source.Id,
                targetEpisode.Id,
                transaction);
        }

        return new ProjectTreeNode(
            ProjectTreeNodeKind.Shot,
            transferred.Id,
            transferred.Name,
            transferred.Notes,
            sourceNode.RecordClassId,
            targetEpisodeNode);
    }

    private ProjectTreeNode TransferScreen(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProjectTreeNode sourceNode,
        ProjectTreeNode targetShotNode,
        ProductionHierarchyTransferMode mode)
    {
        var source = _moduleInstanceRepository.Get(
            connection,
            sourceNode.Id);
        var sourceShot = _shotRepository.Get(
            connection,
            source.ShotId);
        var targetShot = _shotRepository.Get(
            connection,
            targetShotNode.Id);
        if (!sourceShot.ProjectId.Equals(
                targetShot.ProjectId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A Screen can only be transferred inside its current Project.");
        }
        if (source.ShotId.Equals(
                targetShot.Id,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Screen already belongs to the target Shot.");
        }

        ModuleInstanceRecord transferred;
        if (mode == ProductionHierarchyTransferMode.Copy)
        {
            var copyName = _moduleInstanceRepository.UniqueName(
                connection,
                targetShot.Id,
                $"{source.Name} copy");
            transferred = _moduleInstanceRepository.Duplicate(
                connection,
                source.Id,
                $"module_instance_{Guid.NewGuid():N}",
                targetShot.Id,
                copyName,
                _moduleInstanceRepository.NextSortOrder(
                    connection,
                    targetShot.Id),
                transaction);
        }
        else
        {
            transferred = _moduleInstanceRepository.MoveToShot(
                connection,
                source.Id,
                targetShot.Id,
                _moduleInstanceRepository.NextSortOrder(
                    connection,
                    targetShot.Id),
                transaction);
        }

        SynchronizeTimelineDurations(
            connection,
            transaction: transaction);
        return new ProjectTreeNode(
            ProjectTreeNodeKind.ModuleInstance,
            transferred.Id,
            transferred.Name,
            transferred.Notes,
            sourceNode.RecordClassId,
            targetShotNode);
    }
}
