using Mockups.DesktopEditorShell.Data;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ComponentPreviewInputDataSource
{
    private readonly SpikeDatabase _database;

    public ComponentPreviewInputDataSource(SpikeDatabase database)
    {
        _database = database;
    }

    public int ProjectDefaultFrameRate(string projectId)
    {
        return _database.GetProjectSettings(projectId).DefaultFps;
    }

    public JsonObject ComponentPresetConfig(string presetReference)
    {
        return _database.GetComponentPresetConfig(presetReference);
    }

    public JsonObject ComponentPresetRuntimeContract(string presetReference)
    {
        return _database.GetComponentPresetRuntimeContract(presetReference);
    }

    public string ValidateComponentPresetReference(
        string projectId,
        string componentType,
        string presetReference)
    {
        return _database.ValidateComponentPresetReferenceValue(
            projectId,
            componentType,
            presetReference);
    }
}
