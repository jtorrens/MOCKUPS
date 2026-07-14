using Mockups.DesktopEditorShell.Data;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RuntimeInputFieldDefinitionFactory
{
    public static FieldDefinition Create(
        SpikeDatabase database,
        ProjectTreeNode node,
        ComponentInputDefinition input)
    {
        var projectId = ProjectAncestor(node).Id;
        var options = input.ValueKind switch
        {
            ValueKind.RecordReference when input.TableId == "actors" => database.GetActorOptions(projectId),
            ValueKind.ComponentPreset when !string.IsNullOrWhiteSpace(input.ComponentType) =>
                database.GetComponentPresetReferenceOptions(projectId, input.ComponentType),
            ValueKind.PaletteColorToken => database.GetPaletteColorOptions(projectId),
            _ => input.Options,
        };
        return new FieldDefinition(
            input.Id,
            input.Label,
            input.ValueKind,
            DefaultValue: input.DefaultValue,
            Options: options,
            PairLabels: input.ValueKind == ValueKind.IntegerPair ? input.PairLabels : null,
            Number: input.ValueKind is ValueKind.Decimal or ValueKind.Integer or ValueKind.Alpha
                ? new NumberDefinition(input.Minimum, input.Maximum, input.Increment, input.ValueKind == ValueKind.Integer ? 0 : 2)
                : null,
            RecordReference: input.ValueKind == ValueKind.RecordReference
                ? new RecordReferenceDefinition(input.TableId)
                : null,
            SelectComponentClass: input.ValueKind == ValueKind.ComponentPreset
                && input.ComponentType.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains("*", StringComparer.Ordinal),
            Unit: input.Unit,
            Animation: input.Animation,
            BehaviorTiming: input.BehaviorTiming);
    }

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
            current = current.Parent ?? throw new InvalidOperationException($"{node.Kind} has no project ancestor.");
        return current;
    }
}
