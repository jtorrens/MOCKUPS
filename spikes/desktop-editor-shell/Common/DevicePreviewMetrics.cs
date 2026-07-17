namespace Mockups.DesktopEditorShell.Common;

internal sealed record DevicePreviewMetrics(
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
    double ScaleToPixels);
