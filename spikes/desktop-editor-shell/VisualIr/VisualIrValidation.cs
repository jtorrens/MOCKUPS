using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.VisualIr;

internal sealed record VisualIrDiagnostic(string Code, string Message, string Path);

internal static partial class VisualIrValidator
{
    public static IReadOnlyList<VisualIrDiagnostic> Validate(VisualIrDocument document)
    {
        var diagnostics = new List<VisualIrDiagnostic>();
        if (document.Kind != "visual-ir.document")
        {
            diagnostics.Add(new VisualIrDiagnostic("document.kind", "Document kind must be visual-ir.document.", "$"));
        }

        if (!document.Viewport.IsValid())
        {
            diagnostics.Add(new VisualIrDiagnostic("document.viewport", "Viewport must have positive finite dimensions and scale.", "$.viewport"));
        }

        var colorVariants = document.Resources?.ColorVariants is { Count: > 0 } variants
            ? variants.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        ValidateNode(document.Root, "$.root", colorVariants, diagnostics);
        return diagnostics;
    }

    public static void ThrowIfInvalid(VisualIrDocument document)
    {
        var diagnostics = Validate(document);
        if (diagnostics.Count == 0)
        {
            return;
        }

        var message = string.Join(Environment.NewLine, diagnostics.Select((diagnostic) =>
            $"{diagnostic.Path}: {diagnostic.Code}: {diagnostic.Message}"));
        throw new InvalidOperationException(message);
    }

    private static void ValidateNode(
        VisualIrNode node,
        string path,
        ISet<string> colorVariants,
        ICollection<VisualIrDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            diagnostics.Add(new VisualIrDiagnostic("node.id", "Node id is required.", path));
        }

        if (!node.Bounds.IsValid())
        {
            diagnostics.Add(new VisualIrDiagnostic("node.bounds", "Node bounds must be finite and non-negative.", $"{path}.bounds"));
        }

        if (node.Opacity is < 0 or > 1 || double.IsNaN(node.Opacity) || double.IsInfinity(node.Opacity))
        {
            diagnostics.Add(new VisualIrDiagnostic("node.opacity", "Node opacity must be between 0 and 1.", $"{path}.opacity"));
        }

        if (node.Effects is not null)
        {
            for (var index = 0; index < node.Effects.Count; index++)
            {
                switch (node.Effects[index])
                {
                    case VisualIrShadowEffect shadow:
                        ValidateColor(shadow.Color, $"{path}.effects[{index}].color", colorVariants, diagnostics);
                        if (shadow.Blur < 0 || double.IsNaN(shadow.Blur) || double.IsInfinity(shadow.Blur))
                        {
                            diagnostics.Add(new VisualIrDiagnostic("effect.shadow.blur", "Shadow blur must be finite and non-negative.", $"{path}.effects[{index}].blur"));
                        }
                        break;
                    case VisualIrBlurEffect blur:
                        if (blur.Radius < 0 || double.IsNaN(blur.Radius) || double.IsInfinity(blur.Radius))
                        {
                            diagnostics.Add(new VisualIrDiagnostic("effect.blur.radius", "Blur radius must be finite and non-negative.", $"{path}.effects[{index}].radius"));
                        }
                        break;
                }
            }
        }

        switch (node)
        {
            case VisualIrGroupNode group:
                if (group.ClipRadius < 0 || double.IsNaN(group.ClipRadius) || double.IsInfinity(group.ClipRadius))
                {
                    diagnostics.Add(new VisualIrDiagnostic("group.clipRadius", "Group clip radius must be finite and non-negative.", $"{path}.clipRadius"));
                }

                for (var index = 0; index < group.Children.Count; index++)
                {
                    ValidateNode(group.Children[index], $"{path}.children[{index}]", colorVariants, diagnostics);
                }

                break;
            case VisualIrRectNode rect:
                ValidatePaint(rect.Fill, $"{path}.fill", colorVariants, diagnostics);
                ValidateStroke(rect.Stroke, $"{path}.stroke", colorVariants, diagnostics);
                break;
            case VisualIrEllipseNode ellipse:
                ValidatePaint(ellipse.Fill, $"{path}.fill", colorVariants, diagnostics);
                ValidateStroke(ellipse.Stroke, $"{path}.stroke", colorVariants, diagnostics);
                break;
            case VisualIrPathNode pathNode:
                if (string.IsNullOrWhiteSpace(pathNode.Data))
                {
                    diagnostics.Add(new VisualIrDiagnostic("path.data", "Path data is required.", $"{path}.data"));
                }

                ValidatePaint(pathNode.Fill, $"{path}.fill", colorVariants, diagnostics);
                ValidateStroke(pathNode.Stroke, $"{path}.stroke", colorVariants, diagnostics);
                break;
            case VisualIrTextNode text:
                ValidatePaint(text.Style.Fill, $"{path}.style.fill", colorVariants, diagnostics);
                if (text.Style.FontSize <= 0 || double.IsNaN(text.Style.FontSize) || double.IsInfinity(text.Style.FontSize))
                {
                    diagnostics.Add(new VisualIrDiagnostic("text.fontSize", "Text font size must be positive.", $"{path}.style.fontSize"));
                }

                break;
            case VisualIrImageNode image:
                ValidateSource(image.Source, $"{path}.source", diagnostics);
                break;
            case VisualIrVideoNode video:
                ValidateSource(video.Source, $"{path}.source", diagnostics);
                break;
            case VisualIrSvgNode svg:
                if (string.IsNullOrWhiteSpace(svg.Markup) && svg.Source is null)
                {
                    diagnostics.Add(new VisualIrDiagnostic("svg.source", "SVG node requires markup or source.", path));
                }

                if (svg.Source is not null)
                {
                    ValidateSource(svg.Source, $"{path}.source", diagnostics);
                }

                ValidatePaint(svg.Tint, $"{path}.tint", colorVariants, diagnostics);
                break;
        }
    }

    private static void ValidatePaint(
        VisualIrPaint? paint,
        string path,
        ISet<string> colorVariants,
        ICollection<VisualIrDiagnostic> diagnostics)
    {
        switch (paint)
        {
            case null:
            case VisualIrNonePaint:
                return;
            case VisualIrSolidPaint solid:
                ValidateColor(solid.Color, $"{path}.color", colorVariants, diagnostics);
                return;
            case VisualIrLinearGradientPaint gradient:
                for (var index = 0; index < gradient.Stops.Count; index++)
                {
                    ValidateColor(gradient.Stops[index].Color, $"{path}.stops[{index}].color", colorVariants, diagnostics);
                }

                return;
            case VisualIrRadialGradientPaint gradient:
                for (var index = 0; index < gradient.Stops.Count; index++)
                {
                    ValidateColor(gradient.Stops[index].Color, $"{path}.stops[{index}].color", colorVariants, diagnostics);
                }

                return;
        }
    }

    private static void ValidateStroke(
        VisualIrStroke? stroke,
        string path,
        ISet<string> colorVariants,
        ICollection<VisualIrDiagnostic> diagnostics)
    {
        if (stroke is null)
        {
            return;
        }

        if (stroke.Width < 0 || double.IsNaN(stroke.Width) || double.IsInfinity(stroke.Width))
        {
            diagnostics.Add(new VisualIrDiagnostic("stroke.width", "Stroke width must be finite and non-negative.", $"{path}.width"));
        }

        ValidatePaint(stroke.Paint, $"{path}.paint", colorVariants, diagnostics);
    }

    private static void ValidateColor(
        VisualIrColor? color,
        string path,
        ISet<string> colorVariants,
        ICollection<VisualIrDiagnostic> diagnostics)
    {
        switch (color)
        {
            case null:
                return;
            case VisualIrStaticColor staticColor:
                ValidateHexColor(staticColor.Value, path, diagnostics);
                return;
            case VisualIrVariantColor variantColor:
                if (variantColor.Values.Count == 0)
                {
                    diagnostics.Add(new VisualIrDiagnostic("color.variant.values", "Variant color requires at least one value.", $"{path}.values"));
                }

                foreach (var (variant, value) in variantColor.Values)
                {
                    if (colorVariants.Count > 0 && !colorVariants.Contains(variant))
                    {
                        diagnostics.Add(new VisualIrDiagnostic("color.variant.unknown", $"Variant '{variant}' is not declared in document resources.", $"{path}.values.{variant}"));
                    }

                    ValidateHexColor(value, $"{path}.values.{variant}", diagnostics);
                }

                if (!string.IsNullOrWhiteSpace(variantColor.Fallback))
                {
                    ValidateHexColor(variantColor.Fallback, $"{path}.fallback", diagnostics);
                }

                if (colorVariants.Count > 0 && string.IsNullOrWhiteSpace(variantColor.Fallback))
                {
                    foreach (var declaredVariant in colorVariants.Where((variant) => !variantColor.Values.ContainsKey(variant)))
                    {
                        diagnostics.Add(new VisualIrDiagnostic("color.variant.missing", $"Variant '{declaredVariant}' is missing and no fallback was provided.", $"{path}.values"));
                    }
                }

                return;
        }
    }

    private static void ValidateHexColor(
        string value,
        string path,
        ICollection<VisualIrDiagnostic> diagnostics)
    {
        if (!HexColorRegex().IsMatch(value))
        {
            diagnostics.Add(new VisualIrDiagnostic("color.hex", "Color must be #RRGGBB or #RRGGBBAA.", path));
        }
    }

    private static void ValidateSource(
        VisualIrSource source,
        string path,
        ICollection<VisualIrDiagnostic> diagnostics)
    {
        if (source.Kind == "asset")
        {
            if (string.IsNullOrWhiteSpace(source.AssetId))
            {
                diagnostics.Add(new VisualIrDiagnostic("source.assetId", "Asset source requires assetId.", $"{path}.assetId"));
            }

            return;
        }

        if (source.Kind == "uri")
        {
            if (string.IsNullOrWhiteSpace(source.Uri))
            {
                diagnostics.Add(new VisualIrDiagnostic("source.uri", "URI source requires uri.", $"{path}.uri"));
            }

            return;
        }

        diagnostics.Add(new VisualIrDiagnostic("source.kind", "Source kind must be asset or uri.", $"{path}.kind"));
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex HexColorRegex();
}
