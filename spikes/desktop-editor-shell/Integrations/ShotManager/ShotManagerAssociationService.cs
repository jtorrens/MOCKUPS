using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Integrations.ShotManager;

internal sealed class ShotManagerAssociationService
{
    private readonly SpikeDatabase _database;

    public ShotManagerAssociationService(SpikeDatabase database)
    {
        _database = database;
    }

    public ShotManagerProjectAssociationRecord Synchronize(
        string projectId,
        ShotManagerProductionSnapshot snapshot,
        string seasonId)
    {
        if (snapshot.Production.ProductionType != "SERIES"
            || snapshot.Production.SeriesShotStructure != "EPISODE_SHOT")
        {
            throw new InvalidOperationException(
                "MOCKUPS only supports Shot Manager series whose Shots belong directly to an Episode.");
        }
        var season = snapshot.Seasons.SingleOrDefault((candidate) =>
            candidate.Id.Equals(seasonId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "The selected Shot Manager Season is not active.");
        if (!season.ProductionId.Equals(
            snapshot.Production.Id,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The selected Season does not belong to the selected Production.");
        }
        var remoteEpisodes = snapshot.Episodes
            .Where((episode) =>
                episode.SeasonId.Equals(season.Id, StringComparison.Ordinal))
            .OrderBy((episode) => episode.Number)
            .ThenBy((episode) => episode.Id, StringComparer.Ordinal)
            .ToList();
        RequireUnique(
            remoteEpisodes.Select((episode) => episode.Id),
            "Episode identities");
        RequireUnique(
            remoteEpisodes.Select((episode) => episode.Number.ToString()),
            "Episode numbers");
        RequireUnique(
            remoteEpisodes.Select((episode) => episode.Code),
            "Episode codes",
            StringComparer.OrdinalIgnoreCase);

        var existingAssociation = _database.GetShotManagerAssociation(projectId);
        if (existingAssociation is not null
            && (!existingAssociation.ProductionId.Equals(
                    snapshot.Production.Id,
                    StringComparison.Ordinal)
                || !existingAssociation.SeasonId.Equals(
                    season.Id,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Disconnect the current Shot Manager Season before selecting another one.");
        }
        var locals = _database.LoadShotManagerLocalEpisodes(projectId);
        var governed = existingAssociation is not null;
        var byExternalId = locals
            .Where((local) => local.Binding is not null)
            .ToDictionary(
                (local) => local.Binding!.ExternalEpisodeId,
                StringComparer.Ordinal);
        var unboundByExactCode = locals
            .Where((local) => local.Binding is null)
            .GroupBy(
                (local) => local.Episode.Slug,
                StringComparer.OrdinalIgnoreCase)
            .Where((group) => group.Count() == 1)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.Single(),
                StringComparer.OrdinalIgnoreCase);
        var writes = new List<ShotManagerEpisodeWrite>();
        var matchedLocalIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < remoteEpisodes.Count; index++)
        {
            var remote = remoteEpisodes[index];
            ShotManagerLocalEpisodeRecord? local = null;
            if (governed)
            {
                byExternalId.TryGetValue(remote.Id, out local);
            }
            else
            {
                unboundByExactCode.TryGetValue(remote.Code, out local);
            }
            var episode = local?.Episode ?? new EpisodeRecord(
                $"episode_{Guid.NewGuid():N}",
                projectId,
                EpisodeName(remote),
                remote.Code,
                "Episode synchronized from Shot Manager.",
                index);
            episode = episode with
            {
                Name = EpisodeName(remote),
                Slug = remote.Code,
                SortOrder = index,
            };
            writes.Add(new ShotManagerEpisodeWrite(
                episode,
                remote.Id,
                remote.Number,
                remote.Code));
            matchedLocalIds.Add(episode.Id);
        }

        var removedGoverned = locals.Where((local) =>
            local.Binding is not null
            && !matchedLocalIds.Contains(local.Episode.Id)).ToList();
        var blockedRemoval = removedGoverned
            .Where((local) => local.HasShots)
            .Select((local) => local.Episode.Name)
            .ToList();
        if (blockedRemoval.Count > 0)
        {
            throw new InvalidOperationException(
                "Shot Manager removed Episodes that still contain local Shots: "
                + string.Join(", ", blockedRemoval)
                + ". Disconnect the Project to preserve them as independent Episodes.");
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var association = new ShotManagerProjectAssociationRecord(
            projectId,
            snapshot.Production.Id,
            snapshot.Production.Name,
            season.Id,
            season.Code,
            season.Name,
            timestamp);
        _database.ApplyShotManagerAssociation(new ShotManagerAssociationWritePlan(
            association,
            writes,
            removedGoverned.Select((local) => local.Episode.Id).ToList()));
        return association;
    }

    public void Disconnect(string projectId)
    {
        _database.DisconnectShotManager(projectId);
    }

    private static string EpisodeName(ShotManagerEpisode episode)
    {
        return string.IsNullOrWhiteSpace(episode.Title)
            ? $"Episode {episode.Code}"
            : episode.Title.Trim();
    }

    private static void RequireUnique(
        IEnumerable<string> values,
        string label,
        IEqualityComparer<string>? comparer = null)
    {
        comparer ??= StringComparer.Ordinal;
        var list = values.ToList();
        if (list.Distinct(comparer).Count() != list.Count)
        {
            throw new InvalidOperationException(
                $"Shot Manager returned duplicate {label}.");
        }
    }
}
