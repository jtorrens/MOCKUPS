namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine :
    ICoreFieldStore,
    IRecordClassFieldStore,
    IComponentClassFieldStore,
    IPreviewInputRepository,
    IEditorPresentationContextRepository,
    IDictionaryFieldContextRepository,
    IEditorChildStore,
    IEditorNodeCommandStore,
    IComponentDocumentStore,
    IModuleInstanceTimelineStore,
    IModuleInstanceCollectionStore,
    IReferenceUsageQuery,
    IRenderSnapshotDataSource,
    IProductionNavigationStore
{
}
