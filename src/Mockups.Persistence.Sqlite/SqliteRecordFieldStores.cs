using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionRecordFieldStore :
    IProductionRecordFieldStore
{
    private readonly SqliteProjectContext _context;
    private readonly SqliteProductionOwner _production;
    private readonly SqliteDesignOwner _design;
    private readonly SqliteResourceOwner _resources;

    internal SqliteProductionRecordFieldStore(
        SqliteProjectContext context,
        SqliteProductionOwner production,
        SqliteDesignOwner design,
        SqliteResourceOwner resources)
    {
        _context = context;
        _production = production;
        _design = design;
        _resources = resources;
    }

    public ProjectSettings GetProjectSettings(string projectId) =>
        _production.GetProjectSettings(projectId);

    public void UpdateProjectField(
        string projectId,
        string fieldId,
        string value) =>
        _production.UpdateProjectField(projectId, fieldId, value);

    public void ConnectShotManagerProduction(
        string projectId,
        ShotManagerReadonlyProduction production,
        string workstreamName,
        string folderName) =>
        _production.ConnectShotManagerProduction(
            projectId,
            production,
            workstreamName,
            folderName);

    public void SetShotManagerProductionEnabled(
        string projectId,
        bool enabled) =>
        _production.SetShotManagerProductionEnabled(projectId, enabled);

    public void RefreshShotManagerProduction(
        string projectId,
        ShotManagerReadonlyProduction production) =>
        _production.RefreshShotManagerProduction(projectId, production);

    public EpisodeSettings GetEpisodeSettings(string episodeId) =>
        _production.GetEpisodeSettings(episodeId);

    public void UpdateEpisodeField(
        string episodeId,
        string fieldId,
        string value) =>
        _production.UpdateEpisodeField(episodeId, fieldId, value);

    public void AssociateShotManagerEpisode(
        string episodeId,
        ShotManagerReadonlyEpisode? episode) =>
        _production.AssociateShotManagerEpisode(episodeId, episode);

    public ShotSettings GetShotSettings(string shotId) =>
        _production.GetShotSettings(shotId);

    public void UpdateShotField(
        string shotId,
        string fieldId,
        string value)
    {
        using var connection = _context.OpenConnection();
        if (_production.UpdateShotField(
                connection,
                shotId,
                fieldId,
                value))
        {
            _production.SynchronizeTimelineDurations(
                connection,
                shotId);
        }
    }

    public void AssociateShotManagerShot(
        string shotId,
        ShotManagerReadonlyShot? shot)
    {
        lock (_context.WriteGate)
        {
            using var connection = _context.OpenConnection();
            _production.AssociateShotManagerShot(
                connection,
                shotId,
                shot);
        }
    }

    public ProductionOutputShotContext GetProductionOutputShotContext(
        string shotId) =>
        _production.GetProductionOutputShotContext(shotId);

    public string GetModuleInstanceVariantReference(
        string moduleInstanceId) =>
        _production.GetModuleInstanceVariantReference(
            moduleInstanceId);

    public void UpdateModuleInstanceField(
        string moduleInstanceId,
        string fieldId,
        string value)
    {
        using var connection = _context.OpenConnection();
        _production.UpdateModuleInstanceField(
            connection,
            moduleInstanceId,
            fieldId,
            value,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
    }
}

internal sealed class SqliteRecordReferenceOverrideStore :
    IRecordReferenceOverrideStore
{
    private const string ShotDeviceOverrides =
        "shot.deviceOverrides";
    private readonly SqliteProductionOwner _production;
    private readonly SqliteResourceOwner _resources;

    internal SqliteRecordReferenceOverrideStore(
        SqliteProductionOwner production,
        SqliteResourceOwner resources)
    {
        _production = production;
        _resources = resources;
    }

    public string GetOverrideDocument(
        ProjectTreeNode ownerNode,
        string documentFieldId)
    {
        RequireShotDeviceDocument(
            ownerNode,
            documentFieldId);
        return _production.GetShotSettings(
            ownerNode.Id).DeviceOverridesJson;
    }

    public void UpdateOverrideDocument(
        ProjectTreeNode ownerNode,
        string documentFieldId,
        string overridesJson)
    {
        RequireShotDeviceDocument(
            ownerNode,
            documentFieldId);
        var shot = _production.GetShotSettings(
            ownerNode.Id);
        var actor = _resources.GetActorSettings(
            shot.OwnerActorId);
        var deviceId = shot.EffectiveDeviceId(
            actor.DefaultDeviceId);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new InvalidOperationException(
                $"Shot '{ownerNode.Id}' has no effective Device.");
        }
        _ = DeviceSettingsFieldContract.ApplyOverrides(
            _resources.GetDeviceSettings(deviceId),
            overridesJson,
            $"Shot '{ownerNode.Id}' Device overrides");
        _production.UpdateShotDeviceOverrides(
            ownerNode.Id,
            overridesJson);
    }

    private static void RequireShotDeviceDocument(
        ProjectTreeNode ownerNode,
        string documentFieldId)
    {
        if (ownerNode.Kind != ProjectTreeNodeKind.Shot
            || !documentFieldId.Equals(
                ShotDeviceOverrides,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"RecordReference Overrides document '{documentFieldId}' is not owned by '{ownerNode.RecordClassId}'.");
        }
    }
}

internal sealed partial class SqliteDesignRecordFieldStore :
    IDesignRecordFieldStore
{
    private readonly SqliteDesignOwner _design;

    internal SqliteDesignRecordFieldStore(
        SqliteDesignOwner design)
    {
        _design = design;
    }

    public AppSettings GetAppSettings(string appId) =>
        _design.GetAppSettings(appId);

    public void UpdateAppField(
        string appId,
        string fieldId,
        string value) =>
        _design.UpdateAppField(appId, fieldId, value);

    public string GetAppConfigFieldValue(
        string appId,
        string fieldId) =>
        _design.GetAppConfigFieldValue(appId, fieldId);

    public string GetAppMetadataFieldValue(
        string appId,
        string fieldId) =>
        _design.GetAppMetadataFieldValue(appId, fieldId);

    public ModuleSettings GetModuleSettings(string moduleId) =>
        _design.GetModuleSettings(moduleId);

    public string GetModuleConfigFieldValue(
        string moduleId,
        string fieldId) =>
        _design.GetModuleConfigFieldValue(moduleId, fieldId);

    public ModuleSettings GetModuleVariantSettings(
        ProjectTreeNode variantNode) =>
        _design.GetModuleVariantSettings(variantNode);

    public string GetModuleVariantConfigFieldValue(
        ProjectTreeNode node,
        string fieldId) =>
        _design.GetModuleVariantConfigFieldValue(node, fieldId);

    public IReadOnlyList<FieldOption> GetModuleVariantOptions(
        string moduleId) =>
        _design.GetModuleVariantOptions(moduleId);

    public void UpdateModuleField(
        string moduleId,
        string fieldId,
        string value) =>
        _design.UpdateModuleField(moduleId, fieldId, value);

    public void UpdateModuleVariantField(
        ProjectTreeNode node,
        string fieldId,
        string value) =>
        _design.UpdateModuleVariantField(node, fieldId, value);
}

internal sealed class SqliteResourceRecordFieldStore :
    IResourceRecordFieldStore
{
    private readonly SqliteResourceOwner _resources;
    private readonly SqliteCoreFieldStore _coreFields;

    internal SqliteResourceRecordFieldStore(
        SqliteResourceOwner resources,
        SqliteCoreFieldStore coreFields)
    {
        _resources = resources;
        _coreFields = coreFields;
    }

    public PaletteColorSettings GetPaletteColorSettings(
        string colorId) =>
        _resources.GetPaletteColorSettings(colorId);

    public IReadOnlyList<FieldOption> GetPaletteColorOptions(
        string projectId) =>
        _resources.GetPaletteColorOptions(projectId);

    public void UpdatePaletteColorField(
        string colorId,
        string fieldId,
        string value) =>
        _resources.UpdatePaletteColorField(
            colorId,
            fieldId,
            value);

    public DeviceSettings GetDeviceSettings(string deviceId) =>
        _resources.GetDeviceSettings(deviceId);

    public string GetDeviceMetricFieldValue(
        string deviceId,
        string fieldId) =>
        _resources.GetDeviceMetricFieldValue(deviceId, fieldId);

    public IReadOnlyList<FieldOption> GetDeviceOptions(
        string projectId) =>
        _resources.GetDeviceOptions(projectId);

    public void UpdateDeviceField(
        string deviceId,
        string fieldId,
        string value) =>
        _resources.UpdateDeviceField(deviceId, fieldId, value);

    public ActorSettings GetActorSettings(string actorId) =>
        _resources.GetActorSettings(actorId);

    public string GetActorFieldValue(
        string actorId,
        string fieldId) =>
        _resources.GetActorFieldValue(actorId, fieldId);

    public IReadOnlyList<FieldOption> GetRequiredActorOptions(
        string projectId) =>
        _resources.GetRequiredActorOptions(projectId);

    public void UpdateActorField(
        string actorId,
        string fieldId,
        string value) =>
        _resources.UpdateActorField(actorId, fieldId, value);

    public ThemeSettings GetThemeSettings(string themeId) =>
        _resources.GetThemeSettings(themeId);

    public string GetThemeFieldValue(
        string themeId,
        string fieldId) =>
        _resources.GetThemeFieldValue(themeId, fieldId);

    public IReadOnlyList<FieldOption> GetThemeOptions(
        string projectId) =>
        _resources.GetThemeOptions(projectId);

    public void UpdateThemeField(
        string themeId,
        string fieldId,
        string value) =>
        _resources.UpdateThemeField(themeId, fieldId, value);

    public IReadOnlyList<FieldOption> GetIconThemeOptions(
        string projectId) =>
        _resources.GetIconThemeOptions(projectId);

    public string GetIconThemeFieldValue(
        string iconThemeId,
        string fieldId) =>
        _resources.GetIconThemeFieldValue(iconThemeId, fieldId);

    public IReadOnlyList<FieldOption> GetProductionFontOptions(
        string projectId,
        string? category = null) =>
        _resources.GetProductionFontOptions(projectId, category);

    public string GetProductionFontFieldValue(
        string fontId,
        string fieldId) =>
        _resources.GetProductionFontFieldValue(fontId, fieldId);

    public void UpdateProductionFontField(
        string fontId,
        string fieldId,
        string value) =>
        _resources.UpdateProductionFontField(
            fontId,
            fieldId,
            value);

    public ProjectTreeNode RenamePaletteColor(
        ProjectTreeNode node,
        string name) =>
        _coreFields.RenameDirectNode(node, name);
}

internal sealed partial class SqliteDesignRecordFieldStore
{
    public IReadOnlyList<FieldOption>
        GetComponentVariantReferenceOptionsByType(
            string projectId,
            string componentType,
            bool includeNone = false) =>
        _design.GetComponentVariantReferenceOptionsByType(
            projectId,
            componentType,
            includeNone);

    public IReadOnlyList<FieldOption>
        GetStatusBarComponentVariantOptions(string projectId) =>
        _design.GetStatusBarComponentVariantOptions(projectId);

    public IReadOnlyList<FieldOption>
        GetNavigationBarComponentVariantOptions(string projectId) =>
        _design.GetNavigationBarComponentVariantOptions(projectId);
}

internal sealed partial class SqliteProductionRecordFieldStore
{
    private IReadOnlySet<string> ModuleInstanceProjectActorIds(
        SqliteConnection connection,
        string moduleInstanceId)
    {
        var instance = _production.ModuleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        var module = _design.AppModuleRepository.GetModule(
            connection,
            instance.ModuleId);
        return _resources.ActorRepository.QueryAll(connection)
            .Where((actor) => actor.ProjectId.Equals(
                module.ProjectId,
                StringComparison.Ordinal))
            .Select((actor) => actor.Id)
            .ToHashSet(StringComparer.Ordinal);
    }
}
