using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Preview;
using Mockups.DesktopEditorShell.Preview.Avalonia;
using Mockups.DesktopEditorShell.Preview.Bridges;

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

        var panelText = isDark ? "#E8EEF8" : "#172033";
        var panelBackground = isDark ? "#162238" : "#EDF4FC";
        Background = new SolidColorBrush(Color.Parse(panelBackground));

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

            Children.Add(DeviceFrame(metrics, rendered, panelText, themeName, themeMode, payload));
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
        Control rendered,
        string panelText,
        string themeName,
        string themeMode,
        DesignPreviewPayload payload)
    {
        var canvas = new Canvas
        {
            Width = metrics.CanvasWidth,
            Height = metrics.CanvasHeight,
            ClipToBounds = true,
        };
        canvas.Children.Add(rendered);
        canvas.Children.Add(new Border
        {
            Width = metrics.CanvasWidth,
            Height = metrics.CanvasHeight,
            BorderBrush = new SolidColorBrush(Color.Parse("#111827")),
            BorderThickness = new Thickness(10),
            CornerRadius = new CornerRadius(metrics.CornerRadius),
            IsHitTestVisible = false,
        });

        var meta = new TextBlock
        {
            Text = $"IR · {payload.Kind} · {metrics.Name} · {themeName} · {themeMode}",
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.Parse("#990F172A")),
            FontSize = 10,
            Padding = new Thickness(8, 4),
        };
        Canvas.SetLeft(meta, 12);
        Canvas.SetTop(meta, metrics.CanvasHeight - 30);
        canvas.Children.Add(meta);

        return new Grid
        {
            Margin = new Thickness(18),
            Children =
            {
                new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = canvas,
                },
            },
        };
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
