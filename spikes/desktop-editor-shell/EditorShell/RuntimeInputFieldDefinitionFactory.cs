using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RuntimeInputFieldDefinitionFactory
{
    public static FieldDefinition Create(
        RuntimeInputOptionsDataSource optionsDataSource,
        ProjectTreeNode node,
        ComponentInputDefinition input,
        bool? allowEmpty = null)
    {
        var projectId = ProjectAncestor(node).Id;
        var permitsEmpty = allowEmpty ?? input.AllowEmpty;
        var options = input.ValueKind switch
        {
            ValueKind.RecordReference when input.TableId == "actors" => optionsDataSource.ActorOptions(projectId, permitsEmpty),
            ValueKind.ComponentVariant when !string.IsNullOrWhiteSpace(input.ComponentType) =>
                optionsDataSource.ComponentVariantOptions(projectId, input.ComponentType, permitsEmpty),
            ValueKind.PaletteColorToken => optionsDataSource.PaletteColorOptions(projectId),
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
            SelectComponentClass: input.ValueKind == ValueKind.ComponentVariant
                && input.ComponentType.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains("*", StringComparer.Ordinal),
            StructuredCollection: input.StructuredCollection,
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
