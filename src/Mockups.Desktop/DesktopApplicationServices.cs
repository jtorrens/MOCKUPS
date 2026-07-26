using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell;

internal sealed record DesktopApplicationDataPorts(
    IProjectPathResolver ProjectPaths,
    IEditorNavigationDataSource Navigation,
    ICoreFieldStore CoreFields,
    IRecordClassFieldStore RecordFields,
    IComponentClassFieldStore ComponentFields,
    IVariantHistoryStore VariantHistory,
    IPreviewInputRepository Preview,
    IDictionaryFieldContextRepository Dictionary,
    IEditorNodeCommandStore NodeCommands,
    IProductionNavigationStore ProductionNavigation,
    IEditorPresentationContextRepository Presentation,
    IEditorDomainDialogStore DomainDialogs,
    IComponentDocumentStore Components,
    IEditorHeaderStore Header,
    IEditorCollectionStore Collections,
    IEditorLayoutStore Layouts,
    IActorPreviewRepository ActorPreview);

internal sealed record DesktopApplicationServices(
    DesktopApplicationDataPorts Data,
    EditorVariantHistoryService VariantHistory,
    CoreFieldValueService CoreFieldValues,
    RecordClassFieldValueService RecordClassFieldValues,
    ComponentClassFieldValueService ComponentClassFieldValues,
    ProductionShotContextService ProductionShotContext,
    EditorWorkspaceCoordinator WorkspaceCoordinator)
{
    public static DesktopApplicationServices Create(
        DesktopApplicationDataPorts data) =>
        new(
            data,
            new EditorVariantHistoryService(data.VariantHistory),
            new CoreFieldValueService(data.CoreFields),
            new RecordClassFieldValueService(data.RecordFields),
            new ComponentClassFieldValueService(data.ComponentFields),
            new ProductionShotContextService(
                new ProductionShotContextDataSource(data.Preview)),
            new EditorWorkspaceCoordinator(data.Navigation));
}
