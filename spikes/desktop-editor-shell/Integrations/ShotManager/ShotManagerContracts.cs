using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.Integrations.ShotManager;

internal sealed record ShotManagerStatus(
    bool Connected,
    string Message);

internal sealed record ShotManagerCatalogProduction(
    string Id,
    string Name,
    string Code,
    string ProductionType,
    string? SeriesShotStructure);

internal sealed record ShotManagerSeason(
    string Id,
    string ProductionId,
    int Number,
    string Code,
    string? Name);

internal sealed record ShotManagerEpisode(
    string Id,
    string ProductionId,
    string SeasonId,
    int Number,
    string Code,
    string? Title);

internal sealed record ShotManagerProductionSnapshot(
    string RootPath,
    ShotManagerCatalogProduction Production,
    IReadOnlyList<ShotManagerSeason> Seasons,
    IReadOnlyList<ShotManagerEpisode> Episodes);

internal sealed record ShotManagerEpisodeAssociationChoice(
    string ExternalEpisodeId,
    string? LocalEpisodeId);

internal sealed record ShotManagerPlanDirectory(
    string RelativePath,
    string ResolvedPath);

internal sealed record ShotManagerPlanEntry(
    string EntryId,
    string RelativePath,
    string ResolvedPath);

internal sealed record ShotManagerExternalShotPlan(
    int PlanVersion,
    ShotManagerCatalogProduction Production,
    ShotManagerSeason Season,
    ShotManagerEpisode Episode,
    int ShotNumber,
    string ShotCode,
    string FullName,
    string RootPath,
    IReadOnlyList<ShotManagerPlanDirectory> Directories,
    IReadOnlyList<ShotManagerPlanDirectory> ShotOwnedDirectories,
    IReadOnlyList<ShotManagerPlanEntry> StructureEntries);

internal static class ShotManagerExternalShotPlanExtensions
{
    public static ShotManagerPortableStructure ToPortableStructure(
        this ShotManagerExternalShotPlan plan)
    {
        var structure = new ShotManagerPortableStructure(
            1,
            plan.Directories.Select((directory) => directory.RelativePath).ToList(),
            plan.ShotOwnedDirectories.Select((directory) =>
                directory.RelativePath).ToList(),
            plan.StructureEntries.Select((entry) =>
                new ShotManagerPortableStructureEntry(
                    entry.EntryId,
                    entry.RelativePath)).ToList());
        structure.Validate("Shot Manager external Shot plan");
        return structure;
    }
}

internal interface IShotManagerIntegrationClient
{
    Task<ShotManagerStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShotManagerCatalogProduction>> GetCatalogAsync(
        CancellationToken cancellationToken = default);

    Task<ShotManagerProductionSnapshot> GetSnapshotAsync(
        string productionId,
        CancellationToken cancellationToken = default);

    Task<ShotManagerExternalShotPlan> PlanShotAsync(
        string productionId,
        string episodeId,
        int shotNumber,
        CancellationToken cancellationToken = default);
}
