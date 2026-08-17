using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class FieldOptionContract
{
    public static IReadOnlyList<FieldOption> RequireOptions(
        IReadOnlyList<FieldOption>? options,
        string owner)
    {
        if (options is null || options.Count == 0)
        {
            throw new InvalidOperationException($"{owner} requires declared options.");
        }

        var duplicateValues = options
            .GroupBy((option) => option.Value, StringComparer.Ordinal)
            .Where((group) => group.Count() > 1)
            .Select((group) => group.Key)
            .OrderBy((value) => value, StringComparer.Ordinal)
            .ToList();
        if (duplicateValues.Count > 0)
        {
            throw new InvalidOperationException(
                $"{owner} has duplicate option values: {string.Join(", ", duplicateValues)}.");
        }

        return options;
    }

    public static void ValidateValue(
        FieldDefinition definition,
        string value,
        string owner)
    {
        if (definition.CanInherit
            && value.Equals(
                definition.InheritedStorageValue,
                StringComparison.Ordinal))
        {
            return;
        }

        if (!UsesDeclaredOptions(definition.ValueKind))
        {
            return;
        }

        if (definition.ValueKind == ValueKind.RecordReference
            && string.IsNullOrEmpty(value)
            && definition.RecordReference?.AllowEmpty == true)
        {
            return;
        }

        ValidateValue(
            RequireOptions(definition.Options, owner),
            value,
            owner);
    }

    public static void ValidateValue(
        IReadOnlyList<FieldOption> options,
        string value,
        string owner)
    {
        _ = RequireOptions(options, owner);
        if (!options.Any((option) => option.Value.Equals(value, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{owner} value '{value}' is not one of its declared options.");
        }
    }

    public static bool UsesDeclaredOptions(ValueKind valueKind) => valueKind is
        ValueKind.OptionToken
        or ValueKind.RecordReference;
}
