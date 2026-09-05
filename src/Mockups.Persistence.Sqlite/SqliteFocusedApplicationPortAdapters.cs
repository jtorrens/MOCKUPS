using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteComponentPreviewInputPort(
    IComponentPreviewInputRepository target)
    : IComponentPreviewInputRepository
{
    public JsonObject GetComponentVariantConfig(string variantReference) =>
        target.GetComponentVariantConfig(variantReference);

    public JsonObject GetComponentVariantRuntimeContract(
        string variantReference) =>
        target.GetComponentVariantRuntimeContract(variantReference);

    public string ValidateComponentVariantReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty = false) =>
        target.ValidateComponentVariantReferenceValue(
            projectId,
            componentType,
            reference,
            allowEmpty);
}

internal sealed class SqliteModuleInstanceTimelinePort(
    IModuleInstanceTimelineStore target)
    : IModuleInstanceTimelineStore
{
    public ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId) =>
        target.GetModuleInstanceSettings(moduleInstanceId);

    public ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId) =>
        target.GetModuleInstanceVariantSettings(moduleInstanceId);

    public string GetModuleInstanceModuleName(string moduleInstanceId) =>
        target.GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceTransitionType(string moduleInstanceId) =>
        target.GetModuleInstanceTransitionType(moduleInstanceId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId) =>
        target.GetModuleInstanceEffectiveContractJson(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId) =>
        target.GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public IReadOnlyList<ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId) =>
        target.GetShotModuleInstanceSlots(shotId);
}

internal sealed class SqliteModuleInstanceThemeTokenPort(
    IModuleInstanceThemeTokenQuery target)
    : IModuleInstanceThemeTokenQuery
{
    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId) =>
        target.GetModuleInstanceThemeTokensJson(moduleInstanceId);
}

internal sealed class SqliteEditorChildPort(SqliteEditorChildStore target)
    : IEditorChildStore
{
    public ProjectTreeNode AddChild(ProjectTreeNode parent) =>
        target.AddChild(parent);

    public ProjectTreeNode AddImportedDevice(
        ProjectTreeNode devicesRoot,
        DeviceImportDraft device) =>
        target.AddImportedDevice(devicesRoot, device);

    public ProjectTreeNode AddShot(
        ProjectTreeNode episode,
        string actorId,
        int shotNumber) =>
        target.AddShot(episode, actorId, shotNumber);

    public ProjectTreeNode AddTheme(
        ProjectTreeNode themesRoot,
        string family) =>
        target.AddTheme(themesRoot, family);

    public int SuggestShotNumber(string episodeId) =>
        target.SuggestShotNumber(episodeId);

    public ProjectSettings GetProjectSettings(string projectId) =>
        target.GetProjectSettings(projectId);

    public IReadOnlyList<FieldOption> GetRequiredActorOptions(
        string projectId) =>
        target.GetRequiredActorOptions(projectId);

    public ProjectTreeNode ImportProductionFont(
        ProjectTreeNode fontsRoot,
        IReadOnlyList<string> selectedFilePaths) =>
        target.ImportProductionFont(fontsRoot, selectedFilePaths);

    public IconThemeRefreshResult RefreshIconThemeSets(
        ProjectTreeNode iconThemesRoot) =>
        target.RefreshIconThemeSets(iconThemesRoot);
}

internal sealed class SqliteModuleInstanceCollectionPort(
    SqliteModuleInstanceCollectionStore target)
    : IModuleInstanceCollectionStore
{
    public ProjectTreeNode AddModuleInstance(
        ProjectTreeNode shot,
        ShotModuleInstanceDraft draft) =>
        target.AddModuleInstance(shot, draft);

    public void Delete(ProjectTreeNode node) =>
        target.Delete(node);

    public ProjectTreeNode Duplicate(ProjectTreeNode node) =>
        target.Duplicate(node);

    public void MoveModuleInstance(string moduleInstanceId, int offset) =>
        target.MoveModuleInstance(moduleInstanceId, offset);

    public IReadOnlyList<ShotModuleChoice> GetAvailableShotModules(
        string shotId) =>
        target.GetAvailableShotModules(shotId);

    public IReadOnlyList<FieldOption> GetModuleVariantOptions(
        string moduleId) =>
        target.GetModuleVariantOptions(moduleId);
}

internal sealed class SqliteIconThemeAssetPort(IIconThemeAssetStore target)
    : IIconThemeAssetStore
{
    public IReadOnlyList<IconThemeToken> GetIconThemeTokens(
        string iconThemeId) =>
        target.GetIconThemeTokens(iconThemeId);

    public IconThemeRefreshResult RefreshIconThemeSetsForTheme(
        string iconThemeId) =>
        target.RefreshIconThemeSetsForTheme(iconThemeId);

    public void DeleteIconThemeToken(string iconThemeId, string token) =>
        target.DeleteIconThemeToken(iconThemeId, token);

    public IconThemeTokenSvg ReadIconThemeTokenSvg(
        string iconThemeId,
        string token) =>
        target.ReadIconThemeTokenSvg(iconThemeId, token);

    public IconThemeReplaceSvgResult ReplaceIconThemeTokenSvg(
        string iconThemeId,
        string token,
        string svgText) =>
        target.ReplaceIconThemeTokenSvg(iconThemeId, token, svgText);

    public IconThemeWriteAllSvgResult WriteIconThemeTokenSvgToAllSets(
        string iconThemeId,
        string token,
        string svgText,
        string description) =>
        target.WriteIconThemeTokenSvgToAllSets(
            iconThemeId,
            token,
            svgText,
            description);

    public IconThemeSearchResult SearchIconThemeSources(
        string query,
        CancellationToken cancellationToken = default) =>
        target.SearchIconThemeSources(query, cancellationToken);

    public IconThemeGenerateResult GenerateIconThemeToken(
        string iconThemeId,
        string token,
        string category,
        string description,
        string lucideSource,
        string materialSource,
        CancellationToken cancellationToken = default) =>
        target.GenerateIconThemeToken(
            iconThemeId,
            token,
            category,
            description,
            lucideSource,
            materialSource,
            cancellationToken);

    public string ResolveIconThemeAssetPath(
        string iconThemeId,
        string file) =>
        target.ResolveIconThemeAssetPath(iconThemeId, file);
}

internal sealed class SqliteThemeTokenPort(IThemeTokenQuery target)
    : IThemeTokenQuery
{
    public IReadOnlyList<FieldOption> GetThemeOptions(string projectId) =>
        target.GetThemeOptions(projectId);

    public IReadOnlyList<ThemeTokenOption> GetThemeTokenOptions(
        string projectId,
        string themeId) =>
        target.GetThemeTokenOptions(projectId, themeId);
}

internal sealed class SqliteRuntimeInputOwnerPort(
    IRuntimeInputOwnerStore target)
    : IRuntimeInputOwnerStore
{
    public ComponentVariantSelectionSettings
        GetComponentVariantSelectionSettings(string variantReference) =>
        target.GetComponentVariantSelectionSettings(variantReference);

    public ComponentClassSettings GetComponentVariantSettings(
        ProjectTreeNode variantNode) =>
        target.GetComponentVariantSettings(variantNode);

    public JsonObject GetComponentVariantRuntimeInputs(
        string variantReference) =>
        target.GetComponentVariantRuntimeInputs(variantReference);

    public string GetComponentClassDesignPreviewJson(
        string componentClassId) =>
        target.GetComponentClassDesignPreviewJson(componentClassId);

    public ModuleSettings GetModuleSettings(string moduleId) =>
        target.GetModuleSettings(moduleId);

    public ModuleSettings GetModuleVariantSettings(
        ProjectTreeNode variantNode) =>
        target.GetModuleVariantSettings(variantNode);

    public void UpdateComponentClassDesignPreviewJson(
        string componentClassId,
        string designPreviewJson) =>
        target.UpdateComponentClassDesignPreviewJson(
            componentClassId,
            designPreviewJson);

    public void UpdateModuleDesignPreviewJson(
        string moduleId,
        string designPreviewJson) =>
        target.UpdateModuleDesignPreviewJson(moduleId, designPreviewJson);
}

internal class SqliteModuleInstanceAnimationPort(
    IModuleInstanceAnimationStore target)
    : IModuleInstanceAnimationStore
{
    protected IModuleInstanceAnimationStore Target { get; } = target;

    public void UpdateModuleInstanceAnimationJson(
        string moduleInstanceId,
        string animationJson) =>
        Target.UpdateModuleInstanceAnimationJson(
            moduleInstanceId,
            animationJson);
}

internal sealed class SqliteRuntimeInputInstancePort(
    IRuntimeInputInstanceStore target)
    : IRuntimeInputInstanceStore
{
    public void UpdateModuleInstanceRuntimeValue(
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value) =>
        target.UpdateModuleInstanceRuntimeValue(
            moduleInstanceId,
            jsonKey,
            value);

    public void UpdateModuleInstanceRuntimeCollectionValue(
        string moduleInstanceId,
        StructuredCollectionAddress address,
        string itemId,
        string fieldJsonKey,
        JsonNode? value) =>
        target.UpdateModuleInstanceRuntimeCollectionValue(
            moduleInstanceId,
            address,
            itemId,
            fieldJsonKey,
            value);

    public void UpdateModuleInstanceRuntimeCollectionValues(
        string moduleInstanceId,
        StructuredCollectionAddress address,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values) =>
        target.UpdateModuleInstanceRuntimeCollectionValues(
            moduleInstanceId,
            address,
            itemId,
            values);

    public StructuredCollectionMutationResult MutateModuleInstanceStructuredCollection(
        string moduleInstanceId,
        StructuredCollectionMutation mutation) =>
        target.MutateModuleInstanceStructuredCollection(
            moduleInstanceId,
            mutation);

}

internal sealed class SqliteReferenceUsagePort(IReferenceUsageQuery target)
    : IReferenceUsageQuery
{
    public IReadOnlyList<ReferenceUsageDetail> GetReferenceUsageDetails(
        ProjectTreeNode node) =>
        target.GetReferenceUsageDetails(node);
}

internal sealed class SqliteEditorNodeCommandPort(
    SqliteEditorNodeCommandStore target)
    : IEditorNodeCommandStore
{
    public void Delete(ProjectTreeNode node) =>
        target.Delete(node);

    public ProjectTreeNode Duplicate(ProjectTreeNode node) =>
        target.Duplicate(node);

    public ProjectTreeNode DuplicateShot(
        ProjectTreeNode shot,
        int shotNumber) =>
        target.DuplicateShot(shot, shotNumber);

    public ProjectTreeNode RenameDirectNode(
        ProjectTreeNode node,
        string name) =>
        target.RenameDirectNode(node, name);

    public void ReplaceComponentVariantConfig(
        ProjectTreeNode node,
        string configJson) =>
        target.ReplaceComponentVariantConfig(node, configJson);

    public void ReplaceModuleVariantConfig(
        ProjectTreeNode node,
        string configJson) =>
        target.ReplaceModuleVariantConfig(node, configJson);

    public ProjectTreeNode SaveComponentVariant(
        ProjectTreeNode sourceNode,
        string name) =>
        target.SaveComponentVariant(sourceNode, name);

    public ProjectTreeNode SaveModuleVariant(
        ProjectTreeNode sourceNode,
        string name) =>
        target.SaveModuleVariant(sourceNode, name);

    public ProjectTreeNode ToggleComponentVariantLock(
        ProjectTreeNode node) =>
        target.ToggleComponentVariantLock(node);

    public ProjectTreeNode ToggleModuleVariantLock(
        ProjectTreeNode node) =>
        target.ToggleModuleVariantLock(node);
}

internal sealed class SqlitePreviewInputPort(
    SqliteProductionOwner production,
    SqliteDesignOwner design,
    SqliteResourceOwner resources)
    : IPreviewInputRepository
{
    public ShotSettings GetShotSettings(string shotId) =>
        production.GetShotSettings(shotId);

    public AppSettings GetAppSettings(string appId) =>
        design.GetAppSettings(appId);

    public AppSettings GetModuleAppSettings(string moduleId) =>
        design.GetModuleAppSettings(moduleId);

    public ModuleSettings GetModuleSettings(string moduleId) =>
        design.GetModuleSettings(moduleId);

    public ModuleSettings GetModuleVariantSettings(
        ProjectTreeNode variantNode) =>
        design.GetModuleVariantSettings(variantNode);

    public string GetModuleInstanceVariantName(
        string moduleInstanceId) =>
        production.GetModuleInstanceVariantName(moduleInstanceId);

    public ComponentClassSettings GetComponentClassSettings(
        string componentClassId) =>
        design.GetComponentClassSettings(componentClassId);

    public ComponentClassSettings GetComponentVariantSettings(
        ProjectTreeNode variantNode) =>
        design.GetComponentVariantSettings(variantNode);

    public string GetComponentClassBaseConfigsJson(string projectId) =>
        design.GetComponentClassBaseConfigsJson(projectId);

    public string ValidateComponentVariantReferencesForPreview(
        string projectId,
        string configJson) =>
        design.ValidateComponentVariantReferencesForPreview(
            projectId,
            configJson);

    public DeviceSettings GetDeviceSettings(string deviceId) =>
        resources.GetDeviceSettings(deviceId);

    public DevicePreviewMetrics GetDevicePreviewMetrics(string deviceId) =>
        resources.GetDevicePreviewMetrics(deviceId);

    public IReadOnlyList<FieldOption> GetDeviceOptions(string projectId) =>
        resources.GetDeviceOptions(projectId);

    public ThemeSettings GetThemeSettings(string themeId) =>
        resources.GetThemeSettings(themeId);

    public string GetThemeFieldValue(
        string themeId,
        string fieldId) =>
        resources.GetThemeFieldValue(themeId, fieldId);

    public IReadOnlyList<FieldOption> GetThemeOptions(string projectId) =>
        resources.GetThemeOptions(projectId);

    public IReadOnlyDictionary<string, string> GetPaletteColorMap(
        string projectId) =>
        resources.GetPaletteColorMap(projectId);

    public IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(
        string projectId) =>
        resources.GetPaletteNeutralMap(projectId);

    public IReadOnlyList<ProductionFontFace> GetProductionFontFaces(
        string projectId) =>
        resources.GetProductionFontFaces(projectId);

    public IconThemeSettings GetIconThemeSettings(string iconThemeId) =>
        resources.GetIconThemeSettings(iconThemeId);
}

internal sealed class SqliteDictionaryFieldContextPort(
    SqliteDesignOwner design,
    SqliteResourceOwner resources)
    : IDictionaryFieldContextRepository
{
    public ThemeSettings GetThemeSettings(string themeId) =>
        resources.GetThemeSettings(themeId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId) =>
        resources.GetModuleInstanceThemeTokensJson(moduleInstanceId);

    public IReadOnlyList<FieldOption> GetPaletteColorOptions(
        string projectId) =>
        resources.GetPaletteColorOptions(projectId);

    public ComponentVariantSelectionSettings
        GetComponentVariantSelectionSettings(string variantReference) =>
            design.GetComponentVariantSelectionSettings(
                variantReference);

    public JsonObject GetComponentVariantRuntimeInputs(
        string variantReference) =>
        design.GetComponentVariantRuntimeInputs(variantReference);

    public IReadOnlyList<ComponentInputBindingDefinition>
        GetComponentVariantRuntimeInputBindings(
            string variantReference) =>
            design.GetComponentVariantRuntimeInputBindings(
                variantReference);

    public IReadOnlyList<RuntimeInputCollectionDefinition>
        GetComponentVariantRuntimeCollections(
            string variantReference) =>
            design.GetComponentVariantRuntimeCollections(
                variantReference);

    public IReadOnlyList<FieldOption>
        GetComponentVariantReferenceOptionsByType(
            string projectId,
            string componentType,
            bool includeNone) =>
            design.GetComponentVariantReferenceOptionsByType(
                projectId,
                componentType,
                includeNone);

    public IReadOnlyList<FieldOption> GetComponentVariantReferenceOptions(
        string projectId,
        string componentTypeSelector,
        bool includeNone) =>
        design.GetComponentVariantReferenceOptions(
            projectId,
            componentTypeSelector,
            includeNone);

    public string GetRuntimeComponentVariantName(
        string variantReference,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        design.GetRuntimeComponentVariantName(
            variantReference,
            overrides,
            slots);

    public IReadOnlyList<IconThemeToken> GetIconThemeTokens(
        string iconThemeId) =>
        resources.GetIconThemeTokens(iconThemeId);

    public string ResolveIconThemeAssetPath(
        string iconThemeId,
        string file) =>
        resources.ResolveIconThemeAssetPath(iconThemeId, file);
}

internal sealed class SqliteRenderSnapshotPort(
    IPreviewInputRepository preview,
    IActorPreviewRepository actors,
    IComponentPreviewInputRepository components,
    IModuleInstanceTimelineStore timeline,
    IModuleInstanceThemeTokenQuery moduleInstanceThemes,
    SqliteProductionOwner output)
    : IRenderSnapshotDataSource
{
    public ProjectTreeNode GetCurrentRenderShot(string shotId) =>
        output.GetCurrentRenderShot(shotId);

    public ProductionOutputShotContext GetProductionOutputShotContext(
        string shotId) =>
        output.GetProductionOutputShotContext(shotId);

    public string GetModuleInstanceThemeTokensJson(
        string moduleInstanceId) =>
        moduleInstanceThemes.GetModuleInstanceThemeTokensJson(
            moduleInstanceId);

    public ActorSettings GetActorSettings(string actorId) =>
        actors.GetActorSettings(actorId);

    public string GetActorFieldValue(
        string actorId,
        string fieldId) =>
        actors.GetActorFieldValue(actorId, fieldId);

    public IReadOnlyList<FieldOption> GetActorOptions(string projectId) =>
        actors.GetActorOptions(projectId);

    public IReadOnlyList<FieldOption> GetRequiredActorOptions(
        string projectId) =>
        actors.GetRequiredActorOptions(projectId);

    public IReadOnlyList<FieldOption> GetPaletteColorOptions(
        string projectId) =>
        actors.GetPaletteColorOptions(projectId);

    public ProjectSettings GetProjectSettings(string projectId) =>
        actors.GetProjectSettings(projectId);

    public JsonObject GetComponentVariantConfig(
        string variantReference) =>
        components.GetComponentVariantConfig(variantReference);

    public JsonObject GetComponentVariantRuntimeContract(
        string variantReference) =>
        components.GetComponentVariantRuntimeContract(variantReference);

    public string ValidateComponentVariantReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty) =>
        components.ValidateComponentVariantReferenceValue(
            projectId,
            componentType,
            reference,
            allowEmpty);

    public ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId) =>
        timeline.GetModuleInstanceSettings(moduleInstanceId);

    public ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId) =>
        timeline.GetModuleInstanceVariantSettings(moduleInstanceId);

    public string GetModuleInstanceModuleName(
        string moduleInstanceId) =>
        timeline.GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceTransitionType(
        string moduleInstanceId) =>
        timeline.GetModuleInstanceTransitionType(moduleInstanceId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId) =>
        timeline.GetModuleInstanceEffectiveContractJson(
            moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId) =>
        timeline.GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public IReadOnlyList<ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId) =>
        timeline.GetShotModuleInstanceSlots(shotId);

    public ShotSettings GetShotSettings(string shotId) =>
        preview.GetShotSettings(shotId);

    public AppSettings GetAppSettings(string appId) =>
        preview.GetAppSettings(appId);

    public AppSettings GetModuleAppSettings(string moduleId) =>
        preview.GetModuleAppSettings(moduleId);

    public ModuleSettings GetModuleSettings(string moduleId) =>
        preview.GetModuleSettings(moduleId);

    public ModuleSettings GetModuleVariantSettings(
        ProjectTreeNode variantNode) =>
        preview.GetModuleVariantSettings(variantNode);

    public string GetModuleInstanceVariantName(
        string moduleInstanceId) =>
        preview.GetModuleInstanceVariantName(moduleInstanceId);

    public ComponentClassSettings GetComponentClassSettings(
        string componentClassId) =>
        preview.GetComponentClassSettings(componentClassId);

    public ComponentClassSettings GetComponentVariantSettings(
        ProjectTreeNode variantNode) =>
        preview.GetComponentVariantSettings(variantNode);

    public string GetComponentClassBaseConfigsJson(string projectId) =>
        preview.GetComponentClassBaseConfigsJson(projectId);

    public string ValidateComponentVariantReferencesForPreview(
        string projectId,
        string configJson) =>
        preview.ValidateComponentVariantReferencesForPreview(
            projectId,
            configJson);

    public DeviceSettings GetDeviceSettings(string deviceId) =>
        preview.GetDeviceSettings(deviceId);

    public DevicePreviewMetrics GetDevicePreviewMetrics(string deviceId) =>
        preview.GetDevicePreviewMetrics(deviceId);

    public IReadOnlyList<FieldOption> GetDeviceOptions(string projectId) =>
        preview.GetDeviceOptions(projectId);

    public ThemeSettings GetThemeSettings(string themeId) =>
        preview.GetThemeSettings(themeId);

    public string GetThemeFieldValue(
        string themeId,
        string fieldId) =>
        preview.GetThemeFieldValue(themeId, fieldId);

    public IReadOnlyList<FieldOption> GetThemeOptions(string projectId) =>
        preview.GetThemeOptions(projectId);

    public IReadOnlyDictionary<string, string> GetPaletteColorMap(
        string projectId) =>
        preview.GetPaletteColorMap(projectId);

    public IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(
        string projectId) =>
        preview.GetPaletteNeutralMap(projectId);

    public IReadOnlyList<ProductionFontFace> GetProductionFontFaces(
        string projectId) =>
        preview.GetProductionFontFaces(projectId);

    public IconThemeSettings GetIconThemeSettings(string iconThemeId) =>
        preview.GetIconThemeSettings(iconThemeId);
}
