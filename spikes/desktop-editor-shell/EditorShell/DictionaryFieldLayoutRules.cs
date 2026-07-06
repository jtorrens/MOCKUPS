using Avalonia.Controls;
using Avalonia.Layout;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryFieldLayoutRules
{
    public static ColumnDefinitions Columns(ValueKind valueKind)
    {
        return valueKind switch
        {
            ValueKind.HexColor => new ColumnDefinitions("180,*,Auto"),
            ValueKind.HueDegrees => new ColumnDefinitions("180,*,Auto"),
            ValueKind.IntegerPair => new ColumnDefinitions("180,*,Auto"),
            ValueKind.IconSlots => new ColumnDefinitions("180,*,Auto"),
            ValueKind.EmbeddedComponent => new ColumnDefinitions("180,*,Auto"),
            ValueKind.ComponentPreset => new ColumnDefinitions("180,*,Auto"),
            ValueKind.AlignmentPlacement => new ColumnDefinitions("180,*,Auto"),
            ValueKind.ThemeToken => new ColumnDefinitions("180,*,Auto"),
            ValueKind.OptionToken => new ColumnDefinitions("180,*,Auto"),
            ValueKind.PaletteColorToken => new ColumnDefinitions("180,*,Auto"),
            ValueKind.PaletteColorPair => new ColumnDefinitions("180,*,Auto"),
            _ => new ColumnDefinitions("180,*,Auto"),
        };
    }

    public static double MinHeight(ValueKind valueKind)
    {
        return valueKind switch
        {
            ValueKind.StringMultiline => 96,
            ValueKind.IconSlots => 150,
            _ => 40,
        };
    }

    public static VerticalAlignment LabelVerticalAlignment(ValueKind valueKind)
    {
        return valueKind is ValueKind.StringMultiline or ValueKind.IconSlots or ValueKind.AlignmentPlacement
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
    }

    public static Avalonia.Thickness LabelMargin(ValueKind valueKind)
    {
        return valueKind is ValueKind.StringMultiline or ValueKind.IconSlots or ValueKind.AlignmentPlacement
            ? new Avalonia.Thickness(0, 7, 0, 0)
            : new Avalonia.Thickness(0);
    }

    public static VerticalAlignment RestoreButtonVerticalAlignment(ValueKind valueKind)
    {
        return valueKind is ValueKind.StringMultiline or ValueKind.AlignmentPlacement
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
    }

    public static int RestoreButtonColumn(ValueKind valueKind)
    {
        return valueKind switch
        {
            ValueKind.HexColor => 2,
            _ => 2,
        };
    }
}
