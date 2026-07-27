using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal interface IRuntimeInputOptionsDataSource
{
    IReadOnlyList<FieldOption> RecordReferenceOptions(
        string projectId,
        string tableId,
        bool includeNone);

    IReadOnlyList<FieldOption> ComponentVariantOptions(
        string projectId,
        string componentType,
        bool includeNone);

    IReadOnlyList<FieldOption> PaletteColorOptions(
        string projectId);

    string RuntimeComponentVariantName(
        string variantReference);
}

internal sealed class RuntimeInputOptionsDataSource :
    IRuntimeInputOptionsDataSource
{
    private readonly IDictionaryFieldContextRepository _database;
    private readonly ActorPreviewDataSource _actorDataSource;

    public RuntimeInputOptionsDataSource(
        IDictionaryFieldContextRepository database,
        IActorPreviewRepository actors)
    {
        _database = database;
        _actorDataSource = new ActorPreviewDataSource(actors);
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

internal sealed class PreparedRuntimeInputOptionsDataSource :
    IRuntimeInputOptionsDataSource
{
    private readonly EditorDictionaryContextSnapshot _context;

    public PreparedRuntimeInputOptionsDataSource(
        EditorDictionaryContextSnapshot context)
    {
        _context = context;
    }

    public IReadOnlyList<FieldOption> RecordReferenceOptions(
        string projectId,
        string tableId,
        bool includeNone)
    {
        RequireProject(projectId);
        return _context.RecordOptions(
            tableId,
            includeNone);
    }

    public IReadOnlyList<FieldOption> ComponentVariantOptions(
        string projectId,
        string componentType,
        bool includeNone)
    {
        RequireProject(projectId);
        var options = _context.VariantOptions(
            componentType);
        return includeNone
            ? [new FieldOption("", "None"), .. options]
            : options;
    }

    public IReadOnlyList<FieldOption> PaletteColorOptions(
        string projectId)
    {
        RequireProject(projectId);
        return _context.PaletteColorOptions;
    }

    public string RuntimeComponentVariantName(
        string variantReference) =>
        _context.VariantName(variantReference);

    private void RequireProject(string projectId)
    {
        if (!projectId.Equals(
                _context.ProjectId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Prepared Runtime Input context belongs to Project '{_context.ProjectId}', not '{projectId}'.");
        }
    }
}
