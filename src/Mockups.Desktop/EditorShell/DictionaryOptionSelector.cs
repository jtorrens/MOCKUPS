using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryOptionSelector
{
    public static EditorInstantComboBox CreateComboBox(
        FieldDefinition definition,
        string value,
        bool allowIncompleteDraft = false)
    {
        var comboBox = new EditorInstantComboBox
        {
            MinHeight = 36,
            IsEnabled = definition.IsEditable,
        };
        SetValue(comboBox, definition, value, allowIncompleteDraft);
        return comboBox;
    }

    public static void SetValue(
        EditorInstantComboBox comboBox,
        FieldDefinition definition,
        string value,
        bool allowIncompleteDraft = false)
    {
        comboBox.ItemsSource = DisplayOptions(definition, allowIncompleteDraft);
        comboBox.SelectedItem = SelectedOption(
            definition,
            value,
            allowIncompleteDraft);
    }

    public static FieldOption? SelectedOption(
        FieldDefinition definition,
        string value,
        bool allowIncompleteDraft = false)
    {
        if (allowIncompleteDraft && string.IsNullOrWhiteSpace(value))
        {
            return DisplayOptions(definition, true)
                .First((option) => string.IsNullOrWhiteSpace(option.Value));
        }
        FieldOptionContract.ValidateValue(
            definition,
            value,
            $"Dictionary field '{definition.Id}'");
        return DisplayOptions(definition, allowIncompleteDraft)
            .FirstOrDefault((option) => option.Value == value);
    }

    public static string Value(EditorInstantComboBox comboBox)
    {
        return comboBox.SelectedItem is FieldOption option ? option.Value : "";
    }

    private static IReadOnlyList<FieldOption> DisplayOptions(
        FieldDefinition definition,
        bool allowIncompleteDraft = false)
    {
        var options = FieldOptionContract.RequireOptions(
            definition.Options,
            $"Dictionary field '{definition.Id}'");
        if (((definition.ValueKind == ValueKind.RecordReference
                && definition.RecordReference?.AllowEmpty == true)
             || allowIncompleteDraft)
            && !options.Any((option) => string.IsNullOrWhiteSpace(option.Value)))
        {
            return [new FieldOption("", $"Select {definition.Label.ToLowerInvariant()}…"), .. options];
        }
        return options;
    }
}
