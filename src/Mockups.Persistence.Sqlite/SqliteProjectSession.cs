using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

public sealed class SqliteProjectSession
{
    internal SqliteProjectSession(
        IProjectPathResolver projectPaths,
        IEditorNavigationDataSource navigation,
        ICoreFieldStore coreFields,
        IProductionRecordFieldStore productionRecordFields,
        IDesignRecordFieldStore designRecordFields,
        IResourceRecordFieldStore resourceRecordFields,
        IComponentClassFieldStore componentFields,
        IVariantHistoryStore variantHistory,
        IPreviewInputRepository preview,
        IComponentPreviewInputRepository componentPreview,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes,
        IDictionaryFieldContextRepository dictionary,
        IEditorChildStore children,
        IEditorNodeCommandStore nodeCommands,
        IRenderSnapshotDataSource renderSnapshots,
        IEditorPresentationContextRepository presentation,
        IModuleInstanceCollectionStore moduleInstances,
        IIconThemeAssetStore iconThemes,
        IThemeTokenQuery themeTokens,
        IComponentDocumentStore components,
        IRuntimeInputOwnerStore runtimeInputOwners,
        IRuntimeInputInstanceStore runtimeInputInstances,
        IModuleInstanceAnimationStore animation,
        IReferenceUsageQuery referenceUsage,
        IEditorLayoutStore layouts,
        IActorPreviewRepository actorPreview)
    {
        ProjectPaths = projectPaths;
        Navigation = navigation;
        CoreFields = coreFields;
        ProductionRecordFields = productionRecordFields;
        DesignRecordFields = designRecordFields;
        ResourceRecordFields = resourceRecordFields;
        ComponentFields = componentFields;
        VariantHistory = variantHistory;
        Preview = preview;
        ComponentPreview = componentPreview;
        Timeline = timeline;
        ModuleInstanceThemes = moduleInstanceThemes;
        Dictionary = dictionary;
        Children = children;
        NodeCommands = nodeCommands;
        RenderSnapshots = renderSnapshots;
        Presentation = presentation;
        ModuleInstances = moduleInstances;
        IconThemes = iconThemes;
        ThemeTokens = themeTokens;
        Components = components;
        RuntimeInputOwners = runtimeInputOwners;
        RuntimeInputInstances = runtimeInputInstances;
        Animation = animation;
        ReferenceUsage = referenceUsage;
        Layouts = layouts;
        ActorPreview = actorPreview;
    }

    public IProjectPathResolver ProjectPaths { get; }

    public IEditorNavigationDataSource Navigation { get; }

    public ICoreFieldStore CoreFields { get; }

    public IProductionRecordFieldStore ProductionRecordFields
    {
        get;
    }

    public IDesignRecordFieldStore DesignRecordFields
    {
        get;
    }

    public IResourceRecordFieldStore ResourceRecordFields
    {
        get;
    }

    public IComponentClassFieldStore ComponentFields { get; }

    public IVariantHistoryStore VariantHistory { get; }

    public IPreviewInputRepository Preview { get; }

    public IComponentPreviewInputRepository ComponentPreview { get; }

    public IModuleInstanceTimelineStore Timeline { get; }

    public IModuleInstanceThemeTokenQuery ModuleInstanceThemes { get; }

    public IDictionaryFieldContextRepository Dictionary { get; }

    public IEditorChildStore Children { get; }

    public IEditorNodeCommandStore NodeCommands { get; }

    public IRenderSnapshotDataSource RenderSnapshots { get; }

    public IEditorPresentationContextRepository Presentation { get; }

    public IModuleInstanceCollectionStore ModuleInstances { get; }

    public IIconThemeAssetStore IconThemes { get; }

    public IThemeTokenQuery ThemeTokens { get; }

    public IComponentDocumentStore Components { get; }

    public IRuntimeInputOwnerStore RuntimeInputOwners { get; }

    public IRuntimeInputInstanceStore RuntimeInputInstances { get; }

    public IModuleInstanceAnimationStore Animation { get; }

    public IReferenceUsageQuery ReferenceUsage { get; }

    public IEditorLayoutStore Layouts { get; }

    public IActorPreviewRepository ActorPreview { get; }
}
