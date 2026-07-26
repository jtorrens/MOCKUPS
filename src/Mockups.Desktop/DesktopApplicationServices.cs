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
    IComponentPreviewInputRepository ComponentPreview,
    IModuleInstanceTimelineStore Timeline,
    IModuleInstanceThemeTokenQuery ModuleInstanceThemes,
    IDictionaryFieldContextRepository Dictionary,
    IEditorChildStore Children,
    IEditorNodeCommandStore NodeCommands,
    IProductionNavigationStore ProductionNavigation,
    IEditorPresentationContextRepository Presentation,
    IModuleInstanceCollectionStore ModuleInstances,
    IIconThemeAssetStore IconThemes,
    IThemeTokenQuery ThemeTokens,
    IComponentDocumentStore Components,
    IRuntimeInputOwnerStore RuntimeInputOwners,
    IRuntimeInputInstanceStore RuntimeInputInstances,
    IModuleInstanceAnimationStore Animation,
    IReferenceUsageQuery ReferenceUsage,
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
    EditorOperationCoordinator Operations,
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
                data.ModuleInstanceThemes,
                productionOutputRoots),
            new ComponentClassFieldValueService(data.ComponentFields),
            new ProductionShotContextService(
                new ProductionShotContextDataSource(
                    data.Preview,
                    data.ActorPreview)),
            new EditorWorkspaceCoordinator(data.Navigation),
            new EditorOperationCoordinator(),
            productionOutputRoots);
    }
}
