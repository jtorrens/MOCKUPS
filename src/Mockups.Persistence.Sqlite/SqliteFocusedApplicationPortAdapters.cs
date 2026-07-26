using System.Text.Json.Nodes;
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

internal sealed class SqliteEditorChildPort(IEditorChildStore target)
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
    IModuleInstanceCollectionStore target)
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

    public ModuleSettings GetModuleSettings(string moduleId) =>
        target.GetModuleSettings(moduleId);

    public ModuleSettings GetModuleVariantSettings(
        ProjectTreeNode variantNode) =>
        target.GetModuleVariantSettings(variantNode);

    public ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId) =>
        target.GetModuleInstanceVariantSettings(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId) =>
        target.GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

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

    public ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId) =>
        Target.GetModuleInstanceSettings(moduleInstanceId);

    public ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId) =>
        Target.GetModuleInstanceVariantSettings(moduleInstanceId);

    public string GetModuleInstanceModuleName(string moduleInstanceId) =>
        Target.GetModuleInstanceModuleName(moduleInstanceId);

    public string GetModuleInstanceTransitionType(string moduleInstanceId) =>
        Target.GetModuleInstanceTransitionType(moduleInstanceId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId) =>
        Target.GetModuleInstanceEffectiveContractJson(moduleInstanceId);

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId) =>
        Target.GetModuleInstanceRuntimePreviewJson(moduleInstanceId);

    public IReadOnlyList<ModuleInstanceSlot> GetShotModuleInstanceSlots(
        string shotId) =>
        Target.GetShotModuleInstanceSlots(shotId);

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
        string collectionJsonKey,
        string itemId,
        string fieldJsonKey,
        JsonNode? value) =>
        target.UpdateModuleInstanceRuntimeCollectionValue(
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            fieldJsonKey,
            value);

    public void UpdateModuleInstanceRuntimeCollectionValues(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values) =>
        target.UpdateModuleInstanceRuntimeCollectionValues(
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            values);

    public void AddModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        JsonObject item) =>
        target.AddModuleInstanceRuntimeCollectionItem(
            moduleInstanceId,
            collectionJsonKey,
            item);

    public void InsertModuleInstanceRuntimeCollectionItemAfter(
        string moduleInstanceId,
        string collectionJsonKey,
        string afterItemId,
        JsonObject item) =>
        target.InsertModuleInstanceRuntimeCollectionItemAfter(
            moduleInstanceId,
            collectionJsonKey,
            afterItemId,
            item);

    public void DuplicateModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        JsonObject duplicate,
        IReadOnlyDictionary<string, string> targetIdMappings) =>
        target.DuplicateModuleInstanceRuntimeCollectionItem(
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            duplicate,
            targetIdMappings);

    public void DeleteModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId) =>
        target.DeleteModuleInstanceRuntimeCollectionItem(
            moduleInstanceId,
            collectionJsonKey,
            itemId);

    public void MoveModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        int offset) =>
        target.MoveModuleInstanceRuntimeCollectionItem(
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            offset);
}

internal sealed class SqliteReferenceUsagePort(IReferenceUsageQuery target)
    : IReferenceUsageQuery
{
    public IReadOnlyList<ReferenceUsageDetail> GetReferenceUsageDetails(
        ProjectTreeNode node) =>
        target.GetReferenceUsageDetails(node);
}
