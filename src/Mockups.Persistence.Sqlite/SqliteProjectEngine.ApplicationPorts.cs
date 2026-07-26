namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine :
    ICoreFieldStore,
    IRecordClassFieldStore,
    IComponentClassFieldStore,
    IPreviewInputRepository,
    IDictionaryFieldContextRepository,
    IEditorChildStore,
    IEditorNodeCommandStore,
    IComponentDocumentStore,
    IModuleInstanceCollectionStore
{
}
