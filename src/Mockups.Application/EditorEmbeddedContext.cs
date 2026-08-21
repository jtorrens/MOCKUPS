using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record EditorEmbeddedContext(
    ProjectTreeNode OwnerNode,
    IReadOnlyList<EmbeddedComponentSlotDefinition> Slots,
    RuntimeComponentOverrideSource? RuntimeSource = null,
    RecordReferenceOverrideContext? RecordReferenceOverride = null)
{
    public static EditorEmbeddedContext ForRecordReferenceOverride(
        ProjectTreeNode ownerNode,
        ProjectTreeNode referenceNode,
        string referenceFieldId,
        string overrideDocumentFieldId) =>
        new(
            ownerNode,
            [],
            RecordReferenceOverride: new(
                referenceNode,
                referenceFieldId,
                overrideDocumentFieldId));

    public EmbeddedComponentSlotDefinition Slot => Slots.Count > 0
        ? Slots[^1]
        : throw new InvalidOperationException(
            "The root runtime component has no parent slot.");

    public string RecordClassId =>
        RecordReferenceOverride is not null
            ? RecordReferenceOverride.ReferenceNode.RecordClassId
            : RuntimeSource is not null && Slots.Count == 0
            ? RuntimeSource.RecordClassId
            : Slot.RecordClassId;

    public string ComponentType =>
        RecordReferenceOverride is not null
            ? throw new InvalidOperationException(
                "A RecordReference Overrides context has no Component type.")
            : RuntimeSource is not null && Slots.Count == 0
            ? RuntimeSource.ComponentType
            : Slot.EmbeddedComponentType;

    public bool IsRuntimeRoot => RuntimeSource is not null && Slots.Count == 0;

    public bool IsRecordReferenceOverride =>
        RecordReferenceOverride is not null;

    public bool IsNavigationRoot =>
        IsRecordReferenceOverride
        || IsRuntimeRoot
        || RuntimeSource is null && Slots.Count == 1;

    public EditorEmbeddedContext Nested(EmbeddedComponentSlotDefinition slot) =>
        RecordReferenceOverride is not null
            ? throw new InvalidOperationException(
                "A RecordReference Overrides context cannot contain Component slots.")
            : new(OwnerNode, [.. Slots, slot], RuntimeSource);

    public EditorEmbeddedContext Ancestor(int slotCount) =>
        RecordReferenceOverride is not null
            ? throw new InvalidOperationException(
                "A RecordReference Overrides context has no Component ancestors.")
            : new(
                OwnerNode,
                Slots.Take(slotCount).ToArray(),
                RuntimeSource);
}

public sealed record RecordReferenceOverrideContext(
    ProjectTreeNode ReferenceNode,
    string ReferenceFieldId,
    string OverrideDocumentFieldId);

public sealed record RuntimeComponentOverrideSource(
    string ProjectId,
    string VariantReference,
    string ComponentType,
    string RecordClassId,
    string BaseConfigJson,
    JsonObject Overrides,
    Func<JsonObject, Task> OverridesChanged);
