using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record FixedComponentVariantBoundary(
    string ComponentClassId,
    string DefaultVariantReference,
    IReadOnlyList<FieldOption> VariantOptions);

internal static class ComponentVariantOptionContract
{
    public static bool SelectsComponentClass(string componentTypeSelector) =>
        componentTypeSelector
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("*", StringComparer.Ordinal);

    public static FixedComponentVariantBoundary RequireFixedBoundary(
        IReadOnlyList<FieldOption> options,
        string owner)
    {
        var references = options
            .Where((option) => !string.IsNullOrWhiteSpace(option.Value))
            .ToList();
        if (references.Count == 0)
        {
            throw new InvalidOperationException($"{owner} has no Component Variants.");
        }
        if (references.Any((option) => string.IsNullOrWhiteSpace(option.GroupValue)))
        {
            throw new InvalidOperationException(
                $"{owner} requires every Component Variant option to declare its exact Component Class id.");
        }

        var componentClassIds = references
            .Select((option) => option.GroupValue)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (componentClassIds.Count != 1)
        {
            throw new InvalidOperationException(
                $"{owner} requires one exact Component Class, but found {componentClassIds.Count}.");
        }

        var componentClassId = componentClassIds[0];
        foreach (var option in references)
        {
            if (!VariantReferenceId.TryParse(option.Value, out var referencedClassId, out _)
                || !referencedClassId.Equals(componentClassId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{owner} contains Component Variant '{option.Value}' outside its declared Component Class '{componentClassId}'.");
            }
        }

        return new FixedComponentVariantBoundary(
            componentClassId,
            RequiredDefaultReference(references, componentClassId, owner),
            references);
    }

    public static string RequiredDefaultReference(
        IReadOnlyList<FieldOption> options,
        string componentClassId,
        string owner)
    {
        if (string.IsNullOrWhiteSpace(componentClassId))
        {
            throw new InvalidOperationException($"{owner} requires an exact Component Class id.");
        }

        var expected = VariantReferenceId.Format(componentClassId, VariantEnvelopeContract.DefaultId);
        return options.SingleOrDefault((option) =>
                   option.GroupValue.Equals(componentClassId, StringComparison.Ordinal)
                   && option.Value.Equals(expected, StringComparison.Ordinal))?.Value
               ?? throw new InvalidOperationException(
                   $"{owner} Component Class '{componentClassId}' has no explicit Default Variant.");
    }
}
