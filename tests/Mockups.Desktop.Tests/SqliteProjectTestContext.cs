using Mockups.DesktopEditorShell.Common;
using System.Security.Cryptography;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteProjectTestContext
{
    private static readonly object ValidationCacheGate = new();
    private static readonly HashSet<string> ValidatedFixtureKeys =
        new(StringComparer.Ordinal);
    internal IModuleInstanceAnimationStore Animations { get; }
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
        Design = new SqliteDesignOwner(context, (connection, change) =>
            SqliteModuleVariantDocumentCommit.Commit(context, connection, change, Production!));
        Production = new SqliteProductionOwner(
            context,
            Design,
            Design,
            new SqliteProductionRuntimeReferences(new ActorRepository(context)));
        Resources = new SqliteResourceOwner(
            context,
            Production.ProjectEpisodeRepository,
            Production.ModuleInstanceThemeContextService);
        Animations = new SqliteModuleInstanceAnimationStore(context, Production);
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
                Production);
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
        ProductionRecordFields =
            new SqliteProductionRecordFieldStore(
                context,
                Production);
        RecordReferenceOverrides =
            new SqliteRecordReferenceOverrideStore(
                Production,
                Resources);
        DesignRecordFields =
            new SqliteDesignRecordFieldStore(
                Design);
        ResourceRecordFields =
            new SqliteResourceRecordFieldStore(
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
                Production);

        ValidateFixtureOnce(
            context,
            Design,
            Production,
            Resources);
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

    internal IProductionRecordFieldStore ProductionRecordFields
    {
        get;
    }

    internal IRecordReferenceOverrideStore
        RecordReferenceOverrides
    {
        get;
    }

    internal IDesignRecordFieldStore DesignRecordFields
    {
        get;
    }

    internal IResourceRecordFieldStore ResourceRecordFields
    {
        get;
    }

    internal SqliteEditorNavigationStore Navigation { get; }

    internal SqliteRuntimeInputInstanceStore RuntimeInputInstances
    {
        get;
    }

    private static void ValidateFixtureOnce(
        SqliteProjectContext context,
        SqliteDesignOwner design,
        SqliteProductionOwner production,
        SqliteResourceOwner resources)
    {
        var databaseHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(context.DatabasePath)));
        var fixtureKey = $"{context.ProjectPaths.ProjectRoot}|{databaseHash}";
        lock (ValidationCacheGate)
        {
            if (ValidatedFixtureKeys.Contains(fixtureKey)) return;
        }

        new SqliteCurrentDatabaseValidator(
            context,
            design,
            production,
            resources)
            .Validate();

        lock (ValidationCacheGate)
        {
            ValidatedFixtureKeys.Add(fixtureKey);
        }
    }
}
