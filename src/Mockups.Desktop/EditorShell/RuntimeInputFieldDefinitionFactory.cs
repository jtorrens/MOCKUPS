using System;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RuntimeInputFieldDefinitionFactory
{
    public static FieldDefinition Create(
        IRuntimeInputOptionsDataSource optionsDataSource,
        ProjectTreeNode node,
        ComponentInputDefinition input,
        bool? allowEmpty = null,
        JsonObject? values = null)
    {
        var projectId = ProjectAncestor(node).Id;
        var permitsEmpty = allowEmpty ?? input.AllowEmpty;
        var isDesignTestValue = node.Kind is
            ProjectTreeNodeKind.ComponentClass
            or ProjectTreeNodeKind.ComponentVariant
            or ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.ModuleVariant;
        var valueKind = input.ValueKind is ValueKind.MediaFilePath or ValueKind.MediaDirectoryPath
            && isDesignTestValue
                ? ValueKind.OptionToken
                : input.ValueKind;
        var options = input.ValueKind switch
        {
            ValueKind.RecordReference => optionsDataSource.RecordReferenceOptions(
                projectId,
                input.TableId,
                permitsEmpty,
                isDesignTestValue),
            ValueKind.MediaFilePath when isDesignTestValue =>
                SystemPreviewFixtureCatalog.MediaOptions(permitsEmpty),
            ValueKind.MediaDirectoryPath when isDesignTestValue =>
                SystemPreviewFixtureCatalog.MediaDirectoryOptions(permitsEmpty),
            ValueKind.ComponentVariant or ValueKind.ComponentVariantSlot
                when !string.IsNullOrWhiteSpace(input.ComponentType) =>
                optionsDataSource.ComponentVariantOptions(projectId, input.ComponentType, permitsEmpty),
            ValueKind.PaletteColorToken => optionsDataSource.PaletteColorOptions(projectId),
            _ => string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey)
                ? input.Options
                : RuntimeInputDynamicOptions.Resolve(optionsDataSource, input,
                    values ?? throw new InvalidOperationException($"Runtime field '{input.Id}' requires its exact option-source owner.")),
        };
        return new FieldDefinition(
            input.Id,
            input.Label,
            valueKind,
            DefaultValue: input.DefaultValue,
            Options: options,
            PairLabels: PairFieldLabelsContract.ForField(
                input.ValueKind,
                input.PairLabels,
                $"Runtime Input '{input.Id}'"),
            Number: input.ValueKind is ValueKind.Decimal or ValueKind.Integer or ValueKind.Alpha
                ? input.Number
                : null,
            RecordReference: input.ValueKind == ValueKind.RecordReference
                ? new RecordReferenceDefinition(input.TableId, AllowEmpty: permitsEmpty)
                : null,
            SelectComponentClass: input.ValueKind is ValueKind.ComponentVariant or ValueKind.ComponentVariantSlot
                && ComponentVariantOptionContract.SelectsComponentClass(input.ComponentType),
            StructuredCollection: input.StructuredCollection,
            Unit: input.Unit,
            Animation: input.Animation,
            BehaviorTiming: input.BehaviorTiming,
            HelpText: input.HelpText,
            ValuePattern: input.ValuePattern,
            ValuePatternMessage: input.ValuePatternMessage,
            AllowEmpty: permitsEmpty);
    }

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
            current = current.Parent ?? throw new InvalidOperationException($"{node.Kind} has no project ancestor.");
        return current;
    }
}
