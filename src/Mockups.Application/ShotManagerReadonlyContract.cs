using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Data;

public sealed record ShotManagerOutputSettings(
    bool Enabled,
    string ProductionId,
    string ProductionSlug,
    string SeasonSlug,
    string WorkstreamName,
    string FolderName,
    string FolderSuffix);

public sealed record ShotManagerEpisodeAssociation(
    bool IsAssociated,
    string ReferenceProductionId,
    string EpisodeId,
    int? EpisodeOrder,
    string EpisodeSlug);

public sealed record ShotManagerShotAssociation(
    bool IsAssociated,
    string ReferenceProductionId,
    string ShotId,
    string CanonicalName);

public sealed record ShotManagerReadonlyEpisode(
    string Id,
    int Order,
    string Slug);

public sealed record ShotManagerReadonlyFolder(
    string Name,
    string Suffix);

public sealed record ShotManagerReadonlyWorkstream(
    string Name,
    IReadOnlyList<ShotManagerReadonlyFolder> Folders);

public sealed record ShotManagerReadonlyShot(
    string Id,
    string EpisodeId,
    string CanonicalName);

public sealed record ShotManagerReadonlyProduction(
    string ProductionId,
    string ProductionSlug,
    string SeasonSlug,
    IReadOnlyList<ShotManagerReadonlyEpisode> Episodes,
    IReadOnlyList<ShotManagerReadonlyWorkstream> Workstreams,
    IReadOnlyList<ShotManagerReadonlyShot> Shots);

public static class ShotManagerReadonlyContract
{
    public static ShotManagerOutputSettings RequireSettings(
        ShotManagerOutputSettings settings,
        string context)
    {
        var values = new[]
        {
            settings.ProductionId,
            settings.ProductionSlug,
            settings.WorkstreamName,
            settings.FolderName,
        };
        if (settings.Enabled && values.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"{context} requires Production, Workstream and folder values while associated.");
        }
        if (!settings.Enabled && values.Any((value) =>
                !string.IsNullOrWhiteSpace(value))
            && values.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"{context} contains a partial inactive Production reference.");
        }
        foreach (var (value, name) in new[]
        {
            (settings.ProductionId, "productionId"),
            (settings.WorkstreamName, "workstreamName"),
            (settings.FolderName, "folderName"),
        }) RequireOptionalExternalId(value, $"{context}.{name}");
        if (!string.IsNullOrEmpty(settings.ProductionId))
            RequireUuid(settings.ProductionId, $"{context}.productionId");
        RequireArtifactFragment(
            settings.FolderSuffix,
            $"{context}.folderSuffix",
            allowEmpty: true);
        return settings;
    }

    public static ShotManagerEpisodeAssociation RequireEpisodeAssociation(
        ShotManagerEpisodeAssociation association,
        string context)
    {
        var hasReference = !string.IsNullOrEmpty(association.EpisodeId);
        if (association.IsAssociated && !hasReference)
            throw new InvalidOperationException($"{context} requires an Episode reference while associated.");
        if (hasReference)
        {
            RequireUuid(association.ReferenceProductionId, $"{context}.referenceProductionId");
            RequireUuid(association.EpisodeId, $"{context}.episodeId");
            if (association.EpisodeOrder is null or <= 0)
                throw new InvalidOperationException($"{context}.episodeOrder must be positive.");
        }
        else if (!string.IsNullOrEmpty(association.ReferenceProductionId)
            || association.EpisodeOrder is not null
            || !string.IsNullOrEmpty(association.EpisodeSlug))
            throw new InvalidOperationException($"{context} contains a partial Episode reference.");
        return association;
    }

    public static ShotManagerShotAssociation RequireShotAssociation(
        ShotManagerShotAssociation association,
        string context)
    {
        var hasReference = !string.IsNullOrEmpty(association.ShotId);
        if (association.IsAssociated && !hasReference)
            throw new InvalidOperationException($"{context} requires a Shot reference while associated.");
        if (hasReference)
        {
            RequireUuid(association.ReferenceProductionId, $"{context}.referenceProductionId");
            RequireUuid(association.ShotId, $"{context}.shotId");
            RequireArtifactFragment(association.CanonicalName, $"{context}.canonicalName", allowEmpty: false);
        }
        else if (!string.IsNullOrEmpty(association.ReferenceProductionId)
            || !string.IsNullOrEmpty(association.CanonicalName))
            throw new InvalidOperationException($"{context} contains a partial Shot reference.");
        return association;
    }

    public static string RequireOptionalExternalId(
        string value,
        string context)
    {
        if (!value.Equals(value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"{context} must be blank or one exact external identity.");
        }
        return value;
    }

    public static ShotManagerReadonlyProduction ParseRequired(
        string json,
        string context)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"{context} is not valid JSON.",
                exception);
        }

        using (document)
        {
            var root = RequireObject(document.RootElement, context);
            var productionId = RequireUuid(
                RequireString(root, "productionId", context, allowEmpty: false),
                $"{context}.productionId");
            var productionSlug = RequireString(
                root,
                "productionSlug",
                context,
                allowEmpty: false);
            var seasonSlug = RequireString(
                root,
                "seasonSlug",
                context,
                allowEmpty: true);
            var episodes = RequireArray(root, "episodes", context)
                .EnumerateArray()
                .Select((item, index) =>
                {
                    var owner = $"{context}.episodes[{index}]";
                    var episode = RequireObject(item, owner);
                    return new ShotManagerReadonlyEpisode(
                        RequireUuid(
                            RequireString(episode, "id", owner, allowEmpty: false),
                            $"{owner}.id"),
                        RequirePositiveInteger(episode, "order", owner),
                        RequireString(episode, "slug", owner, allowEmpty: true));
                })
                .ToList();
            RequireUnique(
                episodes,
                (episode) => episode.Id,
                StringComparer.Ordinal,
                $"{context}.episodes ids");
            RequireUnique(
                episodes,
                (episode) => episode.Order.ToString(CultureInfo.InvariantCulture),
                StringComparer.Ordinal,
                $"{context}.episodes orders");

            var workstreams = RequireArray(root, "workstreams", context)
                .EnumerateArray()
                .Select((item, index) =>
                {
                    var owner = $"{context}.workstreams[{index}]";
                    var workstream = RequireObject(item, owner);
                    var folders = RequireArray(workstream, "folders", owner)
                        .EnumerateArray()
                        .Select((folderItem, folderIndex) =>
                        {
                            var folderOwner = $"{owner}.folders[{folderIndex}]";
                            var folder = RequireObject(folderItem, folderOwner);
                            return new ShotManagerReadonlyFolder(
                                RequireSafeSegment(folder, "name", folderOwner),
                                RequireArtifactFragment(folder, "suffix", folderOwner, allowEmpty: true));
                        })
                        .ToList();
                    RequireUnique(
                        folders,
                        (folder) => folder.Name,
                        StringComparer.OrdinalIgnoreCase,
                        $"{owner}.folders names");
                    return new ShotManagerReadonlyWorkstream(
                        RequireSafeSegment(workstream, "name", owner),
                        folders);
                })
                .ToList();
            RequireUnique(
                workstreams,
                (workstream) => workstream.Name,
                StringComparer.OrdinalIgnoreCase,
                $"{context}.workstreams names");

            var shots = RequireArray(root, "shots", context)
                .EnumerateArray()
                .Select((item, index) =>
                {
                    var owner = $"{context}.shots[{index}]";
                    var shot = RequireObject(item, owner);
                    return new ShotManagerReadonlyShot(
                        RequireUuid(
                            RequireString(shot, "id", owner, allowEmpty: false),
                            $"{owner}.id"),
                        RequireUuid(
                            RequireString(shot, "episodeId", owner, allowEmpty: false),
                            $"{owner}.episodeId"),
                        RequireArtifactFragment(shot, "canonicalName", owner, allowEmpty: false));
                })
                .ToList();
            RequireUnique(
                shots,
                (shot) => shot.Id,
                StringComparer.Ordinal,
                $"{context}.shots ids");
            RequireUnique(
                shots,
                (shot) => shot.CanonicalName,
                StringComparer.Ordinal,
                $"{context}.shots canonical names");
            var episodeIds = episodes
                .Select((episode) => episode.Id)
                .ToHashSet(StringComparer.Ordinal);
            var missingEpisode = shots.FirstOrDefault((shot) =>
                !episodeIds.Contains(shot.EpisodeId));
            if (missingEpisode is not null)
            {
                throw new InvalidOperationException(
                    $"{context}.shots id '{missingEpisode.Id}' references missing Episode '{missingEpisode.EpisodeId}'.");
            }

            return new ShotManagerReadonlyProduction(
                productionId,
                productionSlug,
                seasonSlug,
                episodes,
                workstreams,
                shots);
        }
    }

    public static ProductionOutputShotPlan Resolve(
        ProductionOutputShotContext context)
    {
        if (!context.ShotManagerOutput.Enabled
            || !context.ShotManagerEpisode.IsAssociated
            || !context.ShotManagerShot.IsAssociated)
        {
            throw new InvalidOperationException(
                "Shot Manager resolution requires an enabled Project and one exact associated Shot.");
        }
        var technicalName = $"{context.ShotManagerShot.CanonicalName}{context.ShotManagerOutput.FolderSuffix}";
        RequireArtifactFragment(
            technicalName,
            "Shot Manager artifact base name",
            allowEmpty: false);
        var relativeDirectory = string.Join(
            '/',
            context.ShotManagerEpisode.EpisodeOrder!.Value.ToString("D3", CultureInfo.InvariantCulture),
            context.ShotManagerOutput.WorkstreamName,
            context.ShotManagerOutput.FolderName);
        return new ProductionOutputShotPlan(
            context.ProjectId,
            context.ShotId,
            context.ShotNumber,
            context.ShotManagerShot.CanonicalName,
            technicalName,
            $"shot-manager::{context.ShotManagerOutput.WorkstreamName}::{context.ShotManagerOutput.FolderName}",
            relativeDirectory,
            context.ManualOutput.VersionPadding,
            context.ManualOutput.FramePadding);
    }

    private static JsonElement RequireObject(JsonElement value, string owner)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{owner} must be an object.");
        }
        return value;
    }

    private static JsonElement RequireArray(
        JsonElement owner,
        string property,
        string context)
    {
        if (!owner.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"{context}.{property} must be an array.");
        }
        return value;
    }

    private static string RequireString(
        JsonElement owner,
        string property,
        string context,
        bool allowEmpty)
    {
        if (!owner.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"{context}.{property} must be a string.");
        }
        var result = value.GetString() ?? "";
        if (!allowEmpty && string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException(
                $"{context}.{property} cannot be empty.");
        }
        if (!result.Equals(result.Trim(), StringComparison.Ordinal)
            || result.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"{context}.{property} must preserve one exact trimmed string.");
        }
        return result;
    }

    private static int RequirePositiveInteger(
        JsonElement owner,
        string property,
        string context)
    {
        if (!owner.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var result)
            || result <= 0)
        {
            throw new InvalidOperationException(
                $"{context}.{property} must be a positive integer.");
        }
        return result;
    }

    private static string RequireUuid(string value, string context)
    {
        if (!Guid.TryParseExact(value, "D", out _))
            throw new InvalidOperationException($"{context} must be one canonical UUID.");
        return value;
    }

    private static string RequireSafeSegment(
        JsonElement owner,
        string property,
        string context) =>
        RequireSafeSegment(
            RequireString(owner, property, context, allowEmpty: false),
            $"{context}.{property}");

    private static string RequireSafeSegment(string value, string context)
    {
        if (value is "." or ".."
            || value.IndexOfAny(['/', '\\', ':']) >= 0)
        {
            throw new InvalidOperationException(
                $"{context} must be one safe path segment.");
        }
        return value;
    }

    private static string RequireArtifactFragment(
        JsonElement owner,
        string property,
        string context,
        bool allowEmpty) =>
        RequireArtifactFragment(
            RequireString(owner, property, context, allowEmpty),
            $"{context}.{property}",
            allowEmpty);

    private static string RequireArtifactFragment(
        string value,
        string context,
        bool allowEmpty)
    {
        if ((!allowEmpty && string.IsNullOrWhiteSpace(value))
            || value.IndexOfAny(['/', '\\', ':']) >= 0
            || value.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{context} must be one safe artifact-name fragment.");
        }
        return value;
    }

    private static void RequireUnique<T>(
        IReadOnlyList<T> values,
        Func<T, string> selector,
        StringComparer comparer,
        string context)
    {
        var duplicate = values
            .GroupBy(selector, comparer)
            .FirstOrDefault((group) => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{context} contain duplicate value '{duplicate.Key}'.");
        }
    }
}
