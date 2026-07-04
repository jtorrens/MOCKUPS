namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryValueControlFactory
{
    public static IDictionaryValueControl Create(
        FieldDefinition definition,
        string value,
        DictionaryFieldServices services)
    {
        return definition.ValueKind switch
        {
            ValueKind.Boolean => new DictionaryBooleanControl(value, definition.IsEditable),
            ValueKind.OptionToken => new DictionaryOptionTokenControl(definition, value),
            ValueKind.PaletteColorToken => new DictionaryPaletteTokenControl(
                definition.Label,
                definition.Options,
                value,
                definition.IsEditable),
            ValueKind.ThemeToken => new DictionaryThemeTokenControl(definition, value, services.ShowThemeTokenPicker),
            ValueKind.PaletteColorPair => new DictionaryPalettePairControl(definition, value),
            ValueKind.HexColor => new DictionaryHexColorControl(definition, value),
            ValueKind.HueDegrees => new HueDegreesControl(value, definition.IsEditable),
            ValueKind.Integer => new DictionaryIntegerControl(definition, value),
            ValueKind.Decimal => new DictionaryDecimalControl(definition, value),
            ValueKind.IntegerPair => new DictionaryIntegerPairControl(definition, value),
            ValueKind.ImageFilePath => new DictionaryImageFileControl(definition, value, services.ResolveImagePath, services.GetFieldValue),
            ValueKind.IconSlots => new IconSlotsControl(
                value,
                definition.IsEditable,
                services.ShowIconTokenPicker,
                services.CreateIconPreview),
            _ => new DictionaryTextControl(definition, value),
        };
    }
}
