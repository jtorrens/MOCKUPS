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

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceEffectiveContractJson(moduleInstanceId);

    public string GetModuleInstanceModuleName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceSettings(moduleInstanceId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceThemeTokensJson(moduleInstanceId);

    public string GetModuleInstanceTransitionType(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceTransitionType(moduleInstanceId);

    public string GetModuleInstanceVariantReference(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IRecordClassFieldStore)_target).GetModuleInstanceVariantReference(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceVariantSettings(moduleInstanceId);

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

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetShotModuleInstanceSlots(shotId);

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
    Mockups.DesktopEditorShell.Data.IComponentClassFieldStore target)
    : Mockups.DesktopEditorShell.Data.IComponentClassFieldStore
{
    private readonly Mockups.DesktopEditorShell.Data.IComponentClassFieldStore _target = target;

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateComponentClassFieldValue(
        string componentClassId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IComponentClassFieldStore)_target).CreateComponentClassFieldValue(componentClassId, fieldId);

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateComponentVariantFieldValue(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IComponentClassFieldStore)_target).CreateComponentVariantFieldValue(variantNode, fieldId);

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateEmbeddedComponentFieldValue(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).CreateEmbeddedComponentFieldValue(ownerNode, slots, embeddedFieldId);

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).CreateRuntimeComponentOverrideFieldValue(projectId, baseConfigJson, overrides, slots, fieldId);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentClassSettings(
        string componentClassId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetComponentClassSettings(componentClassId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ComponentVariantReferenceUsage> GetComponentVariantReferenceUsageDetails(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetComponentVariantReferenceUsageDetails(node);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetComponentVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        string projectId,
        string componentType,
        string excludedComponentClassId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetEmbeddedComponentUsages(projectId, componentType, excludedComponentClassId);

    public string GetEmbeddedComponentVariantName(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetEmbeddedComponentVariantName(ownerNode, slots);

    public string GetRuntimeComponentVariantName(
        string variantReference,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetRuntimeComponentVariantName(variantReference, overrides, slots);

    public void UpdateComponentClassField(
        string componentClassId,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IComponentClassFieldStore)_target).UpdateComponentClassField(componentClassId, fieldId, value);

    public void UpdateComponentVariantField(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IComponentClassFieldStore)_target).UpdateComponentVariantField(variantNode, fieldId, value);

    public void UpdateEmbeddedComponentField(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).UpdateEmbeddedComponentField(ownerNode, slots, embeddedFieldId, value);

    public void UpdateRuntimeComponentOverride(
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).UpdateRuntimeComponentOverride(overrides, slots, fieldId, value);

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

internal sealed class SqlitePreviewInputPort(
    Mockups.DesktopEditorShell.Data.IPreviewInputRepository target)
    : Mockups.DesktopEditorShell.Data.IPreviewInputRepository
{
    private readonly Mockups.DesktopEditorShell.Data.IPreviewInputRepository _target = target;

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

    public Mockups.DesktopEditorShell.Data.AppSettings GetAppSettings(
        string appId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetAppSettings(appId);

    public string GetComponentClassBaseConfigsJson(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentClassBaseConfigsJson(projectId);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentClassSettings(
        string componentClassId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentClassSettings(componentClassId);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantConfig(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).GetComponentVariantConfig(variantReference);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantRuntimeContract(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).GetComponentVariantRuntimeContract(variantReference);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetDeviceOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDeviceOptions(projectId);

    public Mockups.DesktopEditorShell.Common.DevicePreviewMetrics GetDevicePreviewMetrics(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDevicePreviewMetrics(deviceId);

    public Mockups.DesktopEditorShell.Data.DeviceSettings GetDeviceSettings(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDeviceSettings(deviceId);

    public Mockups.DesktopEditorShell.Data.IconThemeSettings GetIconThemeSettings(
        string iconThemeId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetIconThemeSettings(iconThemeId);

    public Mockups.DesktopEditorShell.Data.AppSettings GetModuleAppSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleAppSettings(moduleId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceEffectiveContractJson(moduleInstanceId);

    public string GetModuleInstanceModuleName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceSettings(moduleInstanceId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceThemeTokensJson(moduleInstanceId);

    public string GetModuleInstanceTransitionType(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceTransitionType(moduleInstanceId);

    public string GetModuleInstanceVariantName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleInstanceVariantName(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceVariantSettings(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleSettings(moduleId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyDictionary<string, string> GetPaletteColorMap(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetPaletteColorMap(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetPaletteColorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetPaletteColorOptions(projectId);

    public System.Collections.Generic.IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetPaletteNeutralMap(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ProductionFontFace> GetProductionFontFaces(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetProductionFontFaces(projectId);

    public Mockups.DesktopEditorShell.Data.ProjectSettings GetProjectSettings(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IProjectSettingsQuery)_target).GetProjectSettings(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetRequiredActorOptions(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetShotModuleInstanceSlots(shotId);

    public Mockups.DesktopEditorShell.Data.ShotSettings GetShotSettings(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetShotSettings(shotId);

    public string GetThemeFieldValue(
        string themeId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeFieldValue(themeId, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetThemeOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeOptions(projectId);

    public Mockups.DesktopEditorShell.Data.ThemeSettings GetThemeSettings(
        string themeId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeSettings(themeId);

    public string ValidateComponentVariantReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).ValidateComponentVariantReferenceValue(projectId, componentType, reference, allowEmpty);

    public string ValidateComponentVariantReferencesForPreview(
        string projectId,
        string configJson)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).ValidateComponentVariantReferencesForPreview(projectId, configJson);

}

internal sealed class SqliteDictionaryFieldContextPort(
    Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository target)
    : Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository
{
    private readonly Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository _target = target;

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

    public Mockups.DesktopEditorShell.Data.AppSettings GetAppSettings(
        string appId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetAppSettings(appId);

    public string GetComponentClassBaseConfigsJson(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentClassBaseConfigsJson(projectId);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentClassSettings(
        string componentClassId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentClassSettings(componentClassId);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantConfig(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).GetComponentVariantConfig(variantReference);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetComponentVariantReferenceOptions(
        string projectId,
        string componentTypeSelector,
        bool includeNone)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantReferenceOptions(projectId, componentTypeSelector, includeNone);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetComponentVariantReferenceOptionsByType(
        string projectId,
        string componentType,
        bool includeNone)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantReferenceOptionsByType(projectId, componentType, includeNone);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.RuntimeInputCollectionDefinition> GetComponentVariantRuntimeCollections(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantRuntimeCollections(variantReference);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantRuntimeContract(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).GetComponentVariantRuntimeContract(variantReference);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.ComponentInputBindingDefinition> GetComponentVariantRuntimeInputBindings(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantRuntimeInputBindings(variantReference);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantRuntimeInputs(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantRuntimeInputs(variantReference);

    public Mockups.DesktopEditorShell.Data.ComponentVariantSelectionSettings GetComponentVariantSelectionSettings(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantSelectionSettings(variantReference);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetDeviceOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDeviceOptions(projectId);

    public Mockups.DesktopEditorShell.Common.DevicePreviewMetrics GetDevicePreviewMetrics(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDevicePreviewMetrics(deviceId);

    public Mockups.DesktopEditorShell.Data.DeviceSettings GetDeviceSettings(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDeviceSettings(deviceId);

    public Mockups.DesktopEditorShell.Data.IconThemeSettings GetIconThemeSettings(
        string iconThemeId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetIconThemeSettings(iconThemeId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.IconThemeToken> GetIconThemeTokens(
        string iconThemeId)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetIconThemeTokens(iconThemeId);

    public Mockups.DesktopEditorShell.Data.AppSettings GetModuleAppSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleAppSettings(moduleId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceEffectiveContractJson(moduleInstanceId);

    public string GetModuleInstanceModuleName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceSettings(moduleInstanceId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceThemeTokensJson(moduleInstanceId);

    public string GetModuleInstanceTransitionType(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceTransitionType(moduleInstanceId);

    public string GetModuleInstanceVariantName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleInstanceVariantName(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceVariantSettings(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleSettings(moduleId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyDictionary<string, string> GetPaletteColorMap(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetPaletteColorMap(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetPaletteColorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetPaletteColorOptions(projectId);

    public System.Collections.Generic.IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetPaletteNeutralMap(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ProductionFontFace> GetProductionFontFaces(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetProductionFontFaces(projectId);

    public Mockups.DesktopEditorShell.Data.ProjectSettings GetProjectSettings(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IProjectSettingsQuery)_target).GetProjectSettings(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetRequiredActorOptions(projectId);

    public string GetRuntimeComponentVariantName(
        string variantReference,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetRuntimeComponentVariantName(variantReference, overrides, slots);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetShotModuleInstanceSlots(shotId);

    public Mockups.DesktopEditorShell.Data.ShotSettings GetShotSettings(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetShotSettings(shotId);

    public string GetThemeFieldValue(
        string themeId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeFieldValue(themeId, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetThemeOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeOptions(projectId);

    public Mockups.DesktopEditorShell.Data.ThemeSettings GetThemeSettings(
        string themeId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeSettings(themeId);

    public string ResolveIconThemeAssetPath(
        string iconThemeId,
        string file)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).ResolveIconThemeAssetPath(iconThemeId, file);

    public string ValidateComponentVariantReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).ValidateComponentVariantReferenceValue(projectId, componentType, reference, allowEmpty);

    public string ValidateComponentVariantReferencesForPreview(
        string projectId,
        string configJson)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).ValidateComponentVariantReferencesForPreview(projectId, configJson);

}

internal sealed class SqliteEditorNodeCommandPort(
    Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore target)
    : Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore
{
    private readonly Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore _target = target;

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode AddChild(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode parent)
        => ((Mockups.DesktopEditorShell.Data.IEditorChildStore)_target).AddChild(parent);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode AddImportedDevice(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode devicesRoot,
        Mockups.DesktopEditorShell.EditorShell.DeviceImportDraft device)
        => ((Mockups.DesktopEditorShell.Data.IEditorChildStore)_target).AddImportedDevice(devicesRoot, device);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode AddModuleInstance(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode shot,
        Mockups.DesktopEditorShell.Data.ShotModuleInstanceDraft draft)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).AddModuleInstance(shot, draft);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode AddShot(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode episode,
        string actorId,
        int shotNumber)
        => ((Mockups.DesktopEditorShell.Data.IEditorChildStore)_target).AddShot(episode, actorId, shotNumber);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode AddTheme(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode themesRoot,
        string family)
        => ((Mockups.DesktopEditorShell.Data.IEditorChildStore)_target).AddTheme(themesRoot, family);

    public void Delete(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).Delete(node);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode Duplicate(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).Duplicate(node);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode DuplicateShot(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode shot,
        int shotNumber)
        => ((Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore)_target).DuplicateShot(shot, shotNumber);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ShotModuleChoice> GetAvailableShotModules(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).GetAvailableShotModules(shotId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceEffectiveContractJson(moduleInstanceId);

    public string GetModuleInstanceModuleName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceSettings(moduleInstanceId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceThemeTokensJson(moduleInstanceId);

    public string GetModuleInstanceTransitionType(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceTransitionType(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceVariantSettings(moduleInstanceId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetModuleVariantOptions(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).GetModuleVariantOptions(moduleId);

    public Mockups.DesktopEditorShell.Data.ProjectSettings GetProjectSettings(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IEditorChildStore)_target).GetProjectSettings(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ReferenceUsageDetail> GetReferenceUsageDetails(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore)_target).GetReferenceUsageDetails(node);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IEditorChildStore)_target).GetRequiredActorOptions(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetShotModuleInstanceSlots(shotId);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ImportProductionFont(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode fontsRoot,
        System.Collections.Generic.IReadOnlyList<string> selectedFilePaths)
        => ((Mockups.DesktopEditorShell.Data.IEditorChildStore)_target).ImportProductionFont(fontsRoot, selectedFilePaths);

    public void MoveModuleInstance(
        string moduleInstanceId,
        int offset)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).MoveModuleInstance(moduleInstanceId, offset);

    public Mockups.DesktopEditorShell.Data.IconThemeRefreshResult RefreshIconThemeSets(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode iconThemesRoot)
        => ((Mockups.DesktopEditorShell.Data.IEditorChildStore)_target).RefreshIconThemeSets(iconThemesRoot);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode RenameDirectNode(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node,
        string name)
        => ((Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore)_target).RenameDirectNode(node, name);

    public void ReplaceComponentVariantConfig(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node,
        string configJson)
        => ((Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore)_target).ReplaceComponentVariantConfig(node, configJson);

    public void ReplaceModuleVariantConfig(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node,
        string configJson)
        => ((Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore)_target).ReplaceModuleVariantConfig(node, configJson);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode SaveComponentVariant(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode sourceNode,
        string name)
        => ((Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore)_target).SaveComponentVariant(sourceNode, name);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode SaveModuleVariant(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode sourceNode,
        string name)
        => ((Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore)_target).SaveModuleVariant(sourceNode, name);

    public int SuggestShotNumber(
        string episodeId)
        => ((Mockups.DesktopEditorShell.Data.IEditorChildStore)_target).SuggestShotNumber(episodeId);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ToggleComponentVariantLock(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore)_target).ToggleComponentVariantLock(node);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ToggleModuleVariantLock(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IEditorNodeCommandStore)_target).ToggleModuleVariantLock(node);

}

internal sealed class SqliteProductionNavigationPort(
    Mockups.DesktopEditorShell.Data.IProductionNavigationStore target)
    : Mockups.DesktopEditorShell.Data.IProductionNavigationStore
{
    private readonly Mockups.DesktopEditorShell.Data.IProductionNavigationStore _target = target;

    public Mockups.DesktopEditorShell.Data.ProductionOutputShotPlan GetProductionOutputShotPlan(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IRenderSnapshotDataSource)_target).GetProductionOutputShotPlan(shotId);

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

    public Mockups.DesktopEditorShell.Data.AppSettings GetAppSettings(
        string appId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetAppSettings(appId);

    public string GetComponentClassBaseConfigsJson(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentClassBaseConfigsJson(projectId);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentClassSettings(
        string componentClassId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentClassSettings(componentClassId);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantConfig(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).GetComponentVariantConfig(variantReference);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantRuntimeContract(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).GetComponentVariantRuntimeContract(variantReference);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetDeviceOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDeviceOptions(projectId);

    public Mockups.DesktopEditorShell.Common.DevicePreviewMetrics GetDevicePreviewMetrics(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDevicePreviewMetrics(deviceId);

    public Mockups.DesktopEditorShell.Data.DeviceSettings GetDeviceSettings(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDeviceSettings(deviceId);

    public Mockups.DesktopEditorShell.Data.IconThemeSettings GetIconThemeSettings(
        string iconThemeId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetIconThemeSettings(iconThemeId);

    public Mockups.DesktopEditorShell.Data.AppSettings GetModuleAppSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleAppSettings(moduleId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceEffectiveContractJson(moduleInstanceId);

    public string GetModuleInstanceModuleName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceSettings(moduleInstanceId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceThemeTokensJson(moduleInstanceId);

    public string GetModuleInstanceTransitionType(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceTransitionType(moduleInstanceId);

    public string GetModuleInstanceVariantName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleInstanceVariantName(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceVariantSettings(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleSettings(moduleId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyDictionary<string, string> GetPaletteColorMap(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetPaletteColorMap(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetPaletteColorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetPaletteColorOptions(projectId);

    public System.Collections.Generic.IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetPaletteNeutralMap(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ProductionFontFace> GetProductionFontFaces(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetProductionFontFaces(projectId);

    public Mockups.DesktopEditorShell.Data.ProjectSettings GetProjectSettings(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IProjectSettingsQuery)_target).GetProjectSettings(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetRequiredActorOptions(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetShotModuleInstanceSlots(shotId);

    public Mockups.DesktopEditorShell.Data.ShotSettings GetShotSettings(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetShotSettings(shotId);

    public string GetThemeFieldValue(
        string themeId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeFieldValue(themeId, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetThemeOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeOptions(projectId);

    public Mockups.DesktopEditorShell.Data.ThemeSettings GetThemeSettings(
        string themeId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeSettings(themeId);

    public string ValidateComponentVariantReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).ValidateComponentVariantReferenceValue(projectId, componentType, reference, allowEmpty);

    public string ValidateComponentVariantReferencesForPreview(
        string projectId,
        string configJson)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).ValidateComponentVariantReferencesForPreview(projectId, configJson);

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

internal sealed class SqliteEditorDomainDialogPort(
    Mockups.DesktopEditorShell.Data.SqliteProjectEngine target)
    : Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore,
      Mockups.DesktopEditorShell.Data.IIconThemeAssetStore,
      Mockups.DesktopEditorShell.Data.IThemeTokenQuery
{
    private readonly Mockups.DesktopEditorShell.Data.SqliteProjectEngine _target = target;

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode AddModuleInstance(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode shot,
        Mockups.DesktopEditorShell.Data.ShotModuleInstanceDraft draft)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).AddModuleInstance(shot, draft);

    public void Delete(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).Delete(node);

    public void DeleteIconThemeToken(
        string iconThemeId,
        string token)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).DeleteIconThemeToken(iconThemeId, token);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode Duplicate(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).Duplicate(node);

    public Mockups.DesktopEditorShell.Data.IconThemeGenerateResult GenerateIconThemeToken(
        string iconThemeId,
        string token,
        string category,
        string description,
        string lucideSource,
        string materialSource,
        System.Threading.CancellationToken cancellationToken)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).GenerateIconThemeToken(iconThemeId, token, category, description, lucideSource, materialSource, cancellationToken);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ShotModuleChoice> GetAvailableShotModules(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).GetAvailableShotModules(shotId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.IconThemeToken> GetIconThemeTokens(
        string iconThemeId)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).GetIconThemeTokens(iconThemeId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceEffectiveContractJson(moduleInstanceId);

    public string GetModuleInstanceModuleName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceSettings(moduleInstanceId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceThemeTokensJson(moduleInstanceId);

    public string GetModuleInstanceTransitionType(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceTransitionType(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceVariantSettings(moduleInstanceId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetModuleVariantOptions(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).GetModuleVariantOptions(moduleId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetShotModuleInstanceSlots(shotId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetThemeOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IThemeTokenQuery)_target).GetThemeOptions(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ThemeTokenOption> GetThemeTokenOptions(
        string projectId,
        string themeId)
        => ((Mockups.DesktopEditorShell.Data.IThemeTokenQuery)_target).GetThemeTokenOptions(projectId, themeId);

    public void MoveModuleInstance(
        string moduleInstanceId,
        int offset)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).MoveModuleInstance(moduleInstanceId, offset);

    public Mockups.DesktopEditorShell.Data.IconThemeTokenSvg ReadIconThemeTokenSvg(
        string iconThemeId,
        string token)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).ReadIconThemeTokenSvg(iconThemeId, token);

    public Mockups.DesktopEditorShell.Data.IconThemeRefreshResult RefreshIconThemeSetsForTheme(
        string iconThemeId)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).RefreshIconThemeSetsForTheme(iconThemeId);

    public Mockups.DesktopEditorShell.Data.IconThemeReplaceSvgResult ReplaceIconThemeTokenSvg(
        string iconThemeId,
        string token,
        string svgText)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).ReplaceIconThemeTokenSvg(iconThemeId, token, svgText);

    public string ResolveIconThemeAssetPath(
        string iconThemeId,
        string file)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).ResolveIconThemeAssetPath(iconThemeId, file);

    public Mockups.DesktopEditorShell.Data.IconThemeSearchResult SearchIconThemeSources(
        string query,
        System.Threading.CancellationToken cancellationToken)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).SearchIconThemeSources(query, cancellationToken);

    public Mockups.DesktopEditorShell.Data.IconThemeWriteAllSvgResult WriteIconThemeTokenSvgToAllSets(
        string iconThemeId,
        string token,
        string svgText,
        string description)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).WriteIconThemeTokenSvgToAllSets(iconThemeId, token, svgText, description);

}

internal sealed class SqliteComponentDocumentPort(
    Mockups.DesktopEditorShell.Data.IComponentDocumentStore target)
    : Mockups.DesktopEditorShell.Data.IComponentDocumentStore
{
    private readonly Mockups.DesktopEditorShell.Data.IComponentDocumentStore _target = target;

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateEmbeddedComponentFieldValue(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).CreateEmbeddedComponentFieldValue(ownerNode, slots, embeddedFieldId);

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).CreateRuntimeComponentOverrideFieldValue(projectId, baseConfigJson, overrides, slots, fieldId);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentClassSettings(
        string componentClassId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetComponentClassSettings(componentClassId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ComponentVariantReferenceUsage> GetComponentVariantReferenceUsageDetails(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetComponentVariantReferenceUsageDetails(node);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetComponentVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        string projectId,
        string componentType,
        string excludedComponentClassId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetEmbeddedComponentUsages(projectId, componentType, excludedComponentClassId);

    public string GetEmbeddedComponentVariantName(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetEmbeddedComponentVariantName(ownerNode, slots);

    public string GetRuntimeComponentVariantName(
        string variantReference,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetRuntimeComponentVariantName(variantReference, overrides, slots);

    public void UpdateEmbeddedComponentField(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).UpdateEmbeddedComponentField(ownerNode, slots, embeddedFieldId, value);

    public void UpdateRuntimeComponentOverride(
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).UpdateRuntimeComponentOverride(overrides, slots, fieldId, value);

}

internal sealed class SqliteEditorHeaderPort(
    Mockups.DesktopEditorShell.Data.SqliteProjectEngine target)
    : Mockups.DesktopEditorShell.Data.IComponentDocumentStore
{
    private readonly Mockups.DesktopEditorShell.Data.SqliteProjectEngine _target = target;

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateEmbeddedComponentFieldValue(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).CreateEmbeddedComponentFieldValue(ownerNode, slots, embeddedFieldId);

    public Mockups.DesktopEditorShell.EditorShell.FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).CreateRuntimeComponentOverrideFieldValue(projectId, baseConfigJson, overrides, slots, fieldId);

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

    public Mockups.DesktopEditorShell.Data.AppSettings GetAppSettings(
        string appId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetAppSettings(appId);

    public string GetComponentClassBaseConfigsJson(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentClassBaseConfigsJson(projectId);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentClassSettings(
        string componentClassId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetComponentClassSettings(componentClassId);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantConfig(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).GetComponentVariantConfig(variantReference);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ComponentVariantReferenceUsage> GetComponentVariantReferenceUsageDetails(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetComponentVariantReferenceUsageDetails(node);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantRuntimeContract(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).GetComponentVariantRuntimeContract(variantReference);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetComponentVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetDeviceOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDeviceOptions(projectId);

    public Mockups.DesktopEditorShell.Common.DevicePreviewMetrics GetDevicePreviewMetrics(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDevicePreviewMetrics(deviceId);

    public Mockups.DesktopEditorShell.Data.DeviceSettings GetDeviceSettings(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDeviceSettings(deviceId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        string projectId,
        string componentType,
        string excludedComponentClassId)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetEmbeddedComponentUsages(projectId, componentType, excludedComponentClassId);

    public string GetEmbeddedComponentVariantName(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetEmbeddedComponentVariantName(ownerNode, slots);

    public Mockups.DesktopEditorShell.Data.IconThemeSettings GetIconThemeSettings(
        string iconThemeId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetIconThemeSettings(iconThemeId);

    public Mockups.DesktopEditorShell.Data.AppSettings GetModuleAppSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleAppSettings(moduleId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceEffectiveContractJson(moduleInstanceId);

    public string GetModuleInstanceModuleName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceSettings(moduleInstanceId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceThemeTokensJson(moduleInstanceId);

    public string GetModuleInstanceTransitionType(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceTransitionType(moduleInstanceId);

    public string GetModuleInstanceVariantName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleInstanceVariantName(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceVariantSettings(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleSettings(moduleId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyDictionary<string, string> GetPaletteColorMap(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetPaletteColorMap(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetPaletteColorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetPaletteColorOptions(projectId);

    public System.Collections.Generic.IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetPaletteNeutralMap(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ProductionFontFace> GetProductionFontFaces(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetProductionFontFaces(projectId);

    public Mockups.DesktopEditorShell.Data.ProjectSettings GetProjectSettings(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IProjectSettingsQuery)_target).GetProjectSettings(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetRequiredActorOptions(projectId);

    public string GetRuntimeComponentVariantName(
        string variantReference,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).GetRuntimeComponentVariantName(variantReference, overrides, slots);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetShotModuleInstanceSlots(shotId);

    public Mockups.DesktopEditorShell.Data.ShotSettings GetShotSettings(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetShotSettings(shotId);

    public string GetThemeFieldValue(
        string themeId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeFieldValue(themeId, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetThemeOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeOptions(projectId);

    public Mockups.DesktopEditorShell.Data.ThemeSettings GetThemeSettings(
        string themeId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeSettings(themeId);

    public void UpdateEmbeddedComponentField(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode ownerNode,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).UpdateEmbeddedComponentField(ownerNode, slots, embeddedFieldId, value);

    public void UpdateRuntimeComponentOverride(
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value)
        => ((Mockups.DesktopEditorShell.Data.IComponentDocumentStore)_target).UpdateRuntimeComponentOverride(overrides, slots, fieldId, value);

    public string ValidateComponentVariantReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).ValidateComponentVariantReferenceValue(projectId, componentType, reference, allowEmpty);

    public string ValidateComponentVariantReferencesForPreview(
        string projectId,
        string configJson)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).ValidateComponentVariantReferencesForPreview(projectId, configJson);

}

internal sealed class SqliteEditorCollectionPort(
    Mockups.DesktopEditorShell.Data.SqliteProjectEngine target)
    : Mockups.DesktopEditorShell.Data.IRuntimeInputOwnerStore
{
    private readonly Mockups.DesktopEditorShell.Data.SqliteProjectEngine _target = target;

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode AddModuleInstance(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode shot,
        Mockups.DesktopEditorShell.Data.ShotModuleInstanceDraft draft)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).AddModuleInstance(shot, draft);

    public void AddModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        System.Text.Json.Nodes.JsonObject item)
        => ((Mockups.DesktopEditorShell.Data.IRuntimeInputInstanceStore)_target).AddModuleInstanceRuntimeCollectionItem(moduleInstanceId, collectionJsonKey, item);

    public void Delete(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).Delete(node);

    public void DeleteIconThemeToken(
        string iconThemeId,
        string token)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).DeleteIconThemeToken(iconThemeId, token);

    public void DeleteModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId)
        => ((Mockups.DesktopEditorShell.Data.IRuntimeInputInstanceStore)_target).DeleteModuleInstanceRuntimeCollectionItem(moduleInstanceId, collectionJsonKey, itemId);

    public Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode Duplicate(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).Duplicate(node);

    public void DuplicateModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        System.Text.Json.Nodes.JsonObject duplicate,
        System.Collections.Generic.IReadOnlyDictionary<string, string> targetIdMappings)
        => ((Mockups.DesktopEditorShell.Data.IRuntimeInputInstanceStore)_target).DuplicateModuleInstanceRuntimeCollectionItem(moduleInstanceId, collectionJsonKey, itemId, duplicate, targetIdMappings);

    public Mockups.DesktopEditorShell.Data.IconThemeGenerateResult GenerateIconThemeToken(
        string iconThemeId,
        string token,
        string category,
        string description,
        string lucideSource,
        string materialSource,
        System.Threading.CancellationToken cancellationToken)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).GenerateIconThemeToken(iconThemeId, token, category, description, lucideSource, materialSource, cancellationToken);

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

    public Mockups.DesktopEditorShell.Data.AppSettings GetAppSettings(
        string appId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetAppSettings(appId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ShotModuleChoice> GetAvailableShotModules(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).GetAvailableShotModules(shotId);

    public string GetComponentClassBaseConfigsJson(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentClassBaseConfigsJson(projectId);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentClassSettings(
        string componentClassId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentClassSettings(componentClassId);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantConfig(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).GetComponentVariantConfig(variantReference);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetComponentVariantReferenceOptions(
        string projectId,
        string componentTypeSelector,
        bool includeNone)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantReferenceOptions(projectId, componentTypeSelector, includeNone);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetComponentVariantReferenceOptionsByType(
        string projectId,
        string componentType,
        bool includeNone)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantReferenceOptionsByType(projectId, componentType, includeNone);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.RuntimeInputCollectionDefinition> GetComponentVariantRuntimeCollections(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantRuntimeCollections(variantReference);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantRuntimeContract(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).GetComponentVariantRuntimeContract(variantReference);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.ComponentInputBindingDefinition> GetComponentVariantRuntimeInputBindings(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantRuntimeInputBindings(variantReference);

    public System.Text.Json.Nodes.JsonObject GetComponentVariantRuntimeInputs(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantRuntimeInputs(variantReference);

    public Mockups.DesktopEditorShell.Data.ComponentVariantSelectionSettings GetComponentVariantSelectionSettings(
        string variantReference)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetComponentVariantSelectionSettings(variantReference);

    public Mockups.DesktopEditorShell.Data.ComponentClassSettings GetComponentVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetComponentVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetDeviceOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDeviceOptions(projectId);

    public Mockups.DesktopEditorShell.Common.DevicePreviewMetrics GetDevicePreviewMetrics(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDevicePreviewMetrics(deviceId);

    public Mockups.DesktopEditorShell.Data.DeviceSettings GetDeviceSettings(
        string deviceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetDeviceSettings(deviceId);

    public Mockups.DesktopEditorShell.Data.IconThemeSettings GetIconThemeSettings(
        string iconThemeId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetIconThemeSettings(iconThemeId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.IconThemeToken> GetIconThemeTokens(
        string iconThemeId)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).GetIconThemeTokens(iconThemeId);

    public Mockups.DesktopEditorShell.Data.AppSettings GetModuleAppSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleAppSettings(moduleId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceEffectiveContractJson(moduleInstanceId);

    public string GetModuleInstanceModuleName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceSettings(moduleInstanceId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceThemeTokensJson(moduleInstanceId);

    public string GetModuleInstanceTransitionType(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceTransitionType(moduleInstanceId);

    public string GetModuleInstanceVariantName(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleInstanceVariantName(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetModuleInstanceVariantSettings(moduleInstanceId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleSettings(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleSettings(moduleId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetModuleVariantOptions(
        string moduleId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).GetModuleVariantOptions(moduleId);

    public Mockups.DesktopEditorShell.Data.ModuleSettings GetModuleVariantSettings(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode variantNode)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetModuleVariantSettings(variantNode);

    public System.Collections.Generic.IReadOnlyDictionary<string, string> GetPaletteColorMap(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetPaletteColorMap(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetPaletteColorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetPaletteColorOptions(projectId);

    public System.Collections.Generic.IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetPaletteNeutralMap(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ProductionFontFace> GetProductionFontFaces(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetProductionFontFaces(projectId);

    public Mockups.DesktopEditorShell.Data.ProjectSettings GetProjectSettings(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IProjectSettingsQuery)_target).GetProjectSettings(projectId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ReferenceUsageDetail> GetReferenceUsageDetails(
        Mockups.DesktopEditorShell.EditorShell.ProjectTreeNode node)
        => ((Mockups.DesktopEditorShell.Data.IReferenceUsageQuery)_target).GetReferenceUsageDetails(node);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IActorPreviewRepository)_target).GetRequiredActorOptions(projectId);

    public string GetRuntimeComponentVariantName(
        string variantReference,
        System.Text.Json.Nodes.JsonObject overrides,
        System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.EmbeddedComponentSlotDefinition> slots)
        => ((Mockups.DesktopEditorShell.Data.IDictionaryFieldContextRepository)_target).GetRuntimeComponentVariantName(variantReference, overrides, slots);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceTimelineStore)_target).GetShotModuleInstanceSlots(shotId);

    public Mockups.DesktopEditorShell.Data.ShotSettings GetShotSettings(
        string shotId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetShotSettings(shotId);

    public string GetThemeFieldValue(
        string themeId,
        string fieldId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeFieldValue(themeId, fieldId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.EditorShell.FieldOption> GetThemeOptions(
        string projectId)
        => ((Mockups.DesktopEditorShell.Data.IThemeTokenQuery)_target).GetThemeOptions(projectId);

    public Mockups.DesktopEditorShell.Data.ThemeSettings GetThemeSettings(
        string themeId)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).GetThemeSettings(themeId);

    public System.Collections.Generic.IReadOnlyList<Mockups.DesktopEditorShell.Data.ThemeTokenOption> GetThemeTokenOptions(
        string projectId,
        string themeId)
        => ((Mockups.DesktopEditorShell.Data.IThemeTokenQuery)_target).GetThemeTokenOptions(projectId, themeId);

    public void InsertModuleInstanceRuntimeCollectionItemAfter(
        string moduleInstanceId,
        string collectionJsonKey,
        string afterItemId,
        System.Text.Json.Nodes.JsonObject item)
        => ((Mockups.DesktopEditorShell.Data.IRuntimeInputInstanceStore)_target).InsertModuleInstanceRuntimeCollectionItemAfter(moduleInstanceId, collectionJsonKey, afterItemId, item);

    public void MoveModuleInstance(
        string moduleInstanceId,
        int offset)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceCollectionStore)_target).MoveModuleInstance(moduleInstanceId, offset);

    public void MoveModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        int offset)
        => ((Mockups.DesktopEditorShell.Data.IRuntimeInputInstanceStore)_target).MoveModuleInstanceRuntimeCollectionItem(moduleInstanceId, collectionJsonKey, itemId, offset);

    public Mockups.DesktopEditorShell.Data.IconThemeTokenSvg ReadIconThemeTokenSvg(
        string iconThemeId,
        string token)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).ReadIconThemeTokenSvg(iconThemeId, token);

    public Mockups.DesktopEditorShell.Data.IconThemeRefreshResult RefreshIconThemeSetsForTheme(
        string iconThemeId)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).RefreshIconThemeSetsForTheme(iconThemeId);

    public Mockups.DesktopEditorShell.Data.IconThemeReplaceSvgResult ReplaceIconThemeTokenSvg(
        string iconThemeId,
        string token,
        string svgText)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).ReplaceIconThemeTokenSvg(iconThemeId, token, svgText);

    public string ResolveIconThemeAssetPath(
        string iconThemeId,
        string file)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).ResolveIconThemeAssetPath(iconThemeId, file);

    public Mockups.DesktopEditorShell.Data.IconThemeSearchResult SearchIconThemeSources(
        string query,
        System.Threading.CancellationToken cancellationToken)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).SearchIconThemeSources(query, cancellationToken);

    public void UpdateComponentClassDesignPreviewJson(
        string componentClassId,
        string designPreviewJson)
        => ((Mockups.DesktopEditorShell.Data.IRuntimeInputOwnerStore)_target).UpdateComponentClassDesignPreviewJson(componentClassId, designPreviewJson);

    public void UpdateModuleDesignPreviewJson(
        string moduleId,
        string designPreviewJson)
        => ((Mockups.DesktopEditorShell.Data.IRuntimeInputOwnerStore)_target).UpdateModuleDesignPreviewJson(moduleId, designPreviewJson);

    public void UpdateModuleInstanceAnimationJson(
        string moduleInstanceId,
        string animationJson)
        => ((Mockups.DesktopEditorShell.Data.IModuleInstanceAnimationStore)_target).UpdateModuleInstanceAnimationJson(moduleInstanceId, animationJson);

    public void UpdateModuleInstanceRuntimeCollectionValue(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        string fieldJsonKey,
        System.Text.Json.Nodes.JsonNode value)
        => ((Mockups.DesktopEditorShell.Data.IRuntimeInputInstanceStore)_target).UpdateModuleInstanceRuntimeCollectionValue(moduleInstanceId, collectionJsonKey, itemId, fieldJsonKey, value);

    public void UpdateModuleInstanceRuntimeCollectionValues(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        System.Collections.Generic.IReadOnlyDictionary<string, System.Text.Json.Nodes.JsonNode> values)
        => ((Mockups.DesktopEditorShell.Data.IRuntimeInputInstanceStore)_target).UpdateModuleInstanceRuntimeCollectionValues(moduleInstanceId, collectionJsonKey, itemId, values);

    public void UpdateModuleInstanceRuntimeValue(
        string moduleInstanceId,
        string jsonKey,
        System.Text.Json.Nodes.JsonNode value)
        => ((Mockups.DesktopEditorShell.Data.IRuntimeInputInstanceStore)_target).UpdateModuleInstanceRuntimeValue(moduleInstanceId, jsonKey, value);

    public string ValidateComponentVariantReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty)
        => ((Mockups.DesktopEditorShell.Data.IComponentPreviewInputRepository)_target).ValidateComponentVariantReferenceValue(projectId, componentType, reference, allowEmpty);

    public string ValidateComponentVariantReferencesForPreview(
        string projectId,
        string configJson)
        => ((Mockups.DesktopEditorShell.Data.IPreviewInputRepository)_target).ValidateComponentVariantReferencesForPreview(projectId, configJson);

    public Mockups.DesktopEditorShell.Data.IconThemeWriteAllSvgResult WriteIconThemeTokenSvgToAllSets(
        string iconThemeId,
        string token,
        string svgText,
        string description)
        => ((Mockups.DesktopEditorShell.Data.IIconThemeAssetStore)_target).WriteIconThemeTokenSvgToAllSets(iconThemeId, token, svgText, description);

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
