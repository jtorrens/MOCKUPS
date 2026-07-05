using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Preview.Resolved;
using Mockups.DesktopEditorShell.Preview.Resolvers;
using Mockups.DesktopEditorShell.VisualIr;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Preview.Bridges;

internal static class DesignPreviewToVisualIrBridge
{
    public static VisualIrDocument Convert(
        DesignPreviewPayload payload,
        SpikeDatabase.DevicePreviewMetrics metrics,
        string selectedColorVariant = "set_night")
    {
        var frame = DesignPreviewFrameResolver.Resolve(payload, metrics);
        var document = Convert(frame, payload, selectedColorVariant);
        VisualIrValidator.ThrowIfInvalid(document);
        return document;
    }

    private static VisualIrDocument Convert(
        ResolvedDesignFrame frame,
        DesignPreviewPayload payload,
        string selectedColorVariant)
    {
        return new VisualIrDocument
        {
            Viewport = new VisualIrViewport(frame.Width, frame.Height, 1),
            Resources = new VisualIrResources
            {
                ColorVariants = ["set_day", "set_night"],
                DefaultColorVariant = selectedColorVariant,
            },
            Root = new VisualIrGroupNode
            {
                Id = "root",
                Bounds = new VisualIrRect(0, 0, frame.Width, frame.Height),
                Children = frame.Children.Select((node) => ConvertNode(node, payload)).ToList(),
            },
            Metadata = frame.Metadata,
        };
    }

    private static VisualIrNode ConvertNode(ResolvedDesignNode node, DesignPreviewPayload payload)
    {
        return node switch
        {
            ResolvedDesignGroupNode group => new VisualIrGroupNode
            {
                Id = group.Id,
                Bounds = Rect(group.Bounds),
                Opacity = group.Opacity,
                Effects = Effects(group.Effects, payload),
                ClipRect = group.ClipRect is null ? null : Rect(group.ClipRect),
                Children = group.Children.Select((child) => ConvertNode(child, payload)).ToList(),
                Metadata = group.Metadata,
            },
            ResolvedDesignRectNode rect => new VisualIrRectNode
            {
                Id = rect.Id,
                Bounds = Rect(rect.Bounds),
                Opacity = rect.Opacity,
                Effects = Effects(rect.Effects, payload),
                Fill = Paint(rect.Fill, payload),
                Stroke = Stroke(rect.Stroke, payload),
                Radius = rect.Radius,
                Metadata = MetadataWithColor(MetadataWithColor(rect.Metadata, "fill", rect.Fill, payload), "stroke", rect.Stroke?.Paint, payload),
            },
            ResolvedDesignTextNode text => new VisualIrTextNode
            {
                Id = text.Id,
                Bounds = Rect(text.Bounds),
                Opacity = text.Opacity,
                Effects = Effects(text.Effects, payload),
                Text = text.Text,
                Style = new VisualIrTextStyle
                {
                    Fill = Paint(text.Style.Fill, payload),
                    FontFamily = text.Style.FontFamily,
                    FontSize = text.Style.FontSize,
                    FontWeight = text.Style.FontWeight,
                    FontStyle = text.Style.FontStyle,
                    LineHeight = text.Style.LineHeight,
                },
                TextAlign = text.TextAlign,
                VerticalAlign = text.VerticalAlign,
                Metadata = MetadataWithColor(text.Metadata, "textFill", text.Style.Fill, payload),
            },
            ResolvedDesignSvgNode svg => new VisualIrSvgNode
            {
                Id = svg.Id,
                Bounds = Rect(svg.Bounds),
                Opacity = svg.Opacity,
                Effects = Effects(svg.Effects, payload),
                Markup = svg.Markup,
                Fit = svg.Fit,
                Tint = Paint(svg.Tint, payload),
                Metadata = MetadataWithColor(svg.Metadata, "tint", svg.Tint, payload),
            },
            _ => throw new InvalidOperationException($"Unsupported resolved design node: {node.GetType().Name}"),
        };
    }

    private static VisualIrRect Rect(DesignRect rect)
    {
        return new VisualIrRect(rect.X, rect.Y, rect.Width, rect.Height);
    }

    private static VisualIrPaint? Paint(ResolvedDesignPaint? paint, DesignPreviewPayload payload)
    {
        return paint is null ? null : new VisualIrSolidPaint(Color(paint.Color, payload));
    }

    private static VisualIrStroke? Stroke(ResolvedDesignStroke? stroke, DesignPreviewPayload payload)
    {
        return stroke is null ? null : new VisualIrStroke(Paint(stroke.Paint, payload) ?? new VisualIrNonePaint(), stroke.Width);
    }

    private static IReadOnlyList<VisualIrEffect>? Effects(
        IReadOnlyList<ResolvedDesignEffect>? effects,
        DesignPreviewPayload payload)
    {
        return effects?.Select((effect) => effect switch
            {
                ResolvedDesignShadowEffect shadow => (VisualIrEffect)new VisualIrShadowEffect(
                    shadow.X,
                    shadow.Y,
                    shadow.Blur,
                    Color(shadow.Color, payload),
                    shadow.Inset),
                ResolvedDesignBlurEffect blur => (VisualIrEffect)new VisualIrBlurEffect(blur.Radius),
                _ => throw new InvalidOperationException($"Unsupported resolved design effect: {effect.GetType().Name}"),
            })
            .ToList();
    }

    private static IReadOnlyDictionary<string, string>? MetadataWithColor(
        IReadOnlyDictionary<string, string>? metadata,
        string prefix,
        ResolvedDesignPaint? paint,
        DesignPreviewPayload payload)
    {
        if (paint is null)
        {
            return metadata;
        }

        var result = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        var color = paint.Color;
        result[$"{prefix}.colorRef"] = color.Id;
        if (!string.IsNullOrWhiteSpace(color.ThemeTokenId)
            && ThemeColorTokenCatalog.TryGet(color.ThemeTokenId, out var paths))
        {
            result[$"{prefix}.themeToken"] = color.ThemeTokenId;
            result[$"{prefix}.set_day"] = AdjustColor(ThemeColor(payload, paths.LightPath, paths.LightAlphaPath, color.FallbackValue), color.BrightnessMultiplier);
            result[$"{prefix}.set_night"] = AdjustColor(ThemeColor(payload, paths.DarkPath, paths.DarkAlphaPath, color.FallbackValue), color.BrightnessMultiplier);
        }
        else
        {
            result[$"{prefix}.static"] = AdjustColor(ResolveColorValue(payload, color.FallbackValue), color.BrightnessMultiplier);
        }

        return result;
    }

    private static VisualIrColor Color(ResolvedDesignColorRef color, DesignPreviewPayload payload)
    {
        var fallback = ResolveColorValue(payload, color.FallbackValue);
        if (string.IsNullOrWhiteSpace(color.ThemeTokenId))
        {
            return VisualIrColor.Static(IsVisualIrColor(fallback) ? AdjustColor(fallback, color.BrightnessMultiplier) : "#ff00ff");
        }

        if (!ThemeColorTokenCatalog.TryGet(color.ThemeTokenId, out var paths))
        {
            return VisualIrColor.Static(IsVisualIrColor(fallback) ? AdjustColor(fallback, color.BrightnessMultiplier) : "#ff00ff");
        }

        return VisualIrColor.Variant(
            new Dictionary<string, string>
            {
                ["set_day"] = AdjustColor(ThemeColor(payload, paths.LightPath, paths.LightAlphaPath, color.FallbackValue), color.BrightnessMultiplier),
                ["set_night"] = AdjustColor(ThemeColor(payload, paths.DarkPath, paths.DarkAlphaPath, color.FallbackValue), color.BrightnessMultiplier),
            },
            IsVisualIrColor(fallback) ? AdjustColor(fallback, color.BrightnessMultiplier) : "#ff00ff");
    }

    private static string ThemeColor(
        DesignPreviewPayload payload,
        IReadOnlyList<string> path,
        IReadOnlyList<string>? alphaPath,
        string fallback)
    {
        var tokens = JsonPath.ParseObject(payload.ThemeTokensJson);
        var current = JsonPath.Get(tokens, path);
        var resolved = ResolveColorValue(payload, current?.ToString() ?? fallback);
        var resolvedFallback = ResolveColorValue(payload, fallback);
        var color = IsVisualIrColor(resolved)
            ? resolved
            : IsVisualIrColor(resolvedFallback) ? resolvedFallback : "#ff00ff";
        return alphaPath is null ? color : ColorValue.WithAlpha(color, JsonPath.NumberAt(tokens, alphaPath, 1));
    }

    private static string ResolveColorValue(DesignPreviewPayload payload, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "#00000000";
        }

        if (value.StartsWith("#", StringComparison.Ordinal)
            || value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            return value.Equals("transparent", StringComparison.OrdinalIgnoreCase) ? "#00000000" : value;
        }

        return payload.PaletteColors.TryGetValue(value, out var resolved) ? resolved : value;
    }

    private static bool IsVisualIrColor(string value)
    {
        return ColorValue.IsHexColor(value);
    }

    private static string AdjustColor(string color, double brightnessMultiplier)
    {
        return brightnessMultiplier == 0 ? color : ColorValue.AdjustBrightness(color, brightnessMultiplier);
    }

}
