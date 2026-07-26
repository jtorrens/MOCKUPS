#nullable disable

// Each adapter deliberately implements only one public Application port.
// Repetition here prevents a consumer from recovering unrelated SQLite
// capabilities by casting the dependency it received.

namespace Mockups.DesktopEditorShell.Data;
internal sealed class SqliteEditorNavigationPort(
    Mockups.DesktopEditorShell.EditorShell.IEditorNavigationDataSource target)
    : Mockups.DesktopEditorShell.EditorShell.IEditorNavigationDataSource
{
    private readonly Mockups.DesktopEditorShell.EditorShell.IEditorNavigationDataSource _target = target;

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode> LoadProjectTree(
        )
        => ((Mockups.DesktopEditorShell.EditorShell.IEditorNavigationDataSource)_target).LoadProjectTree();

}

internal sealed class SqliteCoreFieldPort(
    Mockups.DesktopEditorShell.Data.ICoreFieldStore target)
    : Mockups.DesktopEditorShell.Data.ICoreFieldStore
{
    private readonly Mockups.DesktopEditorShell.Data.ICoreFieldStore _target = target;

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode RenameDirectNode(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node,
        string name)
        => ((Mockups.DesktopEditorShell.Data.ICoreFieldStore)_target).RenameDirectNode(node, name);

    public void UpdateNode(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.ICoreFieldStore)_target).UpdateNode(node);

}

internal sealed class SqliteRecordClassFieldPort(
    Mockups.DesktopEditorShell.Data.IRecordClassFieldStore target)
    : Mockups.DesktopEditorShell.Data.IRecordClassFieldStore
{
    private readonly Mockups.DesktopEditorShell.Data.IRecordClassFieldStore _target = target;

    public string GetActorFieldValue(
        string actorId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetActorFieldValue(actorId, fieldId);

    public Mockups.DesktopEditorShell.Data.ActorSettings GetActorSettings(
        string actorId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetActorSettings(actorId);

    public string GetAppConfigFieldValue(
        string appId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetAppConfigFieldValue(appId, fieldId);

    public string GetAppMetadataFieldValue(
        string appId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetAppMetadataFieldValue(appId, fieldId);

    public Mockups.DesktopEditorShell.Data.AppSettings GetAppSettings(
        string appId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetAppSettings(appId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetComponentVariantReferenceOptionsByType(
        string projectId,
        string componentType,
        bool includeNone)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetComponentVariantReferenceOptionsByType(projectId, componentType, includeNone);

    public string GetDeviceMetricFieldValue(
        string deviceId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetDeviceMetricFieldValue(deviceId, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetDeviceOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetDeviceOptions(projectId);

    public Mockups.DesktopEditorShell.Data.DeviceSettings GetDeviceSettings(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetDeviceSettings(deviceId);

    public Mockups.DesktopEditorShell.Data.EpisodeSettings GetEpisodeSettings(
        string episodeId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetEpisodeSettings(episodeId);

    public string GetIconThemeFieldValue(
        string iconThemeId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetIconThemeFieldValue(iconThemeId, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetIconThemeOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetIconThemeOptions(projectId);

    public string GetModuleConfigFieldValue(
        string moduleId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetModuleConfigFieldValue(moduleId, fieldId);

    public string GetModuleInstanceVariantReference(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetModuleInstanceVariantReference(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetModuleSettings(moduleId);

    public string GetModuleVariantConfigFieldValue(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetModuleVariantConfigFieldValue(node, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetModuleVariantOptions(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetModuleVariantOptions(moduleId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetModuleVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetNavigationBarComponentVariantOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetNavigationBarComponentVariantOptions(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetPaletteColorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetPaletteColorOptions(projectId);

    public Mockups.DesktopEditorShell.Data.PaletteColorSettings GetPaletteColorSettings(
        string colorId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetPaletteColorSettings(colorId);

    public string GetProductionFontFieldValue(
        string fontId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetProductionFontFieldValue(fontId, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetProductionFontOptions(
        string projectId,
        string category)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetProductionFontOptions(projectId, category);

    public Mockups.DesktopEditorShell.Data.ProjectSettings GetProjectSettings(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetProjectSettings(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetRequiredActorOptions(projectId);

    public string GetShotOwnerDeviceName(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetShotOwnerDeviceName(shotId);

    public string GetShotRenderName(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetShotRenderName(shotId);

    public Mockups.DesktopEditorShell.Data.ShotSettings GetShotSettings(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetShotSettings(shotId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetStatusBarComponentVariantOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetStatusBarComponentVariantOptions(projectId);

    public string GetThemeFieldValue(
        string themeId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetThemeFieldValue(themeId, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetThemeOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetThemeOptions(projectId);

    public Mockups.DesktopEditorShell.Data.ThemeSettings GetThemeSettings(
        string themeId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetThemeSettings(themeId);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode RenameDirectNode(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node,
        string name)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).RenameDirectNode(node, name);

    public void UpdateActorField(
        string actorId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateActorField(actorId, fieldId, value);

    public void UpdateAppField(
        string appId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateAppField(appId, fieldId, value);

    public void UpdateDeviceField(
        string deviceId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateDeviceField(deviceId, fieldId, value);

    public void UpdateEpisodeField(
        string episodeId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateEpisodeField(episodeId, fieldId, value);

    public void UpdateModuleField(
        string moduleId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateModuleField(moduleId, fieldId, value);

    public void UpdateModuleInstanceField(
        string moduleInstanceId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateModuleInstanceField(moduleInstanceId, fieldId, value);

    public void UpdateModuleVariantField(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateModuleVariantField(node, fieldId, value);

    public void UpdatePaletteColorField(
        string colorId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdatePaletteColorField(colorId, fieldId, value);

    public void UpdateProductionFontField(
        string fontId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateProductionFontField(fontId, fieldId, value);

    public void UpdateProjectField(
        string projectId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateProjectField(projectId, fieldId, value);

    public void UpdateShotField(
        string shotId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateShotField(shotId, fieldId, value);

    public void UpdateThemeField(
        string themeId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).UpdateThemeField(themeId, fieldId, value);

}

internal sealed class SqliteComponentClassFieldPort(
    SqliteComponentDocumentStore target)
    : Mockups.DesktopEditorShell.Data.IComponentClassFieldStore
{
    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateComponentClassFieldValue(
        string componentClassId,
        string fieldId)
        => target.CreateComponentClassFieldValue(componentClassId, fieldId);

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateComponentVariantFieldValue(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode,
        string fieldId)
        => target.CreateComponentVariantFieldValue(variantNode, fieldId);

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateEmbeddedComponentFieldValue(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
        => target.CreateEmbeddedComponentFieldValue(ownerNode, slots, embeddedFieldId);

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId)
        => target.CreateRuntimeComponentOverrideFieldValue(projectId, baseConfigJson, overrides, slots, fieldId);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentClassSettings(
        string componentClassId)
        => target.GetComponentClassSettings(componentClassId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ComponentVariantReferenceUsage> GetComponentVariantReferenceUsageDetails(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => target.GetComponentVariantReferenceUsageDetails(node);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => target.GetComponentVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        string projectId,
        string componentType,
        string excludedComponentClassId)
        => target.GetEmbeddedComponentUsages(projectId, componentType, excludedComponentClassId);

    public string GetEmbeddedComponentVariantName(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => target.GetEmbeddedComponentVariantName(ownerNode, slots);

    public string GetRuntimeComponentVariantName(
        string variantReference,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => target.GetRuntimeComponentVariantName(variantReference, overrides, slots);

    public void UpdateComponentClassField(
        string componentClassId,
        string fieldId,
        string value)
        => target.UpdateComponentClassField(componentClassId, fieldId, value);

    public void UpdateComponentVariantField(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode,
        string fieldId,
        string value)
        => target.UpdateComponentVariantField(variantNode, fieldId, value);

    public void UpdateEmbeddedComponentField(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
        => target.UpdateEmbeddedComponentField(ownerNode, slots, embeddedFieldId, value);

    public void UpdateRuntimeComponentOverride(
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value)
        => target.UpdateRuntimeComponentOverride(overrides, slots, fieldId, value);

}

internal sealed class SqliteVariantHistoryPort(
    Mockups.DesktopEditorShell.Data.IVariantHistoryStore target)
    : Mockups.DesktopEditorShell.Data.IVariantHistoryStore
{
    private readonly Mockups.DesktopEditorShell.Data.IVariantHistoryStore _target = target;

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IVariantHistoryStore)_target).GetComponentVariantSettings(variantNode);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IVariantHistoryStore)_target).GetModuleVariantSettings(variantNode);

}

internal sealed class SqliteEditorPresentationPort(
    Mockups.DesktopEditorShell.Data.IEditorPresentationContextRepository target)
    : Mockups.DesktopEditorShell.Data.IEditorPresentationContextRepository
{
    private readonly Mockups.DesktopEditorShell.Data.IEditorPresentationContextRepository _target = target;

    public string GetProductionFontFieldValue(
        string fontId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IEditorPresentationContextRepository)_target).GetProductionFontFieldValue(fontId, fieldId);

    public Mockups.DesktopEditorShell.Data.ProjectSettings GetProjectSettings(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IEditorPresentationContextRepository)_target).GetProjectSettings(projectId);

    public Mockups.DesktopEditorShell.Data.ThemeSettings GetThemeSettings(
        string themeId)
        => ((Mockups.DesktopEditorShell.Data.IEditorPresentationContextRepository)_target).GetThemeSettings(themeId);

}

internal sealed class SqliteComponentDocumentPort(
    SqliteComponentDocumentStore target)
    : Mockups.DesktopEditorShell.Data.IComponentDocumentStore
{
    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateEmbeddedComponentFieldValue(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
        => target.CreateEmbeddedComponentFieldValue(ownerNode, slots, embeddedFieldId);

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId)
        => target.CreateRuntimeComponentOverrideFieldValue(projectId, baseConfigJson, overrides, slots, fieldId);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentClassSettings(
        string componentClassId)
        => target.GetComponentClassSettings(componentClassId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ComponentVariantReferenceUsage> GetComponentVariantReferenceUsageDetails(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => target.GetComponentVariantReferenceUsageDetails(node);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => target.GetComponentVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        string projectId,
        string componentType,
        string excludedComponentClassId)
        => target.GetEmbeddedComponentUsages(projectId, componentType, excludedComponentClassId);

    public string GetEmbeddedComponentVariantName(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => target.GetEmbeddedComponentVariantName(ownerNode, slots);

    public string GetRuntimeComponentVariantName(
        string variantReference,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => target.GetRuntimeComponentVariantName(variantReference, overrides, slots);

    public void UpdateEmbeddedComponentField(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
        => target.UpdateEmbeddedComponentField(ownerNode, slots, embeddedFieldId, value);

    public void UpdateRuntimeComponentOverride(
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value)
        => target.UpdateRuntimeComponentOverride(overrides, slots, fieldId, value);

}

internal sealed class SqliteEditorLayoutPort(
    Mockups.DesktopEditorShell.Data.IEditorLayoutStore target)
    : Mockups.DesktopEditorShell.Data.IEditorLayoutStore
{
    private readonly Mockups.DesktopEditorShell.Data.IEditorLayoutStore _target = target;

    public Mockups.DesktopEditorShell.EditorShell.EditorLayout LoadEditorLayout(
        string recordClassId)
        => ((Mockups.DesktopEditorShell.Data.IEditorLayoutStore)_target).LoadEditorLayout(recordClassId);

    public void SaveEditorLayout(
        string recordClassId,
        Mockups.DesktopEditorShell.EditorShell.EditorLayout layout)
        => ((Mockups.DesktopEditorShell.Data.IEditorLayoutStore)_target).SaveEditorLayout(recordClassId, layout);

}

internal sealed class SqliteActorPreviewPort(
    Mockups.DesktopEditorShell.Data.IActorPreviewRepository target)
    : Mockups.DesktopEditorShell.Data.IActorPreviewRepository
{
    private readonly Mockups.DesktopEditorShell.Data.IActorPreviewRepository _target = target;

    public string GetActorFieldValue(
        string actorId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetActorFieldValue(actorId, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetActorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetActorOptions(projectId);

    public Mockups.DesktopEditorShell.Data.ActorSettings GetActorSettings(
        string actorId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetActorSettings(actorId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetPaletteColorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetPaletteColorOptions(projectId);

    public Mockups.DesktopEditorShell.Data.ProjectSettings GetProjectSettings(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IProjectSettingsQuery)_target).GetProjectSettings(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetRequiredActorOptions(projectId);

}
