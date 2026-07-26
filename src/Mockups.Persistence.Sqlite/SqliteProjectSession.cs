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
        VariantHistory =
            new SqliteVariantHistoryPort(engine.Design);
        Preview = engine.PreviewInputs;
        ComponentPreview =
            new SqliteComponentPreviewInputPort(engine.Design);
        Timeline =
            new SqliteModuleInstanceTimelinePort(engine.Production);
        ModuleInstanceThemes =
            new SqliteModuleInstanceThemeTokenPort(
                engine.Resources);
        Dictionary = engine.DictionaryContext;
        Children = new SqliteEditorChildPort(engine);
        NodeCommands =
            new SqliteEditorNodeCommandPort(engine);
        RenderSnapshots =
            new SqliteRenderSnapshotPort(
                engine.PreviewInputs,
                engine.Resources,
                engine.Design,
                engine.Production,
                engine.Resources,
                engine.Production);
        Presentation =
            new SqliteEditorPresentationPort(engine.Resources);
        ModuleInstances =
            new SqliteModuleInstanceCollectionPort(engine);
        IconThemes =
            new SqliteIconThemeAssetPort(engine.Resources);
        ThemeTokens = new SqliteThemeTokenPort(engine.Resources);
        Components =
            new SqliteComponentDocumentPort(engine);
        RuntimeInputOwners =
            new SqliteRuntimeInputOwnerPort(engine.Design);
        RuntimeInputInstances =
            new SqliteRuntimeInputInstancePort(
                new SqliteRuntimeInputInstanceStore(
                    engine.Context,
                    engine.Design,
                    engine.Production,
                    engine.Resources));
        Animation =
            new SqliteModuleInstanceAnimationPort(
                engine.Production);
        ReferenceUsage =
            new SqliteReferenceUsagePort(engine.ReferenceUsages);
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
