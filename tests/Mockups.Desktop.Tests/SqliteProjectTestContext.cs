using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteProjectTestContext
{
    private readonly ReferenceUsageService _referenceUsages;
    private readonly IPreviewInputRepository _previewInputs;
    private readonly IDictionaryFieldContextRepository
        _dictionaryContext;

    internal SqliteProjectTestContext(string databasePath)
        : this(new SqliteProjectContext(databasePath))
    {
    }

    internal SqliteProjectTestContext(SqliteProjectContext context)
    {
        Context = context;
        Design = new SqliteDesignOwner(context);
        Production = new SqliteProductionOwner(
            context,
            Design);
        Resources = new SqliteResourceOwner(
            context,
            Production.ProjectEpisodeRepository,
            Production.ModuleInstanceThemeContextService);
        var componentFieldOptions =
            new ComponentFieldOptionResolver(
                Design,
                Resources);
        _referenceUsages = new ReferenceUsageService(context);
        _previewInputs = new SqlitePreviewInputPort(
            Production,
            Design,
            Resources);
        _dictionaryContext =
            new SqliteDictionaryFieldContextPort(
                Design,
                Resources);
        ComponentDocuments =
            new SqliteComponentDocumentStore(
                Design,
                componentFieldOptions,
                _referenceUsages);
        ModuleInstanceCollection =
            new SqliteModuleInstanceCollectionStore(
                context,
                Design,
                Production,
                Resources,
                _referenceUsages);
        CoreFields = new SqliteCoreFieldStore(
            context,
            Design,
            Production,
            Resources);
        Children = new SqliteEditorChildStore(
            context,
            Design,
            Production,
            Resources);
        NodeCommands = new SqliteEditorNodeCommandStore(
            context,
            Design,
            Production,
            Resources,
            _referenceUsages,
            CoreFields);
        RecordFields = new SqliteRecordClassFieldStore(
            context,
            Design,
            Production,
            Resources,
            CoreFields);
        Navigation = new SqliteEditorNavigationStore(
            context,
            Design,
            Production,
            Resources,
            _referenceUsages);
        RuntimeInputInstances =
            new SqliteRuntimeInputInstanceStore(
                context,
                Design,
                Production,
                Resources);

        new SqliteCurrentDatabaseValidator(
            context,
            Design,
            Production,
            Resources)
            .Validate();
    }

    internal IProjectPathResolver ProjectPaths =>
        Context.ProjectPaths;

    internal SqliteProjectContext Context { get; }

    internal SqliteDesignOwner Design { get; }

    internal SqliteProductionOwner Production { get; }

    internal SqliteResourceOwner Resources { get; }

    internal IReferenceUsageQuery ReferenceUsages =>
        _referenceUsages;

    internal IPreviewInputRepository PreviewInputs =>
        _previewInputs;

    internal IDictionaryFieldContextRepository DictionaryContext =>
        _dictionaryContext;

    internal SqliteComponentDocumentStore ComponentDocuments
    {
        get;
    }

    internal SqliteModuleInstanceCollectionStore
        ModuleInstanceCollection
    {
        get;
    }

    internal SqliteCoreFieldStore CoreFields { get; }

    internal SqliteEditorChildStore Children { get; }

    internal SqliteEditorNodeCommandStore NodeCommands { get; }

    internal IRecordClassFieldStore RecordFields { get; }

    internal SqliteEditorNavigationStore Navigation { get; }

    internal SqliteRuntimeInputInstanceStore RuntimeInputInstances
    {
        get;
    }
}
