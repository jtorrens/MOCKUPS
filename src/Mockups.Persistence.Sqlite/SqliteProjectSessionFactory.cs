using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteProjectSessionFactory
{
    internal static SqliteProjectSession Create(
        SqliteProjectContext context)
    {
        var design = new SqliteDesignOwner(context);
        var production = new SqliteProductionOwner(
            context,
            design,
            design);
        var resources = new SqliteResourceOwner(
            context,
            production.ProjectEpisodeRepository,
            production.ModuleInstanceThemeContextService);
        var componentFieldOptions =
            new ComponentFieldOptionResolver(
                design,
                resources);
        var referenceUsages = new ReferenceUsageService(context);
        var externalMediaUsages = new ExternalMediaUsageService(context);
        var previewInputs = new SqlitePreviewInputPort(
            production,
            design,
            resources);
        var dictionaryContext =
            new SqliteDictionaryFieldContextPort(
                design,
                resources);
        var componentDocuments =
            new SqliteComponentDocumentStore(
                design,
                componentFieldOptions,
                referenceUsages);
        var moduleInstanceCollection =
            new SqliteModuleInstanceCollectionStore(
                context,
                design,
                production,
                resources,
                referenceUsages);
        var coreFields = new SqliteCoreFieldStore(
            context,
            design,
            production,
            resources);
        var children = new SqliteEditorChildStore(
            context,
            design,
            production,
            resources);
        var nodeCommands = new SqliteEditorNodeCommandStore(
            context,
            design,
            production,
            resources,
            referenceUsages,
            coreFields);
        var productionRecordFields =
            new SqliteProductionRecordFieldStore(
                context,
                production,
                design,
                resources);
        var recordReferenceOverrides =
            new SqliteRecordReferenceOverrideStore(
                production,
                resources);
        var designRecordFields =
            new SqliteDesignRecordFieldStore(
                design);
        var resourceRecordFields =
            new SqliteResourceRecordFieldStore(
                resources,
                coreFields);
        var navigation = new SqliteEditorNavigationStore(
            context,
            design,
            production,
            resources,
            referenceUsages);
        var runtimeInputInstances =
            new SqliteRuntimeInputInstanceStore(
                context,
                design,
                production,
                resources);
        var animations =
            new SqliteModuleInstanceAnimationStore(
                production,
                resources);

        new SqliteCurrentDatabaseValidator(
            context,
            design,
            production,
            resources)
            .Validate();

        return new SqliteProjectSession(
            context.ProjectPaths,
            new SqliteEditorNavigationPort(
                navigation.LoadProjectTree),
            new SqliteCoreFieldPort(coreFields),
            new SqliteProductionRecordFieldPort(
                productionRecordFields),
            new SqliteRecordReferenceOverridePort(
                recordReferenceOverrides),
            new SqliteDesignRecordFieldPort(
                designRecordFields),
            new SqliteResourceRecordFieldPort(
                resourceRecordFields),
            new SqliteComponentClassFieldPort(
                componentDocuments),
            new SqliteVariantHistoryPort(design),
            previewInputs,
            new SqliteComponentPreviewInputPort(design),
            new SqliteModuleInstanceTimelinePort(production),
            new SqliteModuleInstanceThemeTokenPort(resources),
            dictionaryContext,
            new SqliteEditorChildPort(children),
            new SqliteEditorNodeCommandPort(nodeCommands),
            new SqliteRenderSnapshotPort(
                previewInputs,
                resources,
                design,
                production,
                resources,
                production),
            new SqliteEditorPresentationPort(resources),
            new SqliteModuleInstanceCollectionPort(
                moduleInstanceCollection),
            new SqliteIconThemeAssetPort(resources),
            new SqliteThemeTokenPort(resources),
            new SqliteComponentDocumentPort(
                componentDocuments),
            new SqliteRuntimeInputOwnerPort(design),
            new SqliteRuntimeInputInstancePort(
                runtimeInputInstances),
            new SqliteModuleInstanceAnimationPort(animations),
            new SqliteReferenceUsagePort(referenceUsages),
            new SqliteExternalMediaUsagePort(externalMediaUsages),
            new SqliteExternalMediaAssetReplacementPort(resources),
            new SqliteEditorLayoutPort(
                new SqliteEditorLayoutStore(context)),
            new SqliteActorPreviewPort(resources));
    }
}
