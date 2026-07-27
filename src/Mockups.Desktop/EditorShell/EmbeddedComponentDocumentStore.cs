using Mockups.DesktopEditorShell.Data;
using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EmbeddedComponentDocumentStore
{
    private readonly IComponentDocumentStore _database;
    private readonly SemaphoreSlim _runtimeCommitGate =
        new(1, 1);

    public EmbeddedComponentDocumentStore(IComponentDocumentStore database)
    {
        _database = database;
    }

    public string ActiveVariantName(EditorEmbeddedContext context)
    {
        return context.RuntimeSource is null
            ? _database.GetEmbeddedComponentVariantName(context.OwnerNode, context.Slots)
            : _database.GetRuntimeComponentVariantName(
                context.RuntimeSource.VariantReference,
                context.RuntimeSource.Overrides,
                context.Slots);
    }

    public FieldValue CreateFieldValue(EditorEmbeddedContext context, string fieldId)
    {
        return context.RuntimeSource is null
            ? _database.CreateEmbeddedComponentFieldValue(
                context.OwnerNode,
                context.Slots,
                fieldId)
            : _database.CreateRuntimeComponentOverrideFieldValue(
                context.RuntimeSource.ProjectId,
                context.RuntimeSource.BaseConfigJson,
                context.RuntimeSource.Overrides,
                context.Slots,
                fieldId);
    }

    public async Task CommitFieldValueAsync(
        EditorEmbeddedContext context,
        string fieldId,
        string value)
    {
        if (context.RuntimeSource is null)
        {
            _database.UpdateEmbeddedComponentField(
                context.OwnerNode,
                context.Slots,
                fieldId,
                value);
            return;
        }

        await _runtimeCommitGate.WaitAsync();
        try
        {
            var candidate = context.RuntimeSource.Overrides
                .DeepClone()
                .AsObject();
            _database.UpdateRuntimeComponentOverride(
                candidate,
                context.Slots,
                fieldId,
                value);
            await context.RuntimeSource.OverridesChanged(
                candidate);
            ReplaceObject(
                context.RuntimeSource.Overrides,
                candidate);
        }
        finally
        {
            _runtimeCommitGate.Release();
        }
    }

    public void CommitFieldValue(
        EditorEmbeddedContext context,
        string fieldId,
        string value)
    {
        if (context.RuntimeSource is not null)
        {
            throw new InvalidOperationException(
                "Runtime Overrides require the task-returning commit path.");
        }
        _database.UpdateEmbeddedComponentField(
            context.OwnerNode,
            context.Slots,
            fieldId,
            value);
    }

    private static void ReplaceObject(
        JsonObject target,
        JsonObject source)
    {
        target.Clear();
        foreach (var (key, value) in source)
        {
            target[key] = value?.DeepClone();
        }
    }
}
