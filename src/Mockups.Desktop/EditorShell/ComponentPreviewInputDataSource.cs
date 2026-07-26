using Mockups.DesktopEditorShell.Data;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ComponentPreviewInputDataSource
{
    private readonly IComponentPreviewInputRepository _database;
    private readonly IProjectSettingsQuery _projects;

    public ComponentPreviewInputDataSource(
        IComponentPreviewInputRepository database,
        IProjectSettingsQuery projects)
    {
        _database = database;
        _projects = projects;
    }

    public int ProjectDefaultFrameRate(string projectId)
    {
        return _projects.GetProjectSettings(projectId).DefaultFps;
    }

    public JsonObject ComponentVariantConfig(string variantReference)
    {
        return _database.GetComponentVariantConfig(variantReference);
    }

    public JsonObject ComponentVariantRuntimeContract(string variantReference)
    {
        return _database.GetComponentVariantRuntimeContract(variantReference);
    }

    public string ValidateComponentVariantReference(
        string projectId,
        string componentType,
        string variantReference)
    {
        return _database.ValidateComponentVariantReferenceValue(
            projectId,
            componentType,
            variantReference);
    }
}
