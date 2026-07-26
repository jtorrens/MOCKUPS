namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteDesignOwner
{
    private readonly IAppModuleRepository _appModuleRepository;
    private readonly IComponentClassRepository _componentClassRepository;

    internal SqliteDesignOwner(SqliteProjectContext context)
    {
        _appModuleRepository = new AppModuleRepository(context);
        _componentClassRepository = new ComponentClassRepository(context);
    }

    internal IAppModuleRepository AppModuleRepository =>
        _appModuleRepository;

    internal IComponentClassRepository ComponentClassRepository =>
        _componentClassRepository;
}
