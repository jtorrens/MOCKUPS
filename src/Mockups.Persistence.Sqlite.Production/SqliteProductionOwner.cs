using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner :
    IProjectSettingsQuery,
    IModuleInstanceTimelineStore,
    IModuleInstanceAnimationStore
{
    private readonly SqliteProjectContext _context;
    private readonly IShotRepository _shotRepository;
    private readonly IProjectEpisodeRepository _projectEpisodeRepository;
    private readonly IModuleInstanceRepository _moduleInstanceRepository;
    private readonly IModuleInstanceThemeContextService
        _moduleInstanceThemeContextService;
    private readonly IModuleVariantCatalog _moduleVariantCatalog;
    private readonly IComponentVariantConfigCatalog
        _componentVariantConfigCatalog;

    internal SqliteProductionOwner(
        SqliteProjectContext context,
        IModuleVariantCatalog moduleVariantCatalog,
        IComponentVariantConfigCatalog componentVariantConfigCatalog)
    {
        _context = context;
        _moduleVariantCatalog = moduleVariantCatalog;
        _componentVariantConfigCatalog = componentVariantConfigCatalog;
        _shotRepository = new ShotRepository(context);
        _projectEpisodeRepository = new ProjectEpisodeRepository(
            context,
            _shotRepository);
        _moduleInstanceRepository = new ModuleInstanceRepository(context);
        _moduleInstanceThemeContextService =
            new ModuleInstanceThemeContextService(context);
    }

    internal IShotRepository ShotRepository => _shotRepository;

    internal IProjectEpisodeRepository ProjectEpisodeRepository =>
        _projectEpisodeRepository;

    internal IModuleInstanceRepository ModuleInstanceRepository =>
        _moduleInstanceRepository;

    internal IModuleInstanceThemeContextService
        ModuleInstanceThemeContextService =>
            _moduleInstanceThemeContextService;

    private object WriteGate => _context.WriteGate;

    private SqliteConnection OpenConnection() => _context.OpenConnection();

}
