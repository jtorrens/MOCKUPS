using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Preview.Resolved;

internal sealed record ResolvedDesignFrame
{
    public required double Width { get; init; }
    public required double Height { get; init; }
    public required IReadOnlyList<ResolvedDesignNode> Children { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

internal sealed record DesignRect(double X, double Y, double Width, double Height);

internal abstract record ResolvedDesignNode
{
    public required string Id { get; init; }
    public required DesignRect Bounds { get; init; }
    public double Opacity { get; init; } = 1;
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

internal sealed record ResolvedDesignGroupNode : ResolvedDesignNode
{
    public IReadOnlyList<ResolvedDesignNode> Children { get; init; } = [];
    public DesignRect? ClipRect { get; init; }
}

internal sealed record ResolvedDesignRectNode : ResolvedDesignNode
{
    public ResolvedDesignPaint? Fill { get; init; }
    public double Radius { get; init; }
}

internal sealed record ResolvedDesignTextNode : ResolvedDesignNode
{
    public required string Text { get; init; }
    public required ResolvedDesignTextStyle Style { get; init; }
    public string TextAlign { get; init; } = "left";
    public string VerticalAlign { get; init; } = "top";
}

internal sealed record ResolvedDesignSvgNode : ResolvedDesignNode
{
    public required string Markup { get; init; }
    public string Fit { get; init; } = "fill";
    public ResolvedDesignPaint? Tint { get; init; }
}

internal sealed record ResolvedDesignTextStyle
{
    public ResolvedDesignPaint? Fill { get; init; }
    public string? FontFamily { get; init; }
    public double FontSize { get; init; }
    public int? FontWeight { get; init; }
    public string? FontStyle { get; init; }
    public double? LineHeight { get; init; }
}

internal sealed record ResolvedDesignPaint(ResolvedDesignColorRef Color);

internal sealed record ResolvedDesignColorRef(
    string Id,
    string? ThemeTokenId,
    string FallbackValue);
