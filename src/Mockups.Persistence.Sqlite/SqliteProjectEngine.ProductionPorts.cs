using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public ShotSettings GetShotSettings(string shotId) =>
        _productionOwner.GetShotSettings(shotId);

    public string GetShotRenderName(string shotId) =>
        _productionOwner.GetShotRenderName(shotId);

    public ProjectSettings GetProjectSettings(string projectId) =>
        _productionOwner.GetProjectSettings(projectId);

    public void UpdateProjectField(
        string projectId,
        string fieldId,
        string value) =>
        _productionOwner.UpdateProjectField(projectId, fieldId, value);

    public EpisodeSettings GetEpisodeSettings(string episodeId) =>
        _productionOwner.GetEpisodeSettings(episodeId);

    public void UpdateEpisodeField(
        string episodeId,
        string fieldId,
        string value) =>
        _productionOwner.UpdateEpisodeField(episodeId, fieldId, value);

    public ProductionOutputShotPlan GetProductionOutputShotPlan(
        string shotId) =>
        _productionOwner.GetProductionOutputShotPlan(shotId);
}
