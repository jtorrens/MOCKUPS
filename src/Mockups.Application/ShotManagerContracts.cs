using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.Integrations.ShotManager;

public sealed record ShotManagerStatus(
    bool Connected,
    string Message);

public sealed record ShotManagerCatalogProduction(
    string Id,
    string Name,
    string Code,
    string ProductionType,
    string? SeriesShotStructure);

public sealed record ShotManagerSeason(
    string Id,
    string ProductionId,
    int Number,
    string Code,
    string? Name);

public sealed record ShotManagerEpisode(
    string Id,
    string ProductionId,
    string SeasonId,
    int Number,
    string Code,
    string? Title);

public sealed record ShotManagerProductionSnapshot(
    string RootPath,
    ShotManagerCatalogProduction Production,
    IReadOnlyList<ShotManagerSeason> Seasons,
    IReadOnlyList<ShotManagerEpisode> Episodes);

public sealed record ShotManagerEpisodeAssociationChoice(
    string ExternalEpisodeId,
    string? LocalEpisodeId);

public sealed record ShotManagerPlanDirectory(
    string RelativePath,
    string ResolvedPath);

public sealed record ShotManagerPlanEntry(
    string EntryId,
    string RelativePath,
    string ResolvedPath);

public sealed record ShotManagerPlanOutputContract(
    string EntryId,
    string RelativeDirectory,
    string FileNamePrefix,
    int VersionPadding);

public sealed record ShotManagerExternalShotPlan(
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
    IReadOnlyList<ShotManagerPlanEntry> StructureEntries,
    IReadOnlyList<ShotManagerPlanOutputContract> OutputContracts);

public interface IShotManagerIntegrationClient
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
