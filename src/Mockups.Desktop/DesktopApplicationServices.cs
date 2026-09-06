using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Integrations.ProductionOutput;

namespace Mockups.DesktopEditorShell;

internal sealed record DesktopApplicationDataPorts(
    IProjectPathResolver ProjectPaths,
    IEditorNavigationDataSource Navigation,
    ICoreFieldStore CoreFields,
    IProductionRecordFieldStore ProductionRecordFields,
    IRecordReferenceOverrideStore RecordReferenceOverrides,
    IDesignRecordFieldStore DesignRecordFields,
    IResourceRecordFieldStore ResourceRecordFields,
    IComponentClassFieldStore ComponentFields,
    IVariantHistoryStore VariantHistory,
    IPreviewInputRepository Preview,
    IComponentPreviewInputRepository ComponentPreview,
    IModuleInstanceTimelineStore Timeline,
    IModuleInstanceThemeTokenQuery ModuleInstanceThemes,
    IDictionaryFieldContextRepository Dictionary,
    IEditorChildStore Children,
    IEditorNodeCommandStore NodeCommands,
    IRenderSnapshotDataSource RenderSnapshots,
    IEditorPresentationContextRepository Presentation,
    IModuleInstanceCollectionStore ModuleInstances,
    IIconThemeAssetStore IconThemes,
    IThemeTokenQuery ThemeTokens,
    IComponentDocumentStore Components,
    IRuntimeInputOwnerStore RuntimeInputOwners,
    IRuntimeInputInstanceStore RuntimeInputInstances,
    IModuleInstanceAnimationStore Animation,
    IReferenceUsageQuery ReferenceUsage,
    IExternalMediaUsageQuery ExternalMediaUsage,
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
    ProductionOutputRootStore ProductionOutputRoots,
    ShotManagerDocumentStore ShotManagerDocuments)
{
    public static DesktopApplicationServices Create(
        DesktopApplicationDataPorts data)
    {
        var productionOutputRoots = new ProductionOutputRootStore();
        var shotManagerDocuments = new ShotManagerDocumentStore();
        var operations = new EditorOperationCoordinator();
        return new(
            data,
            new EditorVariantHistoryService(
                data.VariantHistory,
                operations),
            new CoreFieldValueService(data.CoreFields),
            new RecordClassFieldValueService(
                data.ProductionRecordFields,
                data.RecordReferenceOverrides,
                data.DesignRecordFields,
                data.ResourceRecordFields,
                data.Timeline,
                data.ModuleInstanceThemes,
                productionOutputRoots,
                shotManagerDocuments),
            new ComponentClassFieldValueService(
                data.ComponentFields,
                data.Components),
            new ProductionShotContextService(
                new ProductionShotContextDataSource(
                    data.Preview,
                    data.ActorPreview)),
            new EditorWorkspaceCoordinator(data.Navigation),
            operations,
            productionOutputRoots,
            shotManagerDocuments);
    }
}
