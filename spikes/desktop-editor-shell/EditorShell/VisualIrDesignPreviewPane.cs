using Avalonia;
using Avalonia.Controls;
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

    public void Update(
        SpikeDatabase.DevicePreviewMetrics metrics,
        bool isDark,
        string themeName,
        string themeMode,
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
            return;
        }

        try
        {
            var colorVariant = themeMode == "dark" ? "set_night" : "set_day";
            var document = DesignPreviewToVisualIrBridge.Convert(payload, metrics, colorVariant);
            var rendered = _renderer.Render(document, new VisualIrRenderOptions(
                SelectedColorVariant: colorVariant,
                ShowBounds: true,
                ShowUnsupportedPlaceholders: true));

            Children.Add(DeviceFrame(
                metrics,
                document.Viewport.Width,
                document.Viewport.Height,
                rendered,
                panelText,
                themeName,
                themeMode,
                colorVariant,
                document,
                payload));
        }
        catch (System.Exception exception)
        {
            Children.Add(Placeholder(
                $"{payload.Name} · {payload.Kind}",
                $"Visual IR bridge failed: {exception.Message}"));
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
        string colorVariant,
        VisualIrDocument document,
        DesignPreviewPayload payload)
    {
        var scale = metrics.ScaleToPixels > 0 ? metrics.ScaleToPixels : 1;
        var canvas = new Canvas
        {
            Width = frameWidth,
            Height = frameHeight,
            ClipToBounds = true,
        };
        canvas.Children.Add(new Border
        {
            Width = frameWidth,
            Height = frameHeight,
            CornerRadius = new CornerRadius(metrics.CornerRadius / scale),
            ClipToBounds = true,
            Child = rendered,
        });
        canvas.Children.Add(new Border
        {
            Width = frameWidth,
            Height = frameHeight,
            BorderBrush = new SolidColorBrush(Color.Parse("#18181B")),
            BorderThickness = new Thickness(10),
            CornerRadius = new CornerRadius(metrics.CornerRadius / scale),
            IsHitTestVisible = false,
        });

        var meta = new TextBlock
        {
            Text = $"IR · {payload.Kind} · {metrics.Name} · {themeName} · {themeMode}{ColorSummary(document, payload.Kind, colorVariant)}",
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.Parse("#AA18181B")),
            FontSize = 10,
            Padding = new Thickness(8, 4),
        };
        Canvas.SetLeft(meta, 12);
        Canvas.SetTop(meta, frameHeight - 30);
        canvas.Children.Add(meta);

        return new Grid
        {
            Margin = new Thickness(18),
            Children =
            {
                new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = canvas,
                },
            },
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
