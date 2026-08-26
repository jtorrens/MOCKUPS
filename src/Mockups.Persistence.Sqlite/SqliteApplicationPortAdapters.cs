using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

// Each adapter implements one public Application capability. Consumers cannot
// recover sibling SQLite capabilities by casting the dependency they receive.
internal sealed class SqliteEditorNavigationPort(
    Func<IReadOnlyList<ProjectTreeNode>> loadProjectTree)
    : IEditorNavigationDataSource
{
    public IReadOnlyList<ProjectTreeNode> LoadProjectTree() =>
        loadProjectTree();
}

internal sealed class SqliteCoreFieldPort(
    SqliteCoreFieldStore target)
    : ICoreFieldStore
{
    public ProjectTreeNode RenameDirectNode(
        ProjectTreeNode node,
        string name) =>
        target.RenameDirectNode(node, name);

    public void UpdateNode(ProjectTreeNode node) =>
        target.UpdateNode(node);
}

internal sealed class SqliteProductionRecordFieldPort(
    IProductionRecordFieldStore target)
    : IProductionRecordFieldStore
{
    public ProjectSettings GetProjectSettings(string projectId) =>
        target.GetProjectSettings(projectId);

    public void UpdateProjectField(
        string projectId,
        string fieldId,
        string value) =>
        target.UpdateProjectField(projectId, fieldId, value);

    public void ConnectShotManagerProduction(
        string projectId,
        ShotManagerReadonlyProduction production,
        string workstreamName,
        string folderName) =>
        target.ConnectShotManagerProduction(
            projectId,
            production,
            workstreamName,
            folderName);

    public void SetShotManagerProductionEnabled(
        string projectId,
        bool enabled) =>
        target.SetShotManagerProductionEnabled(projectId, enabled);

    public void RefreshShotManagerProduction(
        string projectId,
        ShotManagerReadonlyProduction production) =>
        target.RefreshShotManagerProduction(projectId, production);

    public EpisodeSettings GetEpisodeSettings(string episodeId) =>
        target.GetEpisodeSettings(episodeId);

    public void UpdateEpisodeField(
        string episodeId,
        string fieldId,
        string value) =>
        target.UpdateEpisodeField(episodeId, fieldId, value);

    public void AssociateShotManagerEpisode(
        string episodeId,
        ShotManagerReadonlyEpisode? episode) =>
        target.AssociateShotManagerEpisode(episodeId, episode);

    public ShotSettings GetShotSettings(string shotId) =>
        target.GetShotSettings(shotId);

    public void UpdateShotField(
        string shotId,
        string fieldId,
        string value) =>
        target.UpdateShotField(shotId, fieldId, value);

    public void AssociateShotManagerShot(
        string shotId,
        ShotManagerReadonlyShot? shot) =>
        target.AssociateShotManagerShot(shotId, shot);

    public ProductionOutputShotContext GetProductionOutputShotContext(
        string shotId) =>
        target.GetProductionOutputShotContext(shotId);

    public string GetModuleInstanceVariantReference(
        string moduleInstanceId) =>
        target.GetModuleInstanceVariantReference(
            moduleInstanceId);

    public void UpdateModuleInstanceField(
        string moduleInstanceId,
        string fieldId,
        string value) =>
        target.UpdateModuleInstanceField(
            moduleInstanceId,
            fieldId,
            value);
}

internal sealed class SqliteRecordReferenceOverridePort(
    IRecordReferenceOverrideStore target)
    : IRecordReferenceOverrideStore
{
    public string GetOverrideDocument(
        ProjectTreeNode ownerNode,
        string documentFieldId) =>
        target.GetOverrideDocument(
            ownerNode,
            documentFieldId);

    public void UpdateOverrideDocument(
        ProjectTreeNode ownerNode,
        string documentFieldId,
        string overridesJson) =>
        target.UpdateOverrideDocument(
            ownerNode,
            documentFieldId,
            overridesJson);
}

internal sealed class SqliteDesignRecordFieldPort(
    IDesignRecordFieldStore target)
    : IDesignRecordFieldStore
{
    public AppSettings GetAppSettings(string appId) =>
        target.GetAppSettings(appId);

    public void UpdateAppField(
        string appId,
        string fieldId,
        string value) =>
        target.UpdateAppField(appId, fieldId, value);

    public string GetAppConfigFieldValue(
        string appId,
        string fieldId) =>
        target.GetAppConfigFieldValue(appId, fieldId);

    public string GetAppMetadataFieldValue(
        string appId,
        string fieldId) =>
        target.GetAppMetadataFieldValue(appId, fieldId);

    public ModuleSettings GetModuleSettings(string moduleId) =>
        target.GetModuleSettings(moduleId);

    public string GetModuleConfigFieldValue(
        string moduleId,
        string fieldId) =>
        target.GetModuleConfigFieldValue(moduleId, fieldId);

    public ModuleSettings GetModuleVariantSettings(
        ProjectTreeNode variantNode) =>
        target.GetModuleVariantSettings(variantNode);

    public string GetModuleVariantConfigFieldValue(
        ProjectTreeNode node,
        string fieldId) =>
        target.GetModuleVariantConfigFieldValue(node, fieldId);

    public IReadOnlyList<FieldOption> GetModuleVariantOptions(
        string moduleId) =>
        target.GetModuleVariantOptions(moduleId);

    public void UpdateModuleField(
        string moduleId,
        string fieldId,
        string value) =>
        target.UpdateModuleField(moduleId, fieldId, value);

    public void UpdateModuleVariantField(
        ProjectTreeNode node,
        string fieldId,
        string value) =>
        target.UpdateModuleVariantField(node, fieldId, value);

    public IReadOnlyList<FieldOption>
        GetComponentVariantReferenceOptionsByType(
            string projectId,
            string componentType,
            bool includeNone = false) =>
        target.GetComponentVariantReferenceOptionsByType(
            projectId,
            componentType,
            includeNone);

    public IReadOnlyList<FieldOption>
        GetStatusBarComponentVariantOptions(string projectId) =>
        target.GetStatusBarComponentVariantOptions(projectId);

    public IReadOnlyList<FieldOption>
        GetNavigationBarComponentVariantOptions(
            string projectId) =>
        target.GetNavigationBarComponentVariantOptions(projectId);
}

internal sealed class SqliteResourceRecordFieldPort(
    IResourceRecordFieldStore target)
    : IResourceRecordFieldStore
{
    public PaletteColorSettings GetPaletteColorSettings(
        string colorId) =>
        target.GetPaletteColorSettings(colorId);

    public IReadOnlyList<FieldOption> GetPaletteColorOptions(
        string projectId) =>
        target.GetPaletteColorOptions(projectId);

    public void UpdatePaletteColorField(
        string colorId,
        string fieldId,
        string value) =>
        target.UpdatePaletteColorField(colorId, fieldId, value);

    public DeviceSettings GetDeviceSettings(string deviceId) =>
        target.GetDeviceSettings(deviceId);

    public string GetDeviceMetricFieldValue(
        string deviceId,
        string fieldId) =>
        target.GetDeviceMetricFieldValue(deviceId, fieldId);

    public IReadOnlyList<FieldOption> GetDeviceOptions(
        string projectId) =>
        target.GetDeviceOptions(projectId);

    public void UpdateDeviceField(
        string deviceId,
        string fieldId,
        string value) =>
        target.UpdateDeviceField(deviceId, fieldId, value);

    public ActorSettings GetActorSettings(string actorId) =>
        target.GetActorSettings(actorId);

    public string GetActorFieldValue(
        string actorId,
        string fieldId) =>
        target.GetActorFieldValue(actorId, fieldId);

    public IReadOnlyList<FieldOption> GetRequiredActorOptions(
        string projectId) =>
        target.GetRequiredActorOptions(projectId);

    public void UpdateActorField(
        string actorId,
        string fieldId,
        string value) =>
        target.UpdateActorField(actorId, fieldId, value);

    public ThemeSettings GetThemeSettings(string themeId) =>
        target.GetThemeSettings(themeId);

    public string GetThemeFieldValue(
        string themeId,
        string fieldId) =>
        target.GetThemeFieldValue(themeId, fieldId);

    public IReadOnlyList<FieldOption> GetThemeOptions(
        string projectId) =>
        target.GetThemeOptions(projectId);

    public void UpdateThemeField(
        string themeId,
        string fieldId,
        string value) =>
        target.UpdateThemeField(themeId, fieldId, value);

    public IReadOnlyList<FieldOption> GetIconThemeOptions(
        string projectId) =>
        target.GetIconThemeOptions(projectId);

    public string GetIconThemeFieldValue(
        string iconThemeId,
        string fieldId) =>
        target.GetIconThemeFieldValue(iconThemeId, fieldId);

    public IReadOnlyList<FieldOption> GetProductionFontOptions(
        string projectId,
        string? category = null) =>
        target.GetProductionFontOptions(projectId, category);

    public string GetProductionFontFieldValue(
        string fontId,
        string fieldId) =>
        target.GetProductionFontFieldValue(fontId, fieldId);

    public void UpdateProductionFontField(
        string fontId,
        string fieldId,
        string value) =>
        target.UpdateProductionFontField(fontId, fieldId, value);

    public ProjectTreeNode RenamePaletteColor(
        ProjectTreeNode node,
        string name) =>
        target.RenamePaletteColor(node, name);
}

internal sealed class SqliteComponentClassFieldPort(
    SqliteComponentDocumentStore target)
    : IComponentClassFieldStore
{
    public FieldValue CreateComponentClassFieldValue(
        string componentClassId,
        string fieldId) =>
        target.CreateComponentClassFieldValue(
            componentClassId,
            fieldId);

    public FieldValue CreateComponentVariantFieldValue(
        ProjectTreeNode variantNode,
        string fieldId) =>
        target.CreateComponentVariantFieldValue(
            variantNode,
            fieldId);

    public void UpdateComponentClassField(
        string componentClassId,
        string fieldId,
        string value) =>
        target.UpdateComponentClassField(
            componentClassId,
            fieldId,
            value);

    public void UpdateComponentVariantField(
        ProjectTreeNode variantNode,
        string fieldId,
        string value) =>
        target.UpdateComponentVariantField(
            variantNode,
            fieldId,
            value);
}

internal sealed class SqliteVariantHistoryPort(
    IVariantHistoryStore target)
    : IVariantHistoryStore
{
    public ComponentClassSettings GetComponentVariantSettings(
        ProjectTreeNode variantNode) =>
        target.GetComponentVariantSettings(variantNode);

    public ModuleSettings GetModuleVariantSettings(
        ProjectTreeNode variantNode) =>
        target.GetModuleVariantSettings(variantNode);
}

internal sealed class SqliteEditorPresentationPort(
    IEditorPresentationContextRepository target)
    : IEditorPresentationContextRepository
{
    public ProjectSettings GetProjectSettings(string projectId) =>
        target.GetProjectSettings(projectId);

    public ThemeSettings GetThemeSettings(string themeId) =>
        target.GetThemeSettings(themeId);

    public string GetProductionFontFieldValue(
        string fontId,
        string fieldId) =>
        target.GetProductionFontFieldValue(fontId, fieldId);
}

internal sealed class SqliteComponentDocumentPort(
    SqliteComponentDocumentStore target)
    : IComponentDocumentStore
{
    public ComponentClassSettings GetComponentClassSettings(
        string componentClassId) =>
        target.GetComponentClassSettings(componentClassId);

    public ComponentClassSettings GetComponentVariantSettings(
        ProjectTreeNode variantNode) =>
        target.GetComponentVariantSettings(variantNode);

    public IReadOnlyList<ComponentVariantReferenceUsage>
        GetComponentVariantReferenceUsageDetails(
            ProjectTreeNode node) =>
        target.GetComponentVariantReferenceUsageDetails(node);

    public IReadOnlyList<EmbeddedComponentUsage>
        GetEmbeddedComponentUsages(
            string projectId,
            string componentType,
            string? excludedComponentClassId = null) =>
        target.GetEmbeddedComponentUsages(
            projectId,
            componentType,
            excludedComponentClassId);

    public string GetEmbeddedComponentVariantName(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        target.GetEmbeddedComponentVariantName(ownerNode, slots);

    public string GetRuntimeComponentVariantName(
        string variantReference,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        target.GetRuntimeComponentVariantName(
            variantReference,
            overrides,
            slots);

    public FieldValue CreateEmbeddedComponentFieldValue(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId) =>
        target.CreateEmbeddedComponentFieldValue(
            ownerNode,
            slots,
            embeddedFieldId);

    public FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId) =>
        target.CreateRuntimeComponentOverrideFieldValue(
            projectId,
            baseConfigJson,
            overrides,
            slots,
            fieldId);

    public void UpdateEmbeddedComponentField(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value) =>
        target.UpdateEmbeddedComponentField(
            ownerNode,
            slots,
            embeddedFieldId,
            value);

    public void UpdateRuntimeComponentOverride(
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value) =>
        target.UpdateRuntimeComponentOverride(
            overrides,
            slots,
            fieldId,
            value);
}

internal sealed class SqliteEditorLayoutPort(
    IEditorLayoutStore target)
    : IEditorLayoutStore
{
    public EditorLayout LoadEditorLayout(string recordClassId) =>
        target.LoadEditorLayout(recordClassId);

    public void SaveEditorLayout(
        string recordClassId,
        EditorLayout layout) =>
        target.SaveEditorLayout(recordClassId, layout);
}

internal sealed class SqliteActorPreviewPort(
    IActorPreviewRepository target)
    : IActorPreviewRepository
{
    public ProjectSettings GetProjectSettings(string projectId) =>
        target.GetProjectSettings(projectId);

    public ActorSettings GetActorSettings(string actorId) =>
        target.GetActorSettings(actorId);

    public string GetActorFieldValue(
        string actorId,
        string fieldId) =>
        target.GetActorFieldValue(actorId, fieldId);

    public IReadOnlyList<FieldOption> GetActorOptions(
        string projectId) =>
        target.GetActorOptions(projectId);

    public IReadOnlyList<FieldOption> GetRequiredActorOptions(
        string projectId) =>
        target.GetRequiredActorOptions(projectId);

    public IReadOnlyList<FieldOption> GetPaletteColorOptions(
        string projectId) =>
        target.GetPaletteColorOptions(projectId);
}
