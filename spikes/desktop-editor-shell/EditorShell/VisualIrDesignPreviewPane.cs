using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Preview;
using Mockups.DesktopEditorShell.Preview.Avalonia;
using Mockups.DesktopEditorShell.Preview.Bridges;
using Mockups.DesktopEditorShell.VisualIr;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class VisualIrDesignPreviewPane : Grid
{
    private readonly IVisualIrRenderer _renderer = new AvaloniaVisualIrDebugRenderer();

    public string? Update(
        SpikeDatabase.DevicePreviewMetrics metrics,
        bool isDark,
        string themeName,
        string themeMode,
        string scaleMode,
        bool showDesignMarks,
        DesignPreviewPayload? payload)
    {
        Children.Clear();

        var panelText = isDark ? "#F3F4F6" : "#111827";
        Background = Brushes.Transparent;

        if (payload is null)
        {
            Children.Add(Placeholder(
                "Visual IR debug preview",
                "Select a status bar or navigation bar to inspect the bridge output."));
            return null;
        }

        try
        {
            var colorVariant = themeMode == "dark" ? "set_night" : "set_day";
            var document = DesignPreviewToVisualIrBridge.Convert(payload, metrics, colorVariant);
            var rendered = _renderer.Render(document, new VisualIrRenderOptions(
                SelectedColorVariant: colorVariant,
                ShowBounds: showDesignMarks,
                ShowUnsupportedPlaceholders: true));

            Children.Add(DeviceFrame(
                metrics,
                document.Viewport.Width,
                document.Viewport.Height,
                rendered,
                panelText,
                themeName,
                themeMode,
                scaleMode,
                showDesignMarks,
                colorVariant,
                document,
                payload));
            return null;
        }
        catch (System.Exception exception)
        {
            var message = $"IR error · {payload.Name} · {payload.Kind} · {exception.GetType().Name}: {exception.Message}";
            Children.Add(Placeholder(
                $"{payload.Name} · {payload.Kind}",
                message));
            return message;
        }
    }

    private static Control DeviceFrame(
        SpikeDatabase.DevicePreviewMetrics metrics,
        double frameWidth,
        double frameHeight,
        Control rendered,
        string panelText,
        string themeName,
        string themeMode,
        string scaleMode,
        bool showDesignMarks,
        string colorVariant,
        VisualIrDocument document,
        DesignPreviewPayload payload)
    {
        const double frameBorderThickness = 10;
        const double shadowPadding = 40;
        var scale = metrics.ScaleToPixels > 0 ? metrics.ScaleToPixels : 1;
        var cornerRadius = metrics.CornerRadius / scale;
        var candyOffset = frameBorderThickness / 2;
        var canvas = new Canvas
        {
            Width = frameWidth + frameBorderThickness,
            Height = frameHeight + frameBorderThickness,
            ClipToBounds = false,
            Effect = new DropShadowEffect
            {
                Color = Color.Parse("#66000000"),
                BlurRadius = 28,
                OffsetX = 0,
                OffsetY = 12,
            },
        };
        var clippedDesign = new Border
        {
            Width = frameWidth,
            Height = frameHeight,
            CornerRadius = new CornerRadius(cornerRadius),
            ClipToBounds = true,
            Child = rendered,
        };
        Canvas.SetLeft(clippedDesign, candyOffset);
        Canvas.SetTop(clippedDesign, candyOffset);
        canvas.Children.Add(clippedDesign);
        if (showDesignMarks)
        {
            var guides = DesignGuides(frameWidth, frameHeight);
            Canvas.SetLeft(guides, candyOffset);
            Canvas.SetTop(guides, candyOffset);
            canvas.Children.Add(guides);
        }

        var frameBorder = new Border
        {
            Width = frameWidth + frameBorderThickness,
            Height = frameHeight + frameBorderThickness,
            BorderBrush = new SolidColorBrush(Color.Parse("#18181B")),
            BorderThickness = new Thickness(frameBorderThickness),
            CornerRadius = new CornerRadius(cornerRadius + frameBorderThickness / 2),
            IsHitTestVisible = false,
        };
        canvas.Children.Add(frameBorder);

        if (showDesignMarks)
        {
            var meta = new TextBlock
            {
                Text = $"IR · {payload.Kind} · {metrics.Name} · {themeName} · {themeMode}{ColorSummary(document, payload.Kind, colorVariant)}",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.Parse("#AA18181B")),
                FontSize = 10,
                Padding = new Thickness(8, 4),
            };
            Canvas.SetLeft(meta, candyOffset + 12);
            Canvas.SetTop(meta, candyOffset + frameHeight - 30);
            canvas.Children.Add(meta);
        }

        canvas.HorizontalAlignment = HorizontalAlignment.Center;
        canvas.VerticalAlignment = VerticalAlignment.Center;
        canvas.Margin = new Thickness(shadowPadding);

        var elevatedSurface = new Grid
        {
            Width = canvas.Width + shadowPadding * 2,
            Height = canvas.Height + shadowPadding * 2,
            ClipToBounds = false,
            Children = { canvas },
        };

        Control content = PreviewScaleMode.TryFixedScale(scaleMode, out var fixedScale)
            ? new DraggablePreviewSurface(elevatedSurface, fixedScale)
            : new Viewbox
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = elevatedSurface,
            };

        return new Grid
        {
            Margin = new Thickness(18),
            Children =
            {
                content,
            },
        };
    }

    private static Canvas DesignGuides(double width, double height)
    {
        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            ClipToBounds = true,
            IsHitTestVisible = false,
        };
        var guideBrush = new SolidColorBrush(Color.Parse("#80FF00FF"));
        foreach (var ratio in new[] { 0.25, 0.5, 0.75 })
        {
            var isCenter = ratio == 0.5;
            canvas.Children.Add(GuideLine(width * ratio, 0, width * ratio, height, guideBrush, isCenter));
            canvas.Children.Add(GuideLine(0, height * ratio, width, height * ratio, guideBrush, isCenter));
        }

        return canvas;
    }

    private static Line GuideLine(
        double startX,
        double startY,
        double endX,
        double endY,
        IBrush brush,
        bool isCenter)
    {
        return new Line
        {
            StartPoint = new Point(startX, startY),
            EndPoint = new Point(endX, endY),
            Stroke = brush,
            StrokeThickness = 1,
            StrokeDashArray = isCenter ? null : new AvaloniaList<double> { 6, 6 },
        };
    }

    private static string ColorSummary(VisualIrDocument document, string payloadKind, string colorVariant)
    {
        var prefix = payloadKind switch
        {
            "statusBar" => "statusBar",
            "navigationBar" => "navigationBar",
            _ => "",
        };
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return "";
        }

        var nodes = Flatten(document.Root).ToList();
        var background = nodes
            .FirstOrDefault((node) => node.Id == $"{prefix}.background")?
            .Metadata;
        var foreground = nodes
            .FirstOrDefault((node) => node.Id.StartsWith($"{prefix}.item.", System.StringComparison.Ordinal)
                && (node.Metadata?.ContainsKey("textFill.themeToken") == true
                    || node.Metadata?.ContainsKey("tint.themeToken") == true))?
            .Metadata;
        var backgroundHex = ValueFor(background, "fill", colorVariant);
        var foregroundHex = ValueFor(foreground, foreground?.ContainsKey("textFill.themeToken") == true ? "textFill" : "tint", colorVariant);
        return $" · fg {foregroundHex} · bg {backgroundHex}";
    }

    private static string ValueFor(IReadOnlyDictionary<string, string>? metadata, string prefix, string colorVariant)
    {
        var key = colorVariant == "set_day" ? $"{prefix}.set_day" : $"{prefix}.set_night";
        return metadata is not null && metadata.TryGetValue(key, out var value) ? value : "—";
    }

    private static IEnumerable<VisualIrNode> Flatten(VisualIrNode node)
    {
        yield return node;
        if (node is not VisualIrGroupNode group)
        {
            yield break;
        }

        foreach (var child in group.Children.SelectMany(Flatten))
        {
            yield return child;
        }
    }

    private static Control Placeholder(string title, string text)
    {
        return new Grid
        {
            Children =
            {
                new Border
                {
                    MaxWidth = 340,
                    Padding = new Thickness(16),
                    CornerRadius = new CornerRadius(12),
                    BorderBrush = new SolidColorBrush(Color.Parse("#557C8DA5")),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(Color.Parse("#22000000")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = title,
                                FontSize = 14,
                                FontWeight = FontWeight.SemiBold,
                                TextWrapping = TextWrapping.Wrap,
                            },
                            new TextBlock
                            {
                                Text = text,
                                FontSize = 12,
                                Opacity = 0.78,
                                TextWrapping = TextWrapping.Wrap,
                            },
                        },
                    },
                },
            },
        };
    }
}
