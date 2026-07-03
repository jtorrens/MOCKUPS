using Avalonia.Controls;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryOptionSelector
{
    public static ComboBox CreateComboBox(FieldDefinition definition, string value)
    {
        var comboBox = new ComboBox
        {
            MinHeight = 36,
            MinWidth = 220,
            IsEnabled = definition.IsEditable,
            ItemsSource = definition.Options ?? [],
            ItemTemplate = definition.ValueKind is ValueKind.PaletteColorToken or ValueKind.PaletteColorPair
                ? DictionaryPaletteOptionTemplate.Create()
                : null,
        };
        EditorComboBoxBehavior.Configure(comboBox);
        comboBox.SelectedItem = SelectedOption(definition, value);
        return comboBox;
    }

    public static FieldOption? SelectedOption(FieldDefinition definition, string value)
    {
        return (definition.Options ?? []).FirstOrDefault((option) => option.Value == value)
            ?? (definition.Options ?? []).FirstOrDefault();
    }

    public static string Value(ComboBox comboBox)
    {
        return comboBox.SelectedItem is FieldOption option ? option.Value : "";
    }
}
