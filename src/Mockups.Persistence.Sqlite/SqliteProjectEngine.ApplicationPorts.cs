namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine :
    ICoreFieldStore,
    IRecordClassFieldStore,
    IComponentClassFieldStore,
    IDictionaryFieldContextRepository,
    IEditorChildStore,
    IEditorNodeCommandStore,
    IComponentDocumentStore,
    IModuleInstanceCollectionStore
{
}
