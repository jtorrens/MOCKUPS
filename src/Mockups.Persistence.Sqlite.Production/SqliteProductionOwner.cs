using Microsoft.Data.Sqlite;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner :
    IProjectSettingsQuery,
    IShotManagerProjectStore
{
    private readonly SqliteProjectContext _context;
    private readonly IShotRepository _shotRepository;
    private readonly IProjectEpisodeRepository _projectEpisodeRepository;
    private readonly IModuleInstanceRepository _moduleInstanceRepository;
    private readonly IModuleInstanceThemeContextService
        _moduleInstanceThemeContextService;
    private readonly IShotManagerIntegrationRepository
        _shotManagerIntegrationRepository;

    internal SqliteProductionOwner(SqliteProjectContext context)
    {
        _context = context;
        _shotRepository = new ShotRepository(context);
        _projectEpisodeRepository = new ProjectEpisodeRepository(
            context,
            _shotRepository);
        _moduleInstanceRepository = new ModuleInstanceRepository(context);
        _moduleInstanceThemeContextService =
            new ModuleInstanceThemeContextService(context);
        _shotManagerIntegrationRepository =
            new ShotManagerIntegrationRepository(context);
    }

    internal IShotRepository ShotRepository => _shotRepository;

    internal IProjectEpisodeRepository ProjectEpisodeRepository =>
        _projectEpisodeRepository;

    internal IModuleInstanceRepository ModuleInstanceRepository =>
        _moduleInstanceRepository;

    internal IModuleInstanceThemeContextService
        ModuleInstanceThemeContextService =>
            _moduleInstanceThemeContextService;

    internal IShotManagerIntegrationRepository
        ShotManagerIntegrationRepository =>
            _shotManagerIntegrationRepository;

    private object WriteGate => _context.WriteGate;

    private SqliteConnection OpenConnection() => _context.OpenConnection();
}
