using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Integrations.ProductionOutput;

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
    EditorWorkspaceCoordinator WorkspaceCoordinator,
    ProductionOutputRootStore ProductionOutputRoots)
{
    public static DesktopApplicationServices Create(
        DesktopApplicationDataPorts data)
    {
        var productionOutputRoots = new ProductionOutputRootStore();
        return new(
            data,
            new EditorVariantHistoryService(data.VariantHistory),
            new CoreFieldValueService(data.CoreFields),
            new RecordClassFieldValueService(
                data.RecordFields,
                productionOutputRoots),
            new ComponentClassFieldValueService(data.ComponentFields),
            new ProductionShotContextService(
                new ProductionShotContextDataSource(data.Preview)),
            new EditorWorkspaceCoordinator(data.Navigation),
            productionOutputRoots);
    }
}
