namespace Mockups.DesktopEditorShell.Common;

public sealed record DevicePreviewMetrics(
    string Name,
    double CanvasWidth,
    double CanvasHeight,
    double ScreenX,
    double ScreenY,
    double ScreenWidth,
    double ScreenHeight,
    double CornerRadius,
    double CornerRadiusCoefficient,
    double DesignSafeMarginCoefficient,
    double StatusBarHeight,
    double SafeAreaBottom,
    DeviceModuleTransparencyOverride ModuleTransparency);

public sealed record DeviceModuleTransparencyOverride(
    bool Enabled,
    string Mode,
    string PaletteColor,
    double BackgroundOpacity,
    double FixedStart,
    double GradientHeight,
    double VariableOffset)
{
    public static DeviceModuleTransparencyOverride Disabled { get; } = new(
        false,
        "fixed",
        "gray_000",
        1,
        0,
        1,
        0);
}
