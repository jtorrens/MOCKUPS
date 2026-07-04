using System;

namespace Mockups.DesktopEditorShell.VisualIr;

internal sealed record VisualIrPoint(double X, double Y);

internal sealed record VisualIrRect(double X, double Y, double Width, double Height)
{
    public bool IsValid()
    {
        return IsFinite(X) && IsFinite(Y) && IsFinite(Width) && IsFinite(Height) && Width >= 0 && Height >= 0;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

internal sealed record VisualIrViewport(double Width, double Height, double Scale = 1)
{
    public bool IsValid()
    {
        return IsFinite(Width) && IsFinite(Height) && IsFinite(Scale) && Width > 0 && Height > 0 && Scale > 0;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

