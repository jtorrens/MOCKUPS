using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorForwardVisuals
{
    public const double IndicatorSize = 10;
    public const double ActionSize = 30;
    public const string IndicatorGeometry = "M 1,1 L 9,5 L 1,9 Z";
    public const string InactiveAccessibleName = "Expose to parent runtime";
    public const string ActiveAccessibleName = "Keep as Variant value";

    public static string AccessibleName(bool isForwarded) =>
        isForwarded ? ActiveAccessibleName : InactiveAccessibleName;

    public static Button CreateActionButton(bool isForwarded)
    {
        var indicator = new Path
        {
            Width = IndicatorSize,
            Height = IndicatorSize,
            Data = Geometry.Parse(IndicatorGeometry),
            Fill = isForwarded ? EditorOverrideVisuals.Brush : Brushes.Transparent,
            Stroke = isForwarded ? EditorOverrideVisuals.Brush : null,
            StrokeThickness = 1.5,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var button = new Button
        {
            Content = indicator,
            Width = ActionSize,
            Height = ActionSize,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (!isForwarded)
        {
            indicator.Bind(Path.StrokeProperty, button.GetObservable(Button.ForegroundProperty));
        }

        return EditorAccessibility.Describe(
            button,
            AccessibleName(isForwarded));
    }
}
