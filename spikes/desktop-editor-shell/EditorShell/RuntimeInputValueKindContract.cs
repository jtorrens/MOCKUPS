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
        ValueKind.ComponentVariant => "componentVariant",
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

    public static ValueKind RequireCompatible(string kind, string valueKind, string owner)
    {
        if (!Enum.TryParse<ValueKind>(valueKind, ignoreCase: false, out var parsed)
            || !parsed.ToString().Equals(valueKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{owner} has unsupported or missing valueKind '{valueKind}'.");
        }

        var expectedKind = InputKind(parsed);
        if (!kind.Equals(expectedKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{owner} kind '{kind}' does not match valueKind '{valueKind}' (expected '{expectedKind}').");
        }
        return parsed;
    }
}
