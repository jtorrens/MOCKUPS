using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

public sealed class SqliteProjectSession
{
    internal SqliteProjectSession(
        SqliteProjectEngine engine,
        IEditorLayoutStore layouts)
    {
        ProjectPaths = engine.ProjectPaths;
        Navigation = new SqliteEditorNavigationPort(engine);
        CoreFields = new SqliteCoreFieldPort(engine);
        RecordFields = new SqliteRecordClassFieldPort(engine);
        ComponentFields =
            new SqliteComponentClassFieldPort(engine);
        VariantHistory = new SqliteVariantHistoryPort(engine);
        Preview = new SqlitePreviewInputPort(engine);
        Dictionary =
            new SqliteDictionaryFieldContextPort(engine);
        NodeCommands =
            new SqliteEditorNodeCommandPort(engine);
        ProductionNavigation =
            new SqliteProductionNavigationPort(engine);
        Presentation =
            new SqliteEditorPresentationPort(engine);
        DomainDialogs =
            new SqliteEditorDomainDialogPort(engine);
        Components =
            new SqliteComponentDocumentPort(engine);
        Header = new SqliteEditorHeaderPort(engine);
        Collections = new SqliteEditorCollectionPort(engine);
        Layouts = new SqliteEditorLayoutPort(layouts);
        ActorPreview = new SqliteActorPreviewPort(engine.Resources);
    }

    public IProjectPathResolver ProjectPaths { get; }

    public IEditorNavigationDataSource Navigation { get; }

    public ICoreFieldStore CoreFields { get; }

    public IRecordClassFieldStore RecordFields { get; }

    public IComponentClassFieldStore ComponentFields { get; }

    public IVariantHistoryStore VariantHistory { get; }

    public IPreviewInputRepository Preview { get; }

    public IDictionaryFieldContextRepository Dictionary { get; }

    public IEditorNodeCommandStore NodeCommands { get; }

    public IProductionNavigationStore ProductionNavigation { get; }

    public IEditorPresentationContextRepository Presentation { get; }

    public IEditorDomainDialogStore DomainDialogs { get; }

    public IComponentDocumentStore Components { get; }

    public IEditorHeaderStore Header { get; }

    public IEditorCollectionStore Collections { get; }

    public IEditorLayoutStore Layouts { get; }

    public IActorPreviewRepository ActorPreview { get; }
}
