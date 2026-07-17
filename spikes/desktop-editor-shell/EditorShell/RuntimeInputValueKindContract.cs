using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RuntimeInputValueKindContract
{
    public static string InputKind(ValueKind valueKind) => valueKind switch
    {
        ValueKind.Integer or ValueKind.Decimal or ValueKind.HueDegrees or ValueKind.Alpha => "number",
        ValueKind.IntegerPair => "integerPair",
        ValueKind.Boolean => "boolean",
        ValueKind.OptionToken => "option",
        ValueKind.RecordReference => "recordReference",
        ValueKind.ComponentPreset => "componentPreset",
        ValueKind.ThemeToken => "themeToken",
        ValueKind.IconToken => "icon",
        ValueKind.IconTokenList or ValueKind.IconSlots => "iconList",
        ValueKind.StringMultiline => "multilineText",
        ValueKind.MediaFilePath => "mediaFilePath",
        ValueKind.StructuredCollection => "collection",
        ValueKind.BehaviorTiming => "behaviorTiming",
        ValueKind.StringSingleLine
            or ValueKind.StringReadOnly
            or ValueKind.DirectoryPath
            or ValueKind.ImageFilePath
            or ValueKind.ThemeTokenPair
            or ValueKind.TypographyStyle
            or ValueKind.TypographySystemStyle
            or ValueKind.HexColor
            or ValueKind.PaletteColorToken
            or ValueKind.PaletteColorPair
            or ValueKind.PaletteColorAlphaPair
            or ValueKind.EmbeddedComponent
            or ValueKind.ComponentInputBindings
            or ValueKind.AlignmentPlacement
            or ValueKind.Motion
            or ValueKind.MotionTiming => "text",
        _ => throw new InvalidOperationException($"Runtime input kind is not declared for dictionary value kind '{valueKind}'."),
    };

    public static ValueKind DefaultValueKind(string kind) => kind.Trim() switch
    {
        "text" => ValueKind.StringSingleLine,
        "number" => ValueKind.Decimal,
        "integerPair" => ValueKind.IntegerPair,
        "mediaFilePath" => ValueKind.MediaFilePath,
        "boolean" => ValueKind.Boolean,
        "option" => ValueKind.OptionToken,
        "recordReference" => ValueKind.RecordReference,
        "componentPreset" => ValueKind.ComponentPreset,
        "themeToken" => ValueKind.ThemeToken,
        "icon" => ValueKind.IconToken,
        "iconList" => ValueKind.IconTokenList,
        "multilineText" => ValueKind.StringMultiline,
        "collection" => ValueKind.StructuredCollection,
        "behaviorTiming" => ValueKind.BehaviorTiming,
        _ => throw new InvalidOperationException($"Runtime input kind '{kind}' has no declared dictionary value kind."),
    };
}
