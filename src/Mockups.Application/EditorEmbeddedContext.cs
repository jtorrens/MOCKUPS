using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record EditorEmbeddedContext(
    ProjectTreeNode OwnerNode,
    IReadOnlyList<EmbeddedComponentSlotDefinition> Slots,
    RuntimeComponentOverrideSource? RuntimeSource = null)
{
    public EmbeddedComponentSlotDefinition Slot => Slots.Count > 0
        ? Slots[^1]
        : throw new InvalidOperationException(
            "The root runtime component has no parent slot.");

    public string RecordClassId =>
        RuntimeSource is not null && Slots.Count == 0
            ? RuntimeSource.RecordClassId
            : Slot.RecordClassId;

    public string ComponentType =>
        RuntimeSource is not null && Slots.Count == 0
            ? RuntimeSource.ComponentType
            : Slot.EmbeddedComponentType;

    public bool IsRuntimeRoot => RuntimeSource is not null && Slots.Count == 0;

    public bool IsNavigationRoot =>
        IsRuntimeRoot || RuntimeSource is null && Slots.Count == 1;

    public EditorEmbeddedContext Nested(EmbeddedComponentSlotDefinition slot) =>
        new(OwnerNode, [.. Slots, slot], RuntimeSource);

    public EditorEmbeddedContext Ancestor(int slotCount) =>
        new(OwnerNode, Slots.Take(slotCount).ToArray(), RuntimeSource);
}

public sealed record RuntimeComponentOverrideSource(
    string ProjectId,
    string VariantReference,
    string ComponentType,
    string RecordClassId,
    string BaseConfigJson,
    JsonObject Overrides,
    Func<JsonObject, Task> OverridesChanged);
