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
        comboBox.ItemsSource = DisplayOptions(definition);
        comboBox.SelectedItem = SelectedOption(definition, value);
    }

    public static FieldOption? SelectedOption(FieldDefinition definition, string value)
    {
        FieldOptionContract.ValidateValue(
            definition,
            value,
            $"Dictionary field '{definition.Id}'");
        return DisplayOptions(definition).FirstOrDefault((option) => option.Value == value);
    }

    public static string Value(EditorInstantComboBox comboBox)
    {
        return comboBox.SelectedItem is FieldOption option ? option.Value : "";
    }

    private static IReadOnlyList<FieldOption> DisplayOptions(FieldDefinition definition)
    {
        var options = FieldOptionContract.RequireOptions(
            definition.Options,
            $"Dictionary field '{definition.Id}'");
        if (definition.ValueKind == ValueKind.RecordReference
            && definition.RecordReference?.AllowEmpty == true
            && !options.Any((option) => string.IsNullOrWhiteSpace(option.Value)))
        {
            return [new FieldOption("", $"Select {definition.Label.ToLowerInvariant()}…"), .. options];
        }
        return options;
    }
}
