using Mockups.DesktopEditorShell.Data;
using System;
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

    public IReadOnlyList<FieldOption> ActorOptions(string projectId, bool includeNone)
    {
        return _actorDataSource.Options(projectId, includeNone);
    }

    public IReadOnlyList<FieldOption> RecordReferenceOptions(
        string projectId,
        string tableId,
        bool includeNone)
    {
        return tableId switch
        {
            "actors" => ActorOptions(projectId, includeNone),
            _ => throw new InvalidOperationException(
                $"Runtime record reference table '{tableId}' has no options owner."),
        };
    }

    public IReadOnlyList<FieldOption> ComponentVariantOptions(
        string projectId,
        string componentType,
        bool includeNone)
    {
        return _database.GetComponentVariantReferenceOptions(projectId, componentType, includeNone);
    }

    public IReadOnlyList<FieldOption> PaletteColorOptions(string projectId)
    {
        return _database.GetPaletteColorOptions(projectId);
    }

    public string RuntimeComponentVariantName(string variantReference)
    {
        return _database.GetRuntimeComponentVariantName(variantReference, new JsonObject(), []);
    }
}
