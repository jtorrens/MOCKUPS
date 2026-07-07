using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ComponentPreviewRecordInputResolver
{
    private readonly SpikeDatabase _database;

    public ComponentPreviewRecordInputResolver(SpikeDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<FieldOption> Options(string projectId, string tableId, string inputId)
    {
        return tableId switch
        {
            "actors" => _database.GetActorOptions(projectId),
            _ => throw new InvalidOperationException(
                $"Unsupported record reference input table '{tableId}' for '{inputId}'."),
        };
    }

    public JsonNode ResolvedPreviewValue(
        string tableId,
        string recordId,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors,
        string inputId)
    {
        return tableId switch
        {
            "actors" => !string.IsNullOrWhiteSpace(recordId)
                ? ActorPreviewInputFactory.Create(_database, recordId, themeMode, paletteColors)
                : ActorPreviewInputFactory.CreateSample(),
            _ => throw new InvalidOperationException(
                $"Unsupported record reference input table '{tableId}' for '{inputId}'."),
        };
    }
}
