using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorEmbeddedContext(
    ProjectTreeNode OwnerNode,
    IReadOnlyList<EmbeddedComponentSlotDefinition> Slots,
    RuntimeComponentOverrideSource? RuntimeSource = null)
{
    public EmbeddedComponentSlotDefinition Slot => Slots.Count > 0
        ? Slots[^1]
        : throw new InvalidOperationException("The root runtime component has no parent slot.");

    public string RecordClassId => RuntimeSource is not null && Slots.Count == 0
        ? RuntimeSource.RecordClassId
        : Slot.RecordClassId;

    public string ComponentType => RuntimeSource is not null && Slots.Count == 0
        ? RuntimeSource.ComponentType
        : Slot.EmbeddedComponentType;

    public bool IsRuntimeRoot => RuntimeSource is not null && Slots.Count == 0;

    public bool IsNavigationRoot => IsRuntimeRoot || RuntimeSource is null && Slots.Count == 1;

    public EditorEmbeddedContext Nested(EmbeddedComponentSlotDefinition slot) =>
        new(OwnerNode, [.. Slots, slot], RuntimeSource);

    public EditorEmbeddedContext Ancestor(int slotCount) =>
        new(OwnerNode, Slots.Take(slotCount).ToArray(), RuntimeSource);

    public string ActivePresetName(SpikeDatabase database) => RuntimeSource is null
        ? database.GetEmbeddedComponentPresetName(OwnerNode, Slots)
        : database.GetRuntimeComponentPresetName(RuntimeSource.PresetReference, RuntimeSource.Overrides, Slots);

    public FieldValue CreateFieldValue(SpikeDatabase database, string fieldId) => RuntimeSource is null
        ? database.CreateEmbeddedComponentFieldValue(OwnerNode, Slots, fieldId)
        : database.CreateRuntimeComponentOverrideFieldValue(
            RuntimeSource.ProjectId,
            RuntimeSource.BaseConfigJson,
            RuntimeSource.Overrides,
            Slots,
            fieldId);

    public void CommitFieldValue(SpikeDatabase database, string fieldId, string value)
    {
        if (RuntimeSource is null)
        {
            database.UpdateEmbeddedComponentField(OwnerNode, Slots, fieldId, value);
            return;
        }

        database.UpdateRuntimeComponentOverride(RuntimeSource.Overrides, Slots, fieldId, value);
        RuntimeSource.OverridesChanged(RuntimeSource.Overrides);
    }
}

internal sealed record RuntimeComponentOverrideSource(
    string ProjectId,
    string PresetReference,
    string ComponentType,
    string RecordClassId,
    string BaseConfigJson,
    JsonObject Overrides,
    Action<JsonObject> OverridesChanged);
