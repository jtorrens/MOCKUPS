using Avalonia;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorUiDensity
{
    public static double TextScale { get; private set; } = 1;
    public static double CardPaddingScale { get; private set; } = 1;

    public static void Configure(double textScale, double cardPaddingScale)
    {
        TextScale = ClampScale(textScale, 0.5, 3);
        CardPaddingScale = ClampScale(cardPaddingScale, 0.1, 1.5);
    }

    public static double Card(double value)
    {
        return value * CardPaddingScale;
    }

    public static double TextAwareWidth(double value)
    {
        return value * System.Math.Max(1, TextScale);
    }

    public static Thickness CardThickness(double uniform)
    {
        var value = Card(uniform);
        return new Thickness(value);
    }

    public static Thickness CardThickness(double horizontal, double vertical)
    {
        return new Thickness(Card(horizontal), Card(vertical));
    }

    public static Thickness CardThickness(double left, double top, double right, double bottom)
    {
        return new Thickness(Card(left), Card(top), Card(right), Card(bottom));
    }

    private static double ClampScale(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 1;
        }

        return System.Math.Clamp(value, min, max);
    }
}
