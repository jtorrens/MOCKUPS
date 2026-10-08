using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EmbeddedComponentDocumentStore
{
    private readonly IComponentDocumentStore _database;
    private readonly SemaphoreSlim _runtimeCommitGate =
        new(1, 1);
    private readonly Dictionary<Guid, RuntimeDocument> _runtimeDocuments = new();

    private sealed class RuntimeDocument(string json, Func<JsonObject, Task> write,
        Func<string, Task<ProjectTreeNode>>? promote)
    {
        public string Json { get; set; } = json;
        public long Revision { get; set; }
        public Func<JsonObject, Task> Write { get; } = write;
        public Func<string, Task<ProjectTreeNode>>? Promote { get; } = promote;
    }

    public RuntimeComponentOverrideSource RegisterRuntimeOverrides(
        string projectId, string reference, string type, string recordClassId, string configJson,
        JsonObject overrides, Func<JsonObject, Task> write,
        Func<string, Task<ProjectTreeNode>>? promote = null)
    {
        var id = Guid.NewGuid();
        lock (_runtimeDocuments) _runtimeDocuments.Add(id, new(overrides.ToJsonString(), write, promote));
        return new(projectId, reference, type, recordClassId, configJson, id, promote is not null);
    }

    private RuntimeDocument Document(RuntimeComponentOverrideSource source)
    {
        lock (_runtimeDocuments) return _runtimeDocuments.TryGetValue(source.DocumentId, out var document)
            ? document : throw new InvalidOperationException("The Runtime Override document belongs to another editor session.");
    }

    internal (long Revision, string Json) Snapshot(RuntimeComponentOverrideSource source)
    {
        lock (_runtimeDocuments)
        {
            var document = Document(source);
            return (document.Revision, document.Json);
        }
    }

    private JsonObject Overrides(RuntimeComponentOverrideSource source) =>
        JsonPath.ParseRequiredObject(Snapshot(source).Json, "Runtime Overrides");

    public Task<ProjectTreeNode> PromoteRuntimeOverridesAsync(RuntimeComponentOverrideSource source, string name) =>
        (Document(source).Promote ?? throw new InvalidOperationException("This boundary cannot promote Overrides."))(name);

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
                Overrides(context.RuntimeSource),
                context.Slots);
    }

    public bool HasAuthoredOverrides(EditorEmbeddedContext context)
    {
        if (context.RuntimeSource is null)
        {
            return _database.HasEmbeddedComponentOverrides(
                context.OwnerNode,
                context.Slots);
        }
        var overrides = context.Slots.Count == 0
            ? Overrides(context.RuntimeSource)
            : RuntimeOverridesAt(
                Overrides(context.RuntimeSource),
                context.Slots);
        return overrides is not null
            && OverrideDocumentContract.HasAuthoredValues(
                overrides);
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
                Overrides(context.RuntimeSource),
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
            var candidate = Overrides(context.RuntimeSource);
            _database.UpdateRuntimeComponentOverride(
                candidate,
                context.Slots,
                fieldId,
                value);
            await PublishAsync(context.RuntimeSource, candidate);
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

    public async Task ClearOverridesAsync(
        EditorEmbeddedContext context)
    {
        if (context.RuntimeSource is null)
        {
            _database.ClearEmbeddedComponentOverrides(
                context.OwnerNode,
                context.Slots);
            return;
        }

        await _runtimeCommitGate.WaitAsync();
        try
        {
            var candidate = Overrides(context.RuntimeSource);
            var target = RuntimeOverridesAt(
                candidate,
                context.Slots);
            target?.Clear();
            await PublishAsync(context.RuntimeSource, candidate);
        }
        finally
        {
            _runtimeCommitGate.Release();
        }
    }

    private static JsonObject? RuntimeOverridesAt(
        JsonObject root,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        JsonObject current = root;
        foreach (var slot in slots)
        {
            var slotNode = JsonPath.Get(current, slot.SlotPath);
            if (slotNode is null) return null;
            var slotObject = slotNode as JsonObject
                ?? throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' must be an object.");
            if (!slotObject.TryGetPropertyValue(
                    "overrides",
                    out var overridesNode))
            {
                return null;
            }
            current = overridesNode as JsonObject
                ?? throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' overrides must be an object.");
        }
        return current;
    }

    private async Task PublishAsync(RuntimeComponentOverrideSource source, JsonObject candidate)
    {
        var document = Document(source);
        var confirmedJson = candidate.ToJsonString();
        await document.Write(candidate.DeepClone().AsObject());
        lock (_runtimeDocuments)
        {
            document.Json = confirmedJson;
            document.Revision++;
        }
    }
}
