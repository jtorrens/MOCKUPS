using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record RecordCreationDefinition(
    string Id,
    string RecordClassId,
    string Title,
    string Description,
    string ActionLabel,
    IReadOnlyList<FieldValue> Fields,
    bool RequiresConfirmation = true)
{
    public string? ValidationError(IReadOnlyDictionary<string, string> values)
    {
        foreach (var field in Fields)
        {
            if (!values.TryGetValue(field.Definition.Id, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                return $"{field.Definition.DisplayLabel} is required.";
            }
            try
            {
                _ = RuntimeInputValueKindContract.ParseValue(
                    field.Definition.ValueKind,
                    value,
                    $"Creation field '{field.Definition.Id}'");
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
            if (field.Definition.Options is { Count: > 0 })
            {
                var selected = field.Definition.ValueKind is
                    ValueKind.PaletteColorPair or ValueKind.ThemeTokenPair
                        ? value.Split('|', StringSplitOptions.None)
                        : [value];
                if (selected.Any((item) => field.Definition.Options.All(
                        (option) => !option.Value.Equals(item, StringComparison.Ordinal))))
                {
                    return $"{field.Definition.DisplayLabel} contains an unavailable value.";
                }
            }
            if (field.Definition.Number is { } number
                && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                && (number.Minimum is { } minimum && parsed < minimum
                    || number.Maximum is { } maximum && parsed > maximum))
            {
                return $"{field.Definition.DisplayLabel} is outside its allowed range.";
            }
        }
        return null;
    }
}

public sealed record RecordCreationDraft(
    string DefinitionId,
    IReadOnlyDictionary<string, string> Values);
