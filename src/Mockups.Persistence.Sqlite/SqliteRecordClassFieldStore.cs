using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteRecordClassFieldStore :
    IRecordClassFieldStore
{
    private readonly SqliteProjectContext _context;
    private readonly SqliteDesignOwner _design;
    private readonly SqliteProductionOwner _production;
    private readonly SqliteResourceOwner _resources;
    private readonly SqliteCoreFieldStore _coreFields;

    internal SqliteRecordClassFieldStore(
        SqliteProjectContext context,
        SqliteDesignOwner design,
        SqliteProductionOwner production,
        SqliteResourceOwner resources,
        SqliteCoreFieldStore coreFields)
    {
        _context = context;
        _design = design;
        _production = production;
        _resources = resources;
        _coreFields = coreFields;
    }

    public ProjectSettings GetProjectSettings(string projectId) =>
        _production.GetProjectSettings(projectId);

    public void UpdateProjectField(
        string projectId,
        string fieldId,
        string value) =>
        _production.UpdateProjectField(projectId, fieldId, value);

    public EpisodeSettings GetEpisodeSettings(string episodeId) =>
        _production.GetEpisodeSettings(episodeId);

    public void UpdateEpisodeField(
        string episodeId,
        string fieldId,
        string value) =>
        _production.UpdateEpisodeField(episodeId, fieldId, value);

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

    public string GetShotRenderName(string shotId) =>
        _production.GetShotRenderName(shotId);

    public string GetShotOwnerDeviceName(string shotId)
    {
        using var connection = _context.OpenConnection();
        var shot = _production.ShotRepository.Get(
            connection,
            shotId);
        var actor = _resources.ActorRepository
            .QueryAll(connection)
            .SingleOrDefault((candidate) =>
                candidate.Id == shot.OwnerActorId)
            ?? throw new InvalidOperationException(
                $"Missing Actor '{shot.OwnerActorId}'.");
        if (string.IsNullOrWhiteSpace(actor.DefaultDeviceId))
        {
            return "No default device";
        }

        return _resources.DeviceRepository
            .QueryAll(connection)
            .SingleOrDefault((candidate) =>
                candidate.Id == actor.DefaultDeviceId)
            ?.Name
            ?? throw new InvalidOperationException(
                $"Missing Device '{actor.DefaultDeviceId}'.");
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

    public ProjectTreeNode RenameDirectNode(
        ProjectTreeNode node,
        string name) =>
        _coreFields.RenameDirectNode(node, name);

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
