using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell.VisualIr;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(VisualIrNonePaint), "none")]
[JsonDerivedType(typeof(VisualIrSolidPaint), "solid")]
[JsonDerivedType(typeof(VisualIrLinearGradientPaint), "linearGradient")]
[JsonDerivedType(typeof(VisualIrRadialGradientPaint), "radialGradient")]
internal abstract record VisualIrPaint;

internal sealed record VisualIrNonePaint : VisualIrPaint;

internal sealed record VisualIrSolidPaint(VisualIrColor Color) : VisualIrPaint;

internal sealed record VisualIrLinearGradientPaint(
    VisualIrPoint From,
    VisualIrPoint To,
    IReadOnlyList<VisualIrGradientStop> Stops) : VisualIrPaint;

internal sealed record VisualIrRadialGradientPaint(
    VisualIrPoint Center,
    double Radius,
    IReadOnlyList<VisualIrGradientStop> Stops) : VisualIrPaint;

internal sealed record VisualIrGradientStop(double Offset, VisualIrColor Color);

internal sealed record VisualIrStroke(
    VisualIrPaint Paint,
    double Width,
    string? Alignment = null,
    string? LineCap = null,
    string? LineJoin = null,
    double? MiterLimit = null,
    IReadOnlyList<double>? Dash = null,
    double? DashOffset = null);

internal sealed record VisualIrShadowEffect(
    double X,
    double Y,
    double Blur,
    VisualIrColor Color,
    bool Inset = false);

