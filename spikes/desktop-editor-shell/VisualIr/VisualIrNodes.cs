using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell.VisualIr;

internal sealed record VisualIrDocument
{
    public string Kind { get; init; } = "visual-ir.document";
    public int SchemaVersion { get; init; } = 1;
    public required VisualIrViewport Viewport { get; init; }
    public required VisualIrGroupNode Root { get; init; }
    public VisualIrResources? Resources { get; init; }
    public int? Frame { get; init; }
    public double? Fps { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

internal sealed record VisualIrResources
{
    public IReadOnlyList<string>? ColorVariants { get; init; }
    public string? DefaultColorVariant { get; init; }
    public IReadOnlyDictionary<string, VisualIrAsset>? Assets { get; init; }
    public IReadOnlyList<VisualIrFontFace>? Fonts { get; init; }
}

internal sealed record VisualIrAsset(string Id, VisualIrSource Source, string? MimeType = null);

internal sealed record VisualIrFontFace(
    string Family,
    VisualIrSource Source,
    int? Weight = null,
    string? Style = null);

internal sealed record VisualIrSource(string Kind, string? AssetId = null, string? Uri = null);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(VisualIrGroupNode), "group")]
[JsonDerivedType(typeof(VisualIrRectNode), "rect")]
[JsonDerivedType(typeof(VisualIrEllipseNode), "ellipse")]
[JsonDerivedType(typeof(VisualIrPathNode), "path")]
[JsonDerivedType(typeof(VisualIrTextNode), "text")]
[JsonDerivedType(typeof(VisualIrImageNode), "image")]
[JsonDerivedType(typeof(VisualIrVideoNode), "video")]
[JsonDerivedType(typeof(VisualIrSvgNode), "svg")]
internal abstract record VisualIrNode
{
    public required string Id { get; init; }
    public required VisualIrRect Bounds { get; init; }
    public double Opacity { get; init; } = 1;
    public IReadOnlyList<VisualIrShadowEffect>? Effects { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

internal sealed record VisualIrGroupNode : VisualIrNode
{
    public IReadOnlyList<VisualIrNode> Children { get; init; } = [];
    public VisualIrRect? ClipRect { get; init; }
}

internal sealed record VisualIrRectNode : VisualIrNode
{
    public VisualIrPaint? Fill { get; init; }
    public VisualIrStroke? Stroke { get; init; }
    public double Radius { get; init; }
}

internal sealed record VisualIrEllipseNode : VisualIrNode
{
    public VisualIrPaint? Fill { get; init; }
    public VisualIrStroke? Stroke { get; init; }
}

internal sealed record VisualIrPathNode : VisualIrNode
{
    public required string Data { get; init; }
    public VisualIrPaint? Fill { get; init; }
    public string? FillRule { get; init; }
    public VisualIrStroke? Stroke { get; init; }
}

internal sealed record VisualIrTextNode : VisualIrNode
{
    public required string Text { get; init; }
    public required VisualIrTextStyle Style { get; init; }
    public string TextAlign { get; init; } = "left";
    public string VerticalAlign { get; init; } = "top";
}

internal sealed record VisualIrTextStyle
{
    public VisualIrPaint? Fill { get; init; }
    public string? FontFamily { get; init; }
    public double FontSize { get; init; }
    public int? FontWeight { get; init; }
    public string? FontStyle { get; init; }
    public double? LineHeight { get; init; }
}

internal sealed record VisualIrImageNode : VisualIrNode
{
    public required VisualIrSource Source { get; init; }
    public string Fit { get; init; } = "fill";
    public string? Alt { get; init; }
}

internal sealed record VisualIrVideoNode : VisualIrNode
{
    public required VisualIrSource Source { get; init; }
    public string Fit { get; init; } = "fill";
    public double? TimeSeconds { get; init; }
    public bool Muted { get; init; } = true;
    public bool Loop { get; init; }
}

internal sealed record VisualIrSvgNode : VisualIrNode
{
    public string? Markup { get; init; }
    public VisualIrSource? Source { get; init; }
    public string Fit { get; init; } = "fill";
    public VisualIrPaint? Tint { get; init; }
}
