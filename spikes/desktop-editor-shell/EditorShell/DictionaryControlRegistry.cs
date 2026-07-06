using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record DictionaryControlRequest(
    FieldDefinition Definition,
    string Value,
    DictionaryFieldServices Services,
    bool IsHighlighted);

internal static class DictionaryControlRegistry
{
    private static readonly IReadOnlyDictionary<ValueKind, Func<DictionaryControlRequest, IDictionaryValueControl>> Controls =
        new Dictionary<ValueKind, Func<DictionaryControlRequest, IDictionaryValueControl>>
        {
            [ValueKind.Boolean] = (request) => new DictionaryBooleanControl(request.Value, request.Definition.IsEditable),
            [ValueKind.OptionToken] = (request) => new DictionaryOptionTokenControl(request.Definition, request.Value),
            [ValueKind.PaletteColorToken] = (request) => new DictionaryPaletteTokenControl(
                request.Definition.Label,
                request.Definition.Options,
                request.Value,
                request.Definition.IsEditable),
            [ValueKind.IconToken] = (request) => new DictionaryIconTokenControl(
                request.Value,
                request.Definition.IsEditable,
                request.Services.ShowIconTokenPicker,
                request.Services.CreateIconPreview),
            [ValueKind.ThemeToken] = (request) => new DictionaryThemeTokenControl(
                request.Definition,
                request.Value,
                request.Services.ShowThemeTokenPicker),
            [ValueKind.Alpha] = (request) => new DictionaryAlphaControl(request.Value, request.Definition.IsEditable),
            [ValueKind.PaletteColorPair] = (request) => new DictionaryPalettePairControl(request.Definition, request.Value),
            [ValueKind.PaletteColorAlphaPair] = (request) => new DictionaryPaletteAlphaPairControl(request.Definition, request.Value),
            [ValueKind.HexColor] = (request) => new DictionaryHexColorControl(request.Definition, request.Value),
            [ValueKind.HueDegrees] = (request) => new HueDegreesControl(request.Value, request.Definition.IsEditable),
            [ValueKind.Integer] = CreateNumberControl,
            [ValueKind.Decimal] = CreateNumberControl,
            [ValueKind.IntegerPair] = (request) => new DictionaryIntegerPairControl(request.Definition, request.Value),
            [ValueKind.DirectoryPath] = (request) => new DictionaryPathControl(
                request.Definition,
                request.Value,
                request.Services.BrowsePath),
            [ValueKind.ImageFilePath] = (request) => new DictionaryImageFileControl(
                request.Definition,
                request.Value,
                request.Services.BrowsePath,
                request.Services.ResolveImagePath,
                request.Services.GetFieldValue),
            [ValueKind.IconSlots] = (request) => new IconSlotsControl(
                request.Value,
                request.Definition.IsEditable,
                request.Services.ShowIconTokenPicker,
                request.Services.CreateIconPreview),
            [ValueKind.ComponentPreset] = (request) => new DictionaryComponentPresetControl(
                request.Definition,
                request.Value,
                request.IsHighlighted,
                request.Services.OpenEmbeddedComponent),
            [ValueKind.EmbeddedComponent] = (request) => new DictionaryEmbeddedComponentControl(
                request.Definition,
                request.Value,
                request.IsHighlighted,
                request.Services.OpenEmbeddedComponent),
            [ValueKind.AlignmentPlacement] = (request) => new DictionaryAlignmentPlacementControl(request.Definition, request.Value),
        };

    public static IDictionaryValueControl Create(
        FieldDefinition definition,
        string value,
        DictionaryFieldServices services,
        bool isHighlighted = false)
    {
        var request = new DictionaryControlRequest(definition, value, services, isHighlighted);
        return Controls.TryGetValue(definition.ValueKind, out var factory)
            ? factory(request)
            : new DictionaryTextControl(definition, value);
    }

    private static IDictionaryValueControl CreateNumberControl(DictionaryControlRequest request)
    {
        return request.Definition.Number?.UseSlider == true
            ? new DictionaryNumberSliderControl(request.Definition, request.Value)
            : request.Definition.ValueKind == ValueKind.Integer
                ? new DictionaryIntegerControl(request.Definition, request.Value)
                : new DictionaryDecimalControl(request.Definition, request.Value);
    }
}
