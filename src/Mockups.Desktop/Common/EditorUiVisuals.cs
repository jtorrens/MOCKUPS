using Avalonia.Media;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorUiVisuals
{
    public const double PersistentSecondaryFontSize = 12;
    public static IBrush PrimaryTextBrush(bool isDark) =>
        Brush(isDark ? "#F1F5F9" : "#1F2937");

    public static IBrush SecondaryTextBrush(bool isDark) =>
        Brush(isDark ? "#C7D0DD" : "#596579");

    public static IBrush DisabledTextBrush(bool isDark) =>
        Brush(isDark ? "#9DA8B8" : "#6B7585");

    public static IBrush ConnectorBrush(bool isDark) =>
        ScrollbarSeparatorBrush(isDark);

    public static IBrush ScrollbarSeparatorBrush(bool isDark) =>
        Brush(isDark ? "#4D545E" : "#B7BDC5");

    public static IBrush FocusBrush(bool isDark) =>
        Brush(isDark ? "#66A3FF" : "#1667C5");

    public static IBrush HoverBackgroundBrush(bool isDark) =>
        Brush(isDark ? "#26303B" : "#E2E7ED");

    public static IBrush SelectedBackgroundBrush(bool isDark) =>
        Brush(isDark ? "#285F9E" : "#D7E9FF");

    public static IBrush SelectedTextBrush(bool isDark) =>
        Brush(isDark ? "#FFFFFF" : "#172033");

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
}
