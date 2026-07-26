using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Integrations.ShotManager;

internal sealed class ShotManagerAssociationService
{
    private readonly SpikeDatabase _database;
    private readonly ShotManagerWorkstationRootStore _roots;

    public ShotManagerAssociationService(
        SpikeDatabase database,
        ShotManagerWorkstationRootStore? roots = null)
    {
        _database = database;
        _roots = roots ?? new ShotManagerWorkstationRootStore();
    }

    public ShotManagerProjectAssociationRecord Synchronize(
        string projectId,
        ShotManagerProductionSnapshot snapshot,
        string seasonId,
        IReadOnlyList<ShotManagerEpisodeAssociationChoice> episodeChoices)
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
        var byExternalId = locals
            .Where((local) => local.Binding is not null)
            .ToDictionary(
                (local) => local.Binding!.ExternalEpisodeId,
                StringComparer.Ordinal);
        var unboundById = locals
            .Where((local) => local.Binding is null)
            .ToDictionary(
                (local) => local.Episode.Id,
                StringComparer.Ordinal);
        var unresolvedRemoteIds = remoteEpisodes
            .Where((remote) => !byExternalId.ContainsKey(remote.Id))
            .Select((remote) => remote.Id)
            .ToHashSet(StringComparer.Ordinal);
        var choicesByExternalId = ValidateChoices(
            episodeChoices,
            unresolvedRemoteIds,
            unboundById);
        var writes = new List<ShotManagerEpisodeWrite>();
        var matchedLocalIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < remoteEpisodes.Count; index++)
        {
            var remote = remoteEpisodes[index];
            byExternalId.TryGetValue(remote.Id, out var local);
            if (local is null)
            {
                var choice = choicesByExternalId[remote.Id];
                if (choice.LocalEpisodeId is not null)
                {
                    local = unboundById[choice.LocalEpisodeId];
                }
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
        _roots.Remember(snapshot.Production.Id, snapshot.RootPath);
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

    private static IReadOnlyDictionary<string, ShotManagerEpisodeAssociationChoice>
        ValidateChoices(
            IReadOnlyList<ShotManagerEpisodeAssociationChoice> choices,
            IReadOnlySet<string> unresolvedRemoteIds,
            IReadOnlyDictionary<string, ShotManagerLocalEpisodeRecord> unboundById)
    {
        if (choices.Any((choice) =>
                string.IsNullOrWhiteSpace(choice.ExternalEpisodeId)
                || (choice.LocalEpisodeId is not null
                    && string.IsNullOrWhiteSpace(choice.LocalEpisodeId)))
            || choices.Select((choice) => choice.ExternalEpisodeId)
                .Distinct(StringComparer.Ordinal).Count() != choices.Count)
        {
            throw new InvalidOperationException(
                "Shot Manager Episode associations must identify each remote Episode exactly once.");
        }

        var choicesByExternalId = choices.ToDictionary(
            (choice) => choice.ExternalEpisodeId,
            StringComparer.Ordinal);
        if (!choicesByExternalId.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(unresolvedRemoteIds))
        {
            throw new InvalidOperationException(
                "Every unassociated Shot Manager Episode requires an explicit local Episode or Create new choice.");
        }

        var localIds = choices
            .Where((choice) => choice.LocalEpisodeId is not null)
            .Select((choice) => choice.LocalEpisodeId!)
            .ToList();
        if (localIds.Distinct(StringComparer.Ordinal).Count() != localIds.Count)
        {
            throw new InvalidOperationException(
                "A local Episode cannot be associated with more than one Shot Manager Episode.");
        }
        var missingLocalIds = localIds
            .Where((localId) => !unboundById.ContainsKey(localId))
            .ToList();
        if (missingLocalIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Local Episodes are unavailable for Shot Manager association: {string.Join(", ", missingLocalIds)}.");
        }

        return choicesByExternalId;
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
