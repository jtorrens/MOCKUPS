using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryOptionSelector
{
    public static EditorInstantComboBox CreateComboBox(FieldDefinition definition, string value)
    {
        var comboBox = new EditorInstantComboBox
        {
            MinHeight = 36,
            IsEnabled = definition.IsEditable,
        };
        SetValue(comboBox, definition, value);
        return comboBox;
    }

    public static void SetValue(EditorInstantComboBox comboBox, FieldDefinition definition, string value)
    {
        comboBox.ItemsSource = OptionsWithValue(definition, value);
        comboBox.SelectedItem = SelectedOption(definition, value);
    }

    public static FieldOption? SelectedOption(FieldDefinition definition, string value)
    {
        return OptionsWithValue(definition, value).FirstOrDefault((option) => option.Value == value)
            ?? OptionsWithValue(definition, value).FirstOrDefault();
    }

    public static string Value(EditorInstantComboBox comboBox)
    {
        return comboBox.SelectedItem is FieldOption option ? option.Value : "";
    }

    private static IReadOnlyList<FieldOption> OptionsWithValue(FieldDefinition definition, string value)
    {
        var options = definition.Options ?? [];
        if (definition.ValueKind == ValueKind.RecordReference
            && string.IsNullOrWhiteSpace(value)
            && !options.Any((option) => string.IsNullOrWhiteSpace(option.Value)))
        {
            return [new FieldOption("", $"Select {definition.Label.ToLowerInvariant()}…"), .. options];
        }
        if (string.IsNullOrWhiteSpace(value) || options.Any((option) => option.Value == value))
        {
            return options;
        }

        return [.. options, new FieldOption(value, value)];
    }
}
