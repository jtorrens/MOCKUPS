using Mockups.DesktopEditorShell.Data;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RuntimeInputOptionsDataSource
{
    private readonly SpikeDatabase _database;
    private readonly ActorPreviewDataSource _actorDataSource;

    public RuntimeInputOptionsDataSource(SpikeDatabase database)
    {
        _database = database;
        _actorDataSource = new ActorPreviewDataSource(database);
    }

    public IReadOnlyList<FieldOption> ActorOptions(string projectId)
    {
        return _actorDataSource.Options(projectId);
    }

    public IReadOnlyList<FieldOption> ComponentPresetOptions(
        string projectId,
        string componentType,
        bool includeNone)
    {
        return _database.GetComponentPresetReferenceOptions(projectId, componentType, includeNone);
    }

    public IReadOnlyList<FieldOption> PaletteColorOptions(string projectId)
    {
        return _database.GetPaletteColorOptions(projectId);
    }

    public string RuntimeComponentPresetName(string presetReference)
    {
        return _database.GetRuntimeComponentPresetName(presetReference, new JsonObject(), []);
    }
}
