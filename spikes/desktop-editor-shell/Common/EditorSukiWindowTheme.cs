using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using SukiUI.Controls;
using SukiUI.Enums;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorSukiWindowTheme
{
    private static SukiColor s_accentColor = SukiColor.Blue;

    public static void SetAccentColor(SukiColor accentColor)
    {
        s_accentColor = accentColor;
    }

    public static IBrush NeutralBackgroundBrush(bool isDark)
    {
        return new SolidColorBrush(isDark ? Color.Parse("#181A1F") : Color.Parse("#ECEDEF"));
    }

    public static IBrush AccentBrush()
    {
        return new SolidColorBrush(AccentColor());
    }

    public static IBrush AccentBrush(byte alpha)
    {
        var color = AccentColor();
        return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
    }

    public static IBrush SelectionBackgroundBrush(bool isDark)
    {
        return AccentBrush(isDark ? (byte)0x38 : (byte)0x24);
    }

    public static Color AccentColor()
    {
        return s_accentColor switch
        {
            SukiColor.Red => Color.Parse("#E64A3C"),
            SukiColor.Green => Color.Parse("#31A66A"),
            SukiColor.Orange => Color.Parse("#D98928"),
            _ => Color.Parse("#3388FF"),
        };
    }

    public static bool IsDark(Window? owner)
    {
        var variant = owner?.ActualThemeVariant
            ?? Application.Current?.ActualThemeVariant
            ?? Application.Current?.RequestedThemeVariant
            ?? ThemeVariant.Dark;
        return variant == ThemeVariant.Dark;
    }

    public static void ApplyNeutralBackground(Window window, bool isDark)
    {
        window.Background = NeutralBackgroundBrush(isDark);
    }

    public static void ApplyNeutralBackground(Window window, Border rootShell, bool isDark)
    {
        var brush = NeutralBackgroundBrush(isDark);
        window.Background = brush;
        rootShell.Background = brush;
    }

    public static void ApplyDialogChrome(SukiWindow dialog, Window? owner = null)
    {
        dialog.BackgroundStyle = SukiBackgroundStyle.Flat;
        dialog.BackgroundAnimationEnabled = false;
        dialog.BackgroundTransitionsEnabled = false;
        dialog.BackgroundTransitionTime = 0.05;
        ApplyNeutralBackground(dialog, IsDark(owner));
    }
}
