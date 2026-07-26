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
    IRuntimeInputOwnerStore,
    IRuntimeInputInstanceStore,
    IModuleInstanceTimelineStore,
    IModuleInstanceAnimationStore,
    IModuleInstanceCollectionStore,
    IReferenceUsageQuery,
    IRenderSnapshotDataSource,
    IProductionNavigationStore
{
}
