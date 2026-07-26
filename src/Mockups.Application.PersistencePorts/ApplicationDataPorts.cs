using System.Collections.Generic;

using System.Text.Json.Nodes;
using System.Threading;
using Common = Mockups.DesktopEditorShell.Common;
using EditorShell = Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

public interface IProjectSettingsQuery
{
    ProjectSettings GetProjectSettings(string projectId);
}

public interface IEditorLayoutStore
{
    EditorShell.EditorLayout LoadEditorLayout(string recordClassId);

    void SaveEditorLayout(
        string recordClassId,
        EditorShell.EditorLayout layout);
}

public interface ICoreFieldStore
{
    EditorShell.ProjectTreeNode RenameDirectNode(
        EditorShell.ProjectTreeNode node,
        string name);

    void UpdateNode(EditorShell.ProjectTreeNode node);
}

public interface IRecordClassFieldStore : IModuleInstanceTimelineStore
{
    ProjectSettings GetProjectSettings(string projectId);
    void UpdateProjectField(string projectId, string fieldId, string value);
    EpisodeSettings GetEpisodeSettings(string episodeId);
    void UpdateEpisodeField(string episodeId, string fieldId, string value);
    ShotSettings GetShotSettings(string shotId);
    void UpdateShotField(string shotId, string fieldId, string value);
    string GetShotRenderName(string shotId);
    string GetShotOwnerDeviceName(string shotId);
    AppSettings GetAppSettings(string appId);
    void UpdateAppField(string appId, string fieldId, string value);
    string GetAppConfigFieldValue(string appId, string fieldId);
    string GetAppMetadataFieldValue(string appId, string fieldId);
    ModuleSettings GetModuleSettings(string moduleId);
    string GetModuleConfigFieldValue(string moduleId, string fieldId);
    ModuleSettings GetModuleVariantSettings(
        EditorShell.ProjectTreeNode variantNode);
    string GetModuleVariantConfigFieldValue(
        EditorShell.ProjectTreeNode node,
        string fieldId);
    IReadOnlyList<EditorShell.FieldOption> GetModuleVariantOptions(
        string moduleId);
    void UpdateModuleField(string moduleId, string fieldId, string value);
    void UpdateModuleVariantField(
        EditorShell.ProjectTreeNode node,
        string fieldId,
        string value);
    string GetModuleInstanceVariantReference(string moduleInstanceId);
    void UpdateModuleInstanceField(
        string moduleInstanceId,
        string fieldId,
        string value);
    PaletteColorSettings GetPaletteColorSettings(string colorId);
    IReadOnlyList<EditorShell.FieldOption> GetPaletteColorOptions(
        string projectId);
    void UpdatePaletteColorField(
        string colorId,
        string fieldId,
        string value);
    DeviceSettings GetDeviceSettings(string deviceId);
    string GetDeviceMetricFieldValue(string deviceId, string fieldId);
    IReadOnlyList<EditorShell.FieldOption> GetDeviceOptions(string projectId);
    void UpdateDeviceField(string deviceId, string fieldId, string value);
    ActorSettings GetActorSettings(string actorId);
    string GetActorFieldValue(string actorId, string fieldId);
    IReadOnlyList<EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId);
    void UpdateActorField(string actorId, string fieldId, string value);
    ThemeSettings GetThemeSettings(string themeId);
    string GetThemeFieldValue(string themeId, string fieldId);
    IReadOnlyList<EditorShell.FieldOption> GetThemeOptions(string projectId);
    void UpdateThemeField(string themeId, string fieldId, string value);
    IReadOnlyList<EditorShell.FieldOption> GetIconThemeOptions(
        string projectId);
    string GetIconThemeFieldValue(string iconThemeId, string fieldId);
    IReadOnlyList<EditorShell.FieldOption> GetProductionFontOptions(
        string projectId,
        string? category = null);
    string GetProductionFontFieldValue(string fontId, string fieldId);
    void UpdateProductionFontField(
        string fontId,
        string fieldId,
        string value);
    IReadOnlyList<EditorShell.FieldOption>
        GetComponentVariantReferenceOptionsByType(
            string projectId,
            string componentType,
            bool includeNone = false);
    IReadOnlyList<EditorShell.FieldOption>
        GetStatusBarComponentVariantOptions(string projectId);
    IReadOnlyList<EditorShell.FieldOption>
        GetNavigationBarComponentVariantOptions(string projectId);
    EditorShell.ProjectTreeNode RenameDirectNode(
        EditorShell.ProjectTreeNode node,
        string name);
}

public interface IComponentClassFieldStore : IComponentDocumentStore
{
    EditorShell.FieldValue CreateComponentClassFieldValue(
        string componentClassId,
        string fieldId);
    EditorShell.FieldValue CreateComponentVariantFieldValue(
        EditorShell.ProjectTreeNode variantNode,
        string fieldId);
    void UpdateComponentClassField(
        string componentClassId,
        string fieldId,
        string value);
    void UpdateComponentVariantField(
        EditorShell.ProjectTreeNode variantNode,
        string fieldId,
        string value);
}

public interface IVariantHistoryStore
{
    ComponentClassSettings GetComponentVariantSettings(
        EditorShell.ProjectTreeNode variantNode);
    ModuleSettings GetModuleVariantSettings(
        EditorShell.ProjectTreeNode variantNode);
}

public interface IComponentPreviewInputRepository
{
    JsonObject GetComponentVariantConfig(string variantReference);
    JsonObject GetComponentVariantRuntimeContract(string variantReference);
    string ValidateComponentVariantReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty = false);
}

public interface IPreviewInputRepository
{
    ShotSettings GetShotSettings(string shotId);
    AppSettings GetAppSettings(string appId);
    AppSettings GetModuleAppSettings(string moduleId);
    ModuleSettings GetModuleSettings(string moduleId);
    ModuleSettings GetModuleVariantSettings(
        EditorShell.ProjectTreeNode variantNode);
    string GetModuleInstanceVariantName(string moduleInstanceId);
    ComponentClassSettings GetComponentClassSettings(string componentClassId);
    ComponentClassSettings GetComponentVariantSettings(
        EditorShell.ProjectTreeNode variantNode);
    string GetComponentClassBaseConfigsJson(string projectId);
    string ValidateComponentVariantReferencesForPreview(
        string projectId,
        string configJson);
    DeviceSettings GetDeviceSettings(string deviceId);
    Common.DevicePreviewMetrics GetDevicePreviewMetrics(string deviceId);
    IReadOnlyList<EditorShell.FieldOption> GetDeviceOptions(string projectId);
    ThemeSettings GetThemeSettings(string themeId);
    string GetThemeFieldValue(string themeId, string fieldId);
    IReadOnlyList<EditorShell.FieldOption> GetThemeOptions(string projectId);
    IReadOnlyDictionary<string, string> GetPaletteColorMap(string projectId);
    IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(string projectId);
    IReadOnlyList<ProductionFontFace> GetProductionFontFaces(
        string projectId);
    IconThemeSettings GetIconThemeSettings(string iconThemeId);
}

public interface IActorPreviewRepository : IProjectSettingsQuery
{
    ActorSettings GetActorSettings(string actorId);
    string GetActorFieldValue(string actorId, string fieldId);
    IReadOnlyList<EditorShell.FieldOption> GetActorOptions(string projectId);
    IReadOnlyList<EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId);
    IReadOnlyList<EditorShell.FieldOption> GetPaletteColorOptions(
        string projectId);
}

public interface IEditorPresentationContextRepository
{
    ProjectSettings GetProjectSettings(string projectId);
    ThemeSettings GetThemeSettings(string themeId);
    string GetProductionFontFieldValue(string fontId, string fieldId);
}

public interface IDictionaryFieldContextRepository
{
    ThemeSettings GetThemeSettings(string themeId);
    string GetModuleInstanceThemeTokensJson(string moduleInstanceId);
    IReadOnlyList<EditorShell.FieldOption> GetPaletteColorOptions(
        string projectId);
    ComponentVariantSelectionSettings GetComponentVariantSelectionSettings(
        string variantReference);
    JsonObject GetComponentVariantRuntimeInputs(string variantReference);
    IReadOnlyList<EditorShell.ComponentInputBindingDefinition>
        GetComponentVariantRuntimeInputBindings(string variantReference);
    IReadOnlyList<EditorShell.RuntimeInputCollectionDefinition>
        GetComponentVariantRuntimeCollections(string variantReference);
    IReadOnlyList<EditorShell.FieldOption>
        GetComponentVariantReferenceOptionsByType(
            string projectId,
            string componentType,
            bool includeNone = false);
    IReadOnlyList<EditorShell.FieldOption> GetComponentVariantReferenceOptions(
        string projectId,
        string componentTypeSelector,
        bool includeNone = false);
    string GetRuntimeComponentVariantName(
        string variantReference,
        JsonObject overrides,
        IReadOnlyList<EditorShell.EmbeddedComponentSlotDefinition> slots);
    IReadOnlyList<IconThemeToken> GetIconThemeTokens(string iconThemeId);
    string ResolveIconThemeAssetPath(string iconThemeId, string file);
}

public interface IEditorChildStore
{
    EditorShell.ProjectTreeNode AddChild(EditorShell.ProjectTreeNode parent);
    EditorShell.ProjectTreeNode AddImportedDevice(
        EditorShell.ProjectTreeNode devicesRoot,
        EditorShell.DeviceImportDraft device);
    EditorShell.ProjectTreeNode AddShot(
        EditorShell.ProjectTreeNode episode,
        string actorId,
        int shotNumber);
    EditorShell.ProjectTreeNode AddTheme(
        EditorShell.ProjectTreeNode themesRoot,
        string family);
    int SuggestShotNumber(string episodeId);
    ProjectSettings GetProjectSettings(string projectId);
    IReadOnlyList<EditorShell.FieldOption> GetRequiredActorOptions(
        string projectId);
    EditorShell.ProjectTreeNode ImportProductionFont(
        EditorShell.ProjectTreeNode fontsRoot,
        IReadOnlyList<string> selectedFilePaths);
    IconThemeRefreshResult RefreshIconThemeSets(
        EditorShell.ProjectTreeNode iconThemesRoot);
}

public interface IThemeTokenQuery
{
    IReadOnlyList<EditorShell.FieldOption> GetThemeOptions(string projectId);
    IReadOnlyList<ThemeTokenOption> GetThemeTokenOptions(
        string projectId,
        string themeId);
}

public interface IEditorNodeCommandStore
{
    void Delete(EditorShell.ProjectTreeNode node);
    EditorShell.ProjectTreeNode Duplicate(
        EditorShell.ProjectTreeNode node);
    EditorShell.ProjectTreeNode DuplicateShot(
        EditorShell.ProjectTreeNode shot,
        int shotNumber);
    EditorShell.ProjectTreeNode RenameDirectNode(
        EditorShell.ProjectTreeNode node,
        string name);
    void ReplaceComponentVariantConfig(
        EditorShell.ProjectTreeNode node,
        string configJson);
    void ReplaceModuleVariantConfig(
        EditorShell.ProjectTreeNode node,
        string configJson);
    EditorShell.ProjectTreeNode SaveComponentVariant(
        EditorShell.ProjectTreeNode sourceNode,
        string name);
    EditorShell.ProjectTreeNode SaveModuleVariant(
        EditorShell.ProjectTreeNode sourceNode,
        string name);
    EditorShell.ProjectTreeNode ToggleComponentVariantLock(
        EditorShell.ProjectTreeNode node);
    EditorShell.ProjectTreeNode ToggleModuleVariantLock(
        EditorShell.ProjectTreeNode node);
}

public interface IComponentDocumentStore
{
    ComponentClassSettings GetComponentClassSettings(string componentClassId);
    ComponentClassSettings GetComponentVariantSettings(
        EditorShell.ProjectTreeNode variantNode);
    IReadOnlyList<ComponentVariantReferenceUsage>
        GetComponentVariantReferenceUsageDetails(
            EditorShell.ProjectTreeNode node);
    IReadOnlyList<EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        string projectId,
        string componentType,
        string? excludedComponentClassId = null);
    string GetEmbeddedComponentVariantName(
        EditorShell.ProjectTreeNode ownerNode,
        IReadOnlyList<EditorShell.EmbeddedComponentSlotDefinition> slots);
    string GetRuntimeComponentVariantName(
        string variantReference,
        JsonObject overrides,
        IReadOnlyList<EditorShell.EmbeddedComponentSlotDefinition> slots);
    EditorShell.FieldValue CreateEmbeddedComponentFieldValue(
        EditorShell.ProjectTreeNode ownerNode,
        IReadOnlyList<EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId);
    EditorShell.FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        IReadOnlyList<EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId);
    void UpdateEmbeddedComponentField(
        EditorShell.ProjectTreeNode ownerNode,
        IReadOnlyList<EditorShell.EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value);
    void UpdateRuntimeComponentOverride(
        JsonObject overrides,
        IReadOnlyList<EditorShell.EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value);
}

public interface IRuntimeInputOwnerStore
{
    ComponentVariantSelectionSettings GetComponentVariantSelectionSettings(
        string variantReference);
    ComponentClassSettings GetComponentVariantSettings(
        EditorShell.ProjectTreeNode variantNode);
    JsonObject GetComponentVariantRuntimeInputs(string variantReference);
    ModuleSettings GetModuleSettings(string moduleId);
    ModuleSettings GetModuleVariantSettings(
        EditorShell.ProjectTreeNode variantNode);
    void UpdateComponentClassDesignPreviewJson(
        string componentClassId,
        string designPreviewJson);
    void UpdateModuleDesignPreviewJson(
        string moduleId,
        string designPreviewJson);
}

public interface IRuntimeInputInstanceStore
{
    void UpdateModuleInstanceRuntimeValue(
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value);
    void UpdateModuleInstanceRuntimeCollectionValue(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        string fieldJsonKey,
        JsonNode? value);
    void UpdateModuleInstanceRuntimeCollectionValues(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values);
    void AddModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        JsonObject item);
    void InsertModuleInstanceRuntimeCollectionItemAfter(
        string moduleInstanceId,
        string collectionJsonKey,
        string afterItemId,
        JsonObject item);
    void DuplicateModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        JsonObject duplicate,
        IReadOnlyDictionary<string, string> targetIdMappings);
    void DeleteModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId);
    void MoveModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        int offset);
}

public interface IModuleInstanceTimelineStore
{
    ModuleInstanceSettings GetModuleInstanceSettings(string moduleInstanceId);
    ModuleSettings GetModuleInstanceVariantSettings(string moduleInstanceId);
    string GetModuleInstanceModuleName(string moduleInstanceId);
    string GetModuleInstanceTransitionType(string moduleInstanceId);
    string GetModuleInstanceEffectiveContractJson(string moduleInstanceId);
    string GetModuleInstanceRuntimePreviewJson(string moduleInstanceId);
    IReadOnlyList<ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId);
}

public interface IModuleInstanceThemeTokenQuery
{
    string GetModuleInstanceThemeTokensJson(string moduleInstanceId);
}

public interface IModuleInstanceAnimationStore : IModuleInstanceTimelineStore
{
    void UpdateModuleInstanceAnimationJson(
        string moduleInstanceId,
        string animationJson);
}

public interface IModuleInstanceCollectionStore : IModuleInstanceTimelineStore
{
    EditorShell.ProjectTreeNode AddModuleInstance(
        EditorShell.ProjectTreeNode shot,
        ShotModuleInstanceDraft draft);
    void Delete(EditorShell.ProjectTreeNode node);
    EditorShell.ProjectTreeNode Duplicate(EditorShell.ProjectTreeNode node);
    void MoveModuleInstance(string moduleInstanceId, int offset);
    IReadOnlyList<ShotModuleChoice> GetAvailableShotModules(string shotId);
    IReadOnlyList<EditorShell.FieldOption> GetModuleVariantOptions(
        string moduleId);
}

public interface IIconThemeAssetStore
{
    IReadOnlyList<IconThemeToken> GetIconThemeTokens(string iconThemeId);
    IconThemeRefreshResult RefreshIconThemeSetsForTheme(string iconThemeId);
    void DeleteIconThemeToken(string iconThemeId, string token);
    IconThemeTokenSvg ReadIconThemeTokenSvg(
        string iconThemeId,
        string token);
    IconThemeReplaceSvgResult ReplaceIconThemeTokenSvg(
        string iconThemeId,
        string token,
        string svgText);
    IconThemeWriteAllSvgResult WriteIconThemeTokenSvgToAllSets(
        string iconThemeId,
        string token,
        string svgText,
        string description);
    IconThemeSearchResult SearchIconThemeSources(
        string query,
        CancellationToken cancellationToken = default);
    IconThemeGenerateResult GenerateIconThemeToken(
        string iconThemeId,
        string token,
        string category,
        string description,
        string lucideSource,
        string materialSource,
        CancellationToken cancellationToken = default);
    string ResolveIconThemeAssetPath(string iconThemeId, string file);
}

public interface IReferenceUsageQuery
{
    IReadOnlyList<ReferenceUsageDetail> GetReferenceUsageDetails(
        EditorShell.ProjectTreeNode node);
}

public interface IRenderSnapshotDataSource :
    IPreviewInputRepository,
    IActorPreviewRepository,
    IComponentPreviewInputRepository,
    IModuleInstanceTimelineStore,
    IModuleInstanceThemeTokenQuery
{
    ProductionOutputShotPlan GetProductionOutputShotPlan(string shotId);
}

public interface IProductionNavigationStore : IRenderSnapshotDataSource
{
}
