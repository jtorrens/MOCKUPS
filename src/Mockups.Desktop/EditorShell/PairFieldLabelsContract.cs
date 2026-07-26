using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class PairFieldLabelsContract
{
    public static bool IsPair(ValueKind valueKind)
    {
        return valueKind is ValueKind.IntegerPair
            or ValueKind.ThemeTokenPair
            or ValueKind.PaletteColorPair
            or ValueKind.PaletteColorAlphaPair;
    }

    public static PairFieldLabels? ForField(
        ValueKind valueKind,
        PairFieldLabels? labels,
        string owner)
    {
        if (!IsPair(valueKind))
        {
            return null;
        }

        return Require(labels, owner);
    }

    public static PairFieldLabels Require(PairFieldLabels? labels, string owner)
    {
        if (labels is null
            || string.IsNullOrWhiteSpace(labels.First)
            || string.IsNullOrWhiteSpace(labels.Second))
        {
            throw new InvalidOperationException($"{owner} requires explicit non-empty pair labels.");
        }

        return labels;
    }
}
