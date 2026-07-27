using Mockups.DesktopEditorShell.Common;
using System;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    private object WriteGate => _context.WriteGate;
    private readonly SqliteProjectContext _context;
    private readonly SqliteDesignOwner _designOwner;
    private readonly SqliteProductionOwner _productionOwner;
    private readonly SqliteResourceOwner _resourceOwner;
    private readonly ComponentFieldOptionResolver
        _componentFieldOptions;
    private readonly ReferenceUsageService _referenceUsageService;
    private readonly IPreviewInputRepository _previewInputs;
    private readonly IDictionaryFieldContextRepository
        _dictionaryContext;
    private readonly SqliteComponentDocumentStore
        _componentDocuments;
    private readonly SqliteModuleInstanceCollectionStore
        _moduleInstanceCollection;
    private readonly SqliteCoreFieldStore _coreFields;
    private readonly SqliteEditorChildStore _children;
    private readonly SqliteEditorNodeCommandStore _nodeCommands;
    private readonly SqliteRecordClassFieldStore _recordFields;
    private readonly SqliteEditorNavigationStore _navigation;
    private readonly SqliteRuntimeInputInstanceStore
        _runtimeInputInstances;

    internal IProjectPathResolver ProjectPaths => _context.ProjectPaths;

    internal SqliteProjectContext Context => _context;

    internal SqliteDesignOwner Design => _designOwner;

    internal SqliteProductionOwner Production => _productionOwner;

    internal SqliteResourceOwner Resources => _resourceOwner;

    internal IReferenceUsageQuery ReferenceUsages =>
        _referenceUsageService;

    internal IPreviewInputRepository PreviewInputs =>
        _previewInputs;

    internal IDictionaryFieldContextRepository DictionaryContext =>
        _dictionaryContext;

    internal SqliteComponentDocumentStore ComponentDocuments =>
        _componentDocuments;

    internal SqliteModuleInstanceCollectionStore
        ModuleInstanceCollection =>
            _moduleInstanceCollection;

    internal SqliteCoreFieldStore CoreFields => _coreFields;

    internal SqliteEditorChildStore Children => _children;

    internal SqliteEditorNodeCommandStore NodeCommands =>
        _nodeCommands;

    internal IRecordClassFieldStore RecordFields => _recordFields;

    internal SqliteEditorNavigationStore Navigation => _navigation;

    internal SqliteRuntimeInputInstanceStore RuntimeInputInstances =>
        _runtimeInputInstances;

    internal SqliteProjectEngine(string databasePath)
        : this(new SqliteProjectContext(databasePath))
    {
    }

    internal SqliteProjectEngine(SqliteProjectContext context)
    {
        _context = context;
        _designOwner = new SqliteDesignOwner(_context);
        _productionOwner = new SqliteProductionOwner(
            _context,
            _designOwner);
        _resourceOwner = new SqliteResourceOwner(
            _context,
            _productionOwner.ProjectEpisodeRepository,
            _productionOwner.ModuleInstanceThemeContextService);
        _componentFieldOptions = new ComponentFieldOptionResolver(
            _designOwner,
            _resourceOwner);
        _referenceUsageService = new ReferenceUsageService(_context);
        _previewInputs = new SqlitePreviewInputPort(
            _productionOwner,
            _designOwner,
            _resourceOwner);
        _dictionaryContext = new SqliteDictionaryFieldContextPort(
            _designOwner,
            _resourceOwner);
        _componentDocuments = new SqliteComponentDocumentStore(
            _designOwner,
            _componentFieldOptions,
            _referenceUsageService);
        _moduleInstanceCollection =
            new SqliteModuleInstanceCollectionStore(
                _context,
                _designOwner,
                _productionOwner,
                _resourceOwner,
                _referenceUsageService);
        _coreFields = new SqliteCoreFieldStore(
            _context,
            _designOwner,
            _productionOwner,
            _resourceOwner);
        _children = new SqliteEditorChildStore(
            _context,
            _designOwner,
            _productionOwner,
            _resourceOwner);
        _nodeCommands = new SqliteEditorNodeCommandStore(
            _context,
            _designOwner,
            _productionOwner,
            _resourceOwner,
            _referenceUsageService,
            _coreFields);
        _recordFields = new SqliteRecordClassFieldStore(
            _context,
            _designOwner,
            _productionOwner,
            _resourceOwner,
            _coreFields);
        _navigation = new SqliteEditorNavigationStore(
            _context,
            _designOwner,
            _productionOwner,
            _resourceOwner,
            _referenceUsageService);
        _runtimeInputInstances = new SqliteRuntimeInputInstanceStore(
            _context,
            _designOwner,
            _productionOwner,
            _resourceOwner);

        new SqliteCurrentDatabaseValidator(
            _context,
            _designOwner,
            _productionOwner,
            _resourceOwner)
            .Validate();
    }

}
