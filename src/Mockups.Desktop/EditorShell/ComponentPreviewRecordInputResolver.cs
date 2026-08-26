using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ComponentPreviewRecordInputResolver
{
    private readonly ActorPreviewDataSource _actorDataSource;
    private readonly IProjectPathResolver _projectPaths;

    public ComponentPreviewRecordInputResolver(
        ActorPreviewDataSource actorDataSource,
        IProjectPathResolver projectPaths)
    {
        _actorDataSource = actorDataSource;
        _projectPaths = projectPaths;
    }

    public IReadOnlyList<FieldOption> Options(string projectId, string tableId, string inputId)
    {
        return tableId switch
        {
            "actors" => _actorDataSource.Options(projectId),
            _ => throw new InvalidOperationException(
                $"Unsupported record reference input table '{tableId}' for '{inputId}'."),
        };
    }

    public JsonNode ResolvedPreviewValue(
        string tableId,
        string recordId,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors,
        string inputId,
        bool allowEmpty = false)
    {
        return tableId switch
        {
            "actors" => !string.IsNullOrWhiteSpace(recordId)
                ? ActorPreviewInputFactory.Create(
                    _actorDataSource,
                    _projectPaths,
                    recordId,
                    themeMode,
                    paletteColors)
                : allowEmpty
                    ? new JsonObject()
                    : ActorPreviewInputFactory.CreateSample(),
            _ => throw new InvalidOperationException(
                $"Unsupported record reference input table '{tableId}' for '{inputId}'."),
        };
    }

    public string ProjectId(
        string tableId,
        string recordId,
        string inputId)
    {
        return tableId switch
        {
            "actors" => _actorDataSource.LoadContext(recordId).ProjectId,
            _ => throw new InvalidOperationException(
                $"Unsupported record reference input table '{tableId}' for '{inputId}'."),
        };
    }
}
