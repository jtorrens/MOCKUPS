using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EmbeddedComponentDocumentStore
{
    private readonly IComponentDocumentStore _database;
    private readonly EditorOperationCoordinator _operations;
    private readonly Dictionary<(string Owner, string Address), ComponentOverrideFieldOwner> _owners = new();

    public EmbeddedComponentDocumentStore(IComponentDocumentStore database, EditorOperationCoordinator operations)
    {
        _database = database;
        _operations = operations;
    }

    public RuntimeComponentOverrideSource RegisterRuntimeOverrides(
        ComponentOverrideFieldOwner owner, ComponentOverrideAddress address,
        string projectId, string reference, string type, string recordClassId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner.Identity);
        // Registration supplies an owner capability, never a second copy of its data.
        lock (_owners)
        {
            _owners[(owner.Identity, address.Identity)] = owner;
        }
        return new(projectId, reference, type, recordClassId, owner.Identity,
            address with { Path = Array.AsReadOnly(address.Path.ToArray()) },
            owner.Promote is not null && address.ThemeSource == ThemeComponentVariantSource.None);
    }

    private ComponentOverrideFieldOwner Owner(RuntimeComponentOverrideSource source)
    {
        lock (_owners) return _owners.TryGetValue((source.OwnerIdentity, source.Address.Identity), out var owner)
            ? owner : throw new InvalidOperationException("The Override owner belongs to another editor session.");
    }

    internal string Snapshot(RuntimeComponentOverrideSource source) => Read(source).Overrides.ToJsonString();

    private (JsonNode Root, JsonObject Overrides) Read(RuntimeComponentOverrideSource source)
    {
        var root = JsonNode.Parse(Owner(source).Read(source.Address.FieldId))
            ?? throw new InvalidOperationException("An Override field requires its current document.");
        JsonNode current = root;
        foreach (var segment in source.Address.Path)
        {
            if (segment.Property.Length > 0 && segment.ItemId.Length == 0)
                current = (current as JsonObject)?[segment.Property]
                    ?? throw new InvalidOperationException($"Missing Override field path '{segment.Property}'.");
            else if (segment.ItemId.Length > 0 && segment.Property.Length == 0)
                current = (current as JsonArray)?.Select(item => item as JsonObject
                    ?? throw new InvalidOperationException("An Override collection item must be an object.")).SingleOrDefault(item =>
                    JsonPath.RequiredString(item, "id", "Override collection item") == segment.ItemId)
                    ?? throw new InvalidOperationException($"Missing Override collection item '{segment.ItemId}'.");
            else throw new InvalidOperationException("Invalid Override path segment.");
        }
        var boundary = current as JsonObject
            ?? throw new InvalidOperationException("An Override boundary must be an object.");
        if (source.Address.ThemeSource != ThemeComponentVariantSource.None
            && Owner(source).ThemeVariantReference?.Invoke(source.Address.ThemeSource) != source.VariantReference)
            throw new InvalidOperationException("The Theme now references another Variant. Reopen its current context.");
        if (source.Address.VariantReferenceKey.Length > 0
            && JsonPath.RequiredString(boundary, source.Address.VariantReferenceKey, "Override boundary") != source.VariantReference)
            throw new InvalidOperationException("The Override boundary now references another Variant. Reopen its current context.");
        return (root, source.Address.OverridesKey.Length == 0 ? boundary
            : JsonPath.RequiredObject(boundary, source.Address.OverridesKey, "Override boundary"));
    }

    public Task<ProjectTreeNode> PromoteRuntimeOverridesAsync(RuntimeComponentOverrideSource source, string name) =>
        _operations.ExecuteAsync(() =>
        {
            _ = Read(source);
            if (!source.CanPromoteOverridesToVariant || Owner(source).Promote is not { } promote)
                throw new InvalidOperationException("This boundary cannot promote Overrides.");
            return promote(source.Address, name);
        });

    public string ActiveVariantName(EditorEmbeddedContext context) =>
        context.RuntimeSource is not { } source
            ? _database.GetEmbeddedComponentVariantName(context.OwnerNode, context.Slots)
            : _database.GetRuntimeComponentVariantName(source.VariantReference, Read(source).Overrides, context.Slots);

    public bool HasAuthoredOverrides(EditorEmbeddedContext context)
    {
        if (context.RuntimeSource is not { } source)
            return _database.HasEmbeddedComponentOverrides(context.OwnerNode, context.Slots);
        var overrides = RuntimeOverridesAt(Read(source).Overrides, context.Slots);
        return overrides is not null && OverrideDocumentContract.HasAuthoredValues(overrides);
    }

    public FieldValue CreateFieldValue(EditorEmbeddedContext context, string fieldId)
    {
        if (context.RuntimeSource is not { } source)
            return _database.CreateEmbeddedComponentFieldValue(context.OwnerNode, context.Slots, fieldId);
        var config = _database.GetComponentVariantConfigJson(source.VariantReference);
        return _database.CreateRuntimeComponentOverrideFieldValue(
            source.ProjectId, config, Read(source).Overrides, context.Slots, fieldId);
    }

    public Task<FieldValue> CommitFieldValueAsync(EditorEmbeddedContext context, string fieldId, string value) =>
        _operations.ExecuteAsync(() =>
        {
            CommitFieldValue(context, fieldId, value);
            return CreateFieldValue(context, fieldId);
        });

    public Task<FieldValue> CreateFieldValueAsync(EditorEmbeddedContext context, string fieldId) =>
        _operations.ExecuteAsync(() => CreateFieldValue(context, fieldId));

    public void CommitFieldValue(EditorEmbeddedContext context, string fieldId, string value)
    {
        FieldOptionContract.ValidateValue(CreateFieldValue(context, fieldId).Definition, value,
            $"Dictionary field '{fieldId}'");
        if (context.RuntimeSource is not { } source)
        {
            _database.UpdateEmbeddedComponentField(context.OwnerNode, context.Slots, fieldId, value);
            return;
        }
        var current = Read(source);
        _database.UpdateRuntimeComponentOverride(current.Overrides, context.Slots, fieldId, value);
        Owner(source).Write(source.Address, current.Root.ToJsonString());
    }

    public Task ClearOverridesAsync(EditorEmbeddedContext context) =>
        _operations.ExecuteAsync(() =>
        {
            if (context.RuntimeSource is not { } source)
            {
                _database.ClearEmbeddedComponentOverrides(context.OwnerNode, context.Slots);
                return;
            }
            var current = Read(source);
            RuntimeOverridesAt(current.Overrides, context.Slots)?.Clear();
            Owner(source).Write(source.Address, current.Root.ToJsonString());
        });

    private static JsonObject? RuntimeOverridesAt(JsonObject root, IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        var current = root;
        foreach (var slot in slots)
        {
            var node = JsonPath.Get(current, slot.SlotPath);
            if (node is null) return null;
            var boundary = node as JsonObject
                ?? throw new InvalidOperationException($"Embedded slot '{slot.FieldId}' must be an object.");
            if (!boundary.TryGetPropertyValue("overrides", out var overrides)) return null;
            current = overrides as JsonObject
                ?? throw new InvalidOperationException($"Embedded slot '{slot.FieldId}' Overrides must be an object.");
        }
        return current;
    }
}
