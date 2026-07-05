using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.VisualIr;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.Preview.Avalonia;

internal sealed class AvaloniaVisualIrDebugRenderer : IVisualIrRenderer
{
    private static readonly IBrush BoundsBrush = new SolidColorBrush(Color.Parse("#94A3B8"));
    private static readonly IBrush PlaceholderBrush = new SolidColorBrush(Color.Parse("#27272A"));
    private static readonly IBrush PlaceholderBorderBrush = new SolidColorBrush(Color.Parse("#A3A3A3"));

    public Control Render(VisualIrDocument document, VisualIrRenderOptions options)
    {
        VisualIrValidator.ThrowIfInvalid(document);

        var canvas = new Canvas
        {
            Width = document.Viewport.Width,
            Height = document.Viewport.Height,
            ClipToBounds = true,
            Background = Brushes.Transparent,
        };

        foreach (var child in document.Root.Children)
        {
            canvas.Children.Add(RenderNode(child, document, options));
        }

        return canvas;
    }

    private Control RenderNode(
        VisualIrNode node,
        VisualIrDocument document,
        VisualIrRenderOptions options)
    {
        var control = node switch
        {
            VisualIrGroupNode group => RenderGroup(group, document, options),
            VisualIrRectNode rect => RenderRect(rect, document, options),
            VisualIrTextNode text => RenderText(text, document, options),
            VisualIrEllipseNode ellipse => RenderPlaceholder(ellipse, "ellipse", options),
            VisualIrPathNode path => RenderPlaceholder(path, "path", options),
            VisualIrImageNode image => RenderPlaceholder(image, "image", options),
            VisualIrVideoNode video => RenderPlaceholder(video, "video", options),
            VisualIrSvgNode svg => RenderPlaceholder(svg, "svg", options),
            _ => RenderPlaceholder(node, node.GetType().Name, options),
        };

        control.Opacity = node.Opacity;
        Canvas.SetLeft(control, node.Bounds.X);
        Canvas.SetTop(control, node.Bounds.Y);
        return control;
    }

    private Control RenderGroup(
        VisualIrGroupNode group,
        VisualIrDocument document,
        VisualIrRenderOptions options)
    {
        var canvas = new Canvas
        {
            Width = group.Bounds.Width,
            Height = group.Bounds.Height,
            ClipToBounds = group.ClipRect is not null,
            Background = Brushes.Transparent,
        };

        foreach (var child in group.Children)
        {
            canvas.Children.Add(RenderNode(child, document, options));
        }

        if (options.ShowBounds)
        {
            canvas.Children.Add(BoundsOverlay(group.Bounds.Width, group.Bounds.Height));
        }

        return canvas;
    }

    private Control RenderRect(
        VisualIrRectNode rect,
        VisualIrDocument document,
        VisualIrRenderOptions options)
    {
        var border = new Border
        {
            Width = rect.Bounds.Width,
            Height = rect.Bounds.Height,
            Background = BrushFromPaint(rect.Fill, document, options),
            BorderBrush = BrushFromStroke(rect.Stroke, document, options),
            BorderThickness = new Thickness(rect.Stroke?.Width ?? 0),
            CornerRadius = new CornerRadius(rect.Radius),
        };

        if (!options.ShowBounds)
        {
            return border;
        }

        return WrapWithBounds(border, rect.Bounds.Width, rect.Bounds.Height);
    }

    private Control RenderText(
        VisualIrTextNode text,
        VisualIrDocument document,
        VisualIrRenderOptions options)
    {
        var textBlock = new TextBlock
        {
            Width = text.Bounds.Width,
            Height = text.Bounds.Height,
            Text = text.Text,
            FontSize = text.Style.FontSize,
            FontFamily = string.IsNullOrWhiteSpace(text.Style.FontFamily)
                ? FontFamily.Default
                : new FontFamily(text.Style.FontFamily),
            FontWeight = FontWeightFrom(text.Style.FontWeight),
            FontStyle = text.Style.FontStyle == "italic" ? FontStyle.Italic : FontStyle.Normal,
            Foreground = BrushFromPaint(text.Style.Fill, document, options) ?? Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (!options.ShowBounds)
        {
            return textBlock;
        }

        return WrapWithBounds(textBlock, text.Bounds.Width, text.Bounds.Height);
    }

    private Control RenderPlaceholder(
        VisualIrNode node,
        string label,
        VisualIrRenderOptions options)
    {
        if (!options.ShowUnsupportedPlaceholders)
        {
            return new Canvas
            {
                Width = node.Bounds.Width,
                Height = node.Bounds.Height,
            };
        }

        var placeholder = new Border
        {
            Width = node.Bounds.Width,
            Height = node.Bounds.Height,
            Background = PlaceholderBrush,
            BorderBrush = PlaceholderBorderBrush,
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = $"{label} · {node.Id}",
                FontSize = 10,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0),
            },
        };

        return options.ShowBounds
            ? WrapWithBounds(placeholder, node.Bounds.Width, node.Bounds.Height)
            : placeholder;
    }

    private static Control WrapWithBounds(Control child, double width, double height)
    {
        var grid = new Grid
        {
            Width = width,
            Height = height,
        };
        grid.Children.Add(child);
        grid.Children.Add(BoundsOverlay(width, height));
        return grid;
    }

    private static Border BoundsOverlay(double width, double height)
    {
        return new Border
        {
            Width = width,
            Height = height,
            BorderBrush = BoundsBrush,
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false,
        };
    }

    private static IBrush? BrushFromStroke(
        VisualIrStroke? stroke,
        VisualIrDocument document,
        VisualIrRenderOptions options)
    {
        return stroke is null ? null : BrushFromPaint(stroke.Paint, document, options);
    }

    private static IBrush? BrushFromPaint(
        VisualIrPaint? paint,
        VisualIrDocument document,
        VisualIrRenderOptions options)
    {
        return paint switch
        {
            null => null,
            VisualIrNonePaint => null,
            VisualIrSolidPaint solid => new SolidColorBrush(Color.Parse(ResolveColor(solid.Color, document, options))),
            VisualIrLinearGradientPaint gradient => LinearGradientBrush(gradient, document, options),
            VisualIrRadialGradientPaint gradient => RadialGradientBrush(gradient, document, options),
            _ => null,
        };
    }

    private static LinearGradientBrush LinearGradientBrush(
        VisualIrLinearGradientPaint gradient,
        VisualIrDocument document,
        VisualIrRenderOptions options)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(gradient.From.X, gradient.From.Y, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(gradient.To.X, gradient.To.Y, RelativeUnit.Absolute),
        };
        foreach (var stop in gradient.Stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.Parse(ResolveColor(stop.Color, document, options)), stop.Offset));
        }

        return brush;
    }

    private static RadialGradientBrush RadialGradientBrush(
        VisualIrRadialGradientPaint gradient,
        VisualIrDocument document,
        VisualIrRenderOptions options)
    {
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(gradient.Center.X, gradient.Center.Y, RelativeUnit.Absolute),
        };
        foreach (var stop in gradient.Stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.Parse(ResolveColor(stop.Color, document, options)), stop.Offset));
        }

        return brush;
    }

    private static string ResolveColor(
        VisualIrColor color,
        VisualIrDocument document,
        VisualIrRenderOptions options)
    {
        if (color is VisualIrStaticColor staticColor)
        {
            return staticColor.Value;
        }

        if (color is not VisualIrVariantColor variantColor)
        {
            return "#ff00ff";
        }

        var selectedVariant = options.SelectedColorVariant
            ?? document.Resources?.DefaultColorVariant
            ?? document.Resources?.ColorVariants?.FirstOrDefault();
        if (selectedVariant is not null && variantColor.Values.TryGetValue(selectedVariant, out var value))
        {
            return value;
        }

        return string.IsNullOrWhiteSpace(variantColor.Fallback) ? "#ff00ff" : variantColor.Fallback;
    }

    private static FontWeight FontWeightFrom(int? weight)
    {
        return weight switch
        {
            >= 800 => FontWeight.ExtraBold,
            >= 700 => FontWeight.Bold,
            >= 600 => FontWeight.SemiBold,
            >= 500 => FontWeight.Medium,
            <= 300 => FontWeight.Light,
            _ => FontWeight.Normal,
        };
    }
}
