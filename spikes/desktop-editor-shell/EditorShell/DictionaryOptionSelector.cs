using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryOptionSelector
{
    public static EditorInstantComboBox CreateComboBox(FieldDefinition definition, string value)
    {
        var comboBox = new EditorInstantComboBox
        {
            MinHeight = 36,
            MinWidth = 220,
            IsEnabled = definition.IsEditable,
            ItemsSource = definition.Options ?? [],
        };
        comboBox.SelectedItem = SelectedOption(definition, value);
        return comboBox;
    }

    public static FieldOption? SelectedOption(FieldDefinition definition, string value)
    {
        return (definition.Options ?? []).FirstOrDefault((option) => option.Value == value)
            ?? (definition.Options ?? []).FirstOrDefault();
    }

    public static string Value(EditorInstantComboBox comboBox)
    {
        return comboBox.SelectedItem is FieldOption option ? option.Value : "";
    }
}
