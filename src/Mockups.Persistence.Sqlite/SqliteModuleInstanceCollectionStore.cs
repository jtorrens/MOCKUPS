using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteModuleInstanceCollectionStore
{
    private readonly SqliteProjectContext _context;
    private readonly SqliteDesignOwner _design;
    private readonly SqliteProductionOwner _production;

    internal SqliteModuleInstanceCollectionStore(
        SqliteProjectContext context,
        SqliteDesignOwner design,
        SqliteProductionOwner production)
    {
        _context = context;
        _design = design;
        _production = production;
    }

    internal IReadOnlyList<ShotModuleChoice> GetAvailableShotModules(
        string shotId)
    {
        using var connection = _context.OpenConnection();
        var shot = _production.ShotRepository.Get(connection, shotId);
        var apps = _design.AppModuleRepository
            .QueryApps(connection)
            .OrderBy((app) => app.SortOrder)
            .ThenBy((app) => app.Name)
            .ToDictionary((app) => app.Id, StringComparer.Ordinal);
        return _design.AppModuleRepository
            .QueryModules(connection)
            .Where((module) => apps.ContainsKey(module.AppId))
            .OrderBy((module) => apps[module.AppId].SortOrder)
            .ThenBy((module) => apps[module.AppId].Name)
            .ThenBy((module) => module.SortOrder)
            .ThenBy((module) => module.Name)
            .Select((module) => new ShotModuleChoice(
                module.Id,
                module.Name,
                apps[module.AppId].Name,
                module.AppId,
                module.RecordClassId))
            .ToList();
    }

    internal IReadOnlyList<FieldOption> GetModuleVariantOptions(
        string moduleId) =>
        _design.GetModuleVariantOptions(moduleId);

}
