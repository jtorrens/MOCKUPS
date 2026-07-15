using Avalonia.Controls;
using Avalonia.Layout;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryFieldLayoutRules
{
    public const double MinimumLabelWidth = 72;

    public static double MaximumLabelWidth(bool compact) => compact ? 132 : 180;

    public static double ResponsiveLabelWidth(double availableWidth, bool compact)
    {
        if (availableWidth <= 0 || double.IsInfinity(availableWidth))
        {
            return MaximumLabelWidth(compact);
        }

        return Math.Clamp(
            availableWidth * (compact ? 0.30 : 0.34),
            MinimumLabelWidth,
            MaximumLabelWidth(compact));
    }

    public static bool UsesStackedActions(
        double availableWidth,
        double contentMinimumWidth,
        double actionsMinimumWidth,
        int columnGapCount,
        double columnSpacing)
    {
        return availableWidth > 0
            && !double.IsInfinity(availableWidth)
            && availableWidth < contentMinimumWidth
                + actionsMinimumWidth
                + (columnGapCount * columnSpacing);
    }

    public static ColumnDefinitions Columns(ValueKind valueKind, bool compact = false)
    {
        return new ColumnDefinitions($"{MaximumLabelWidth(compact)},*,Auto");
    }

    public static bool UsesBlockLayout(ValueKind valueKind) =>
        valueKind is ValueKind.ComponentInputBindings or ValueKind.StructuredCollection;

    public static double MinHeight(ValueKind valueKind)
    {
        return valueKind switch
        {
            ValueKind.StringMultiline => 96,
            ValueKind.IconSlots => 150,
            ValueKind.ComponentInputBindings => 46,
            ValueKind.StructuredCollection => 80,
            ValueKind.BehaviorTiming => 124,
            _ => 40,
        };
    }

    public static VerticalAlignment LabelVerticalAlignment(ValueKind valueKind)
    {
        return valueKind is ValueKind.StringMultiline or ValueKind.IconSlots or ValueKind.AlignmentPlacement or ValueKind.TypographyStyle or ValueKind.TypographySystemStyle or ValueKind.ComponentInputBindings or ValueKind.StructuredCollection or ValueKind.BehaviorTiming
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
    }

    public static Avalonia.Thickness LabelMargin(ValueKind valueKind)
    {
        return valueKind is ValueKind.StringMultiline or ValueKind.IconSlots or ValueKind.AlignmentPlacement or ValueKind.TypographyStyle or ValueKind.TypographySystemStyle or ValueKind.ComponentInputBindings or ValueKind.StructuredCollection or ValueKind.BehaviorTiming
            ? new Avalonia.Thickness(0, 7, 0, 0)
            : new Avalonia.Thickness(0);
    }

    public static VerticalAlignment RestoreButtonVerticalAlignment(ValueKind valueKind)
    {
        return valueKind is ValueKind.StringMultiline or ValueKind.AlignmentPlacement or ValueKind.TypographyStyle or ValueKind.TypographySystemStyle or ValueKind.ComponentInputBindings or ValueKind.StructuredCollection or ValueKind.BehaviorTiming
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
    }

    public static int RestoreButtonColumn(ValueKind valueKind) => 2;
}
