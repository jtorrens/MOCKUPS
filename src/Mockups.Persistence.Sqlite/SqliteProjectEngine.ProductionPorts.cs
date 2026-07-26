using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
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

    public ShotManagerProjectAssociationRecord? GetShotManagerAssociation(
        string projectId) =>
        _productionOwner.GetShotManagerAssociation(projectId);

    public ShotManagerEpisodeBindingRecord? GetShotManagerEpisodeBinding(
        string episodeId) =>
        _productionOwner.GetShotManagerEpisodeBinding(episodeId);

    public ShotManagerShotStructureRecord? GetShotManagerShotStructure(
        string shotId) =>
        _productionOwner.GetShotManagerShotStructure(shotId);

    public IReadOnlyList<ShotManagerLocalEpisodeRecord>
        LoadShotManagerLocalEpisodes(string projectId) =>
            _productionOwner.LoadShotManagerLocalEpisodes(projectId);

    public void ApplyShotManagerAssociation(
        ShotManagerAssociationWritePlan plan) =>
        _productionOwner.ApplyShotManagerAssociation(plan);

    public void DisconnectShotManager(string projectId) =>
        _productionOwner.DisconnectShotManager(projectId);

    public ProjectTreeNode AddShotFromShotManager(
        ProjectTreeNode episode,
        string actorId,
        ShotManagerExternalShotPlan plan,
        string? duplicateSourceShotId = null) =>
        _productionOwner.AddShotFromShotManager(
            episode,
            actorId,
            plan,
            duplicateSourceShotId);

    public ShotManagerShotStructureRecord StoreShotManagerPlan(
        string shotId,
        ShotManagerExternalShotPlan plan) =>
        _productionOwner.StoreShotManagerPlan(shotId, plan);
}
