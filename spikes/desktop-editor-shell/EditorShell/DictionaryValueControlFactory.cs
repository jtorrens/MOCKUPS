using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryValueControlFactory
{
    public static IDictionaryValueControl Create(
        FieldDefinition definition,
        string value,
        Func<string, bool, Task<string?>>? showIconTokenPicker,
        Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? showThemeTokenPicker,
        Func<string, Control>? createIconPreview)
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
            ValueKind.ThemeToken => new DictionaryThemeTokenControl(definition, value, showThemeTokenPicker),
            ValueKind.PaletteColorPair => new DictionaryPalettePairControl(definition, value),
            ValueKind.HexColor => new DictionaryHexColorControl(definition, value),
            ValueKind.HueDegrees => new HueDegreesControl(value, definition.IsEditable),
            ValueKind.IntegerPair => new DictionaryIntegerPairControl(definition, value),
            ValueKind.IconSlots => new IconSlotsControl(
                value,
                definition.IsEditable,
                showIconTokenPicker,
                createIconPreview),
            _ => new DictionaryTextControl(definition, value),
        };
    }
}
