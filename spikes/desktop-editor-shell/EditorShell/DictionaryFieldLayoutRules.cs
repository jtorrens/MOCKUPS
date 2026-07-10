using Avalonia.Controls;
using Avalonia.Layout;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryFieldLayoutRules
{
    public static ColumnDefinitions Columns(ValueKind valueKind, bool compact = false)
    {
        return new ColumnDefinitions(compact ? "132,*,Auto" : "180,*,Auto");
    }

    public static double MinHeight(ValueKind valueKind)
    {
        return valueKind switch
        {
            ValueKind.StringMultiline => 96,
            ValueKind.IconSlots => 150,
            ValueKind.ComponentInputBindings => 46,
            _ => 40,
        };
    }

    public static VerticalAlignment LabelVerticalAlignment(ValueKind valueKind)
    {
        return valueKind is ValueKind.StringMultiline or ValueKind.IconSlots or ValueKind.AlignmentPlacement or ValueKind.TypographyStyle or ValueKind.ComponentInputBindings
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
    }

    public static Avalonia.Thickness LabelMargin(ValueKind valueKind)
    {
        return valueKind is ValueKind.StringMultiline or ValueKind.IconSlots or ValueKind.AlignmentPlacement or ValueKind.TypographyStyle or ValueKind.ComponentInputBindings
            ? new Avalonia.Thickness(0, 7, 0, 0)
            : new Avalonia.Thickness(0);
    }

    public static VerticalAlignment RestoreButtonVerticalAlignment(ValueKind valueKind)
    {
        return valueKind is ValueKind.StringMultiline or ValueKind.AlignmentPlacement or ValueKind.TypographyStyle or ValueKind.ComponentInputBindings
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
    }

    public static int RestoreButtonColumn(ValueKind valueKind) => 2;
}
