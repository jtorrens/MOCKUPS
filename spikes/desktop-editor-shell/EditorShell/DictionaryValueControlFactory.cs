namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryValueControlFactory
{
    public static IDictionaryValueControl Create(
        FieldDefinition definition,
        string value,
        DictionaryFieldServices services,
        bool isHighlighted = false)
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
            ValueKind.IconToken => new DictionaryIconTokenControl(
                value,
                definition.IsEditable,
                services.ShowIconTokenPicker,
                services.CreateIconPreview),
            ValueKind.ThemeToken => new DictionaryThemeTokenControl(definition, value, services.ShowThemeTokenPicker),
            ValueKind.Alpha => new DictionaryAlphaControl(value, definition.IsEditable),
            ValueKind.PaletteColorPair => new DictionaryPalettePairControl(definition, value),
            ValueKind.PaletteColorAlphaPair => new DictionaryPaletteAlphaPairControl(definition, value),
            ValueKind.HexColor => new DictionaryHexColorControl(definition, value),
            ValueKind.HueDegrees => new HueDegreesControl(value, definition.IsEditable),
            ValueKind.Integer when definition.Number?.UseSlider == true => new DictionaryNumberSliderControl(definition, value),
            ValueKind.Decimal when definition.Number?.UseSlider == true => new DictionaryNumberSliderControl(definition, value),
            ValueKind.Integer => new DictionaryIntegerControl(definition, value),
            ValueKind.Decimal => new DictionaryDecimalControl(definition, value),
            ValueKind.IntegerPair => new DictionaryIntegerPairControl(definition, value),
            ValueKind.ImageFilePath => new DictionaryImageFileControl(definition, value, services.ResolveImagePath, services.GetFieldValue),
            ValueKind.IconSlots => new IconSlotsControl(
                value,
                definition.IsEditable,
                services.ShowIconTokenPicker,
                services.CreateIconPreview),
            ValueKind.EmbeddedComponent => new DictionaryEmbeddedComponentControl(definition, value, isHighlighted, services.OpenEmbeddedComponent),
            _ => new DictionaryTextControl(definition, value),
        };
    }
}
