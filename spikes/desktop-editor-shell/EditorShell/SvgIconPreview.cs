using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class SvgIconPreview
{
    public static Control CreateIconThemePreview(SpikeDatabase database, string iconThemeId, string file, double size)
    {
        try
        {
            var path = database.ResolveIconThemeAssetPath(iconThemeId, file);
            if (!File.Exists(path)) return EditorIcons.Create(EditorIcons.Icon, size);

            return CreateFromSvg(File.ReadAllText(path), size);
        }
        catch
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }
    }

    public static Control CreateProjectIconTokenPreview(SpikeDatabase database, string projectId, string token, double size)
    {
        try
        {
            var firstToken = token
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstToken)) return EditorIcons.Create(EditorIcons.Icon, size);

            var path = database.ResolveIconTokenAssetPath(projectId, firstToken);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return EditorIcons.Create(EditorIcons.Icon, size);

            return CreateFromSvg(File.ReadAllText(path), size);
        }
        catch
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }
    }

    public static Control CreateSearchPreview(string previewUrl, double size)
    {
        const string prefix = "data:image/svg+xml;base64,";
        if (string.IsNullOrWhiteSpace(previewUrl) || !previewUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }

        try
        {
            var svg = Encoding.UTF8.GetString(Convert.FromBase64String(previewUrl[prefix.Length..]));
            return CreateFromSvg(svg, size);
        }
        catch
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }
    }

    public static Control CreateFromSvg(string svg, double size)
    {
        try
        {
            return CreateSvgPreview(svg, size) ?? EditorIcons.Create(EditorIcons.Icon, size);
        }
        catch
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }
    }

    private static Control? CreateSvgPreview(string svg, double size)
    {
        try
        {
            var viewBox = SvgViewBox(svg);
            var strokeMode = SvgUsesStroke(svg);
            var brush = new SolidColorBrush(Color.Parse("#F2F6FF"));
            var strokeThickness = SvgDouble(SvgAttribute(svg, "stroke-width"), 2);
            var viewportCanvas = new Canvas
            {
                Width = viewBox.Width,
                Height = viewBox.Height,
            };
            var drawingCanvas = new Canvas
            {
                Width = viewBox.Width,
                Height = viewBox.Height,
                RenderTransform = viewBox.X != 0 || viewBox.Y != 0
                    ? new TranslateTransform(-viewBox.X, -viewBox.Y)
                    : null,
            };
            viewportCanvas.Children.Add(drawingCanvas);
            var canvas = drawingCanvas;

            foreach (Match match in Regex.Matches(svg, "<path\\b[^>]*\\bd=\"([^\"]+)\"[^>]*/?>", RegexOptions.IgnoreCase))
            {
                AddSvgPath(canvas, match.Groups[1].Value, strokeMode, brush, strokeThickness);
            }

            foreach (Match match in Regex.Matches(svg, "<line\\b([^>]*)/?>", RegexOptions.IgnoreCase))
            {
                var attrs = match.Groups[1].Value;
                canvas.Children.Add(new Line
                {
                    StartPoint = new Point(SvgDouble(SvgAttribute(attrs, "x1"), 0), SvgDouble(SvgAttribute(attrs, "y1"), 0)),
                    EndPoint = new Point(SvgDouble(SvgAttribute(attrs, "x2"), 0), SvgDouble(SvgAttribute(attrs, "y2"), 0)),
                    Stroke = brush,
                    StrokeThickness = strokeThickness,
                    StrokeLineCap = PenLineCap.Round,
                });
            }

            foreach (Match match in Regex.Matches(svg, "<rect\\b([^>]*)/?>", RegexOptions.IgnoreCase))
            {
                var attrs = match.Groups[1].Value;
                var rect = new Rectangle
                {
                    Width = SvgDouble(SvgAttribute(attrs, "width"), 0),
                    Height = SvgDouble(SvgAttribute(attrs, "height"), 0),
                    RadiusX = SvgDouble(SvgAttribute(attrs, "rx"), 0),
                    RadiusY = SvgDouble(SvgAttribute(attrs, "ry"), SvgDouble(SvgAttribute(attrs, "rx"), 0)),
                    Stroke = strokeMode ? brush : null,
                    Fill = strokeMode ? null : brush,
                    StrokeThickness = strokeThickness,
                };
                Canvas.SetLeft(rect, SvgDouble(SvgAttribute(attrs, "x"), 0));
                Canvas.SetTop(rect, SvgDouble(SvgAttribute(attrs, "y"), 0));
                canvas.Children.Add(rect);
            }

            foreach (Match match in Regex.Matches(svg, "<circle\\b([^>]*)/?>", RegexOptions.IgnoreCase))
            {
                var attrs = match.Groups[1].Value;
                var radius = SvgDouble(SvgAttribute(attrs, "r"), 0);
                var circle = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Stroke = strokeMode ? brush : null,
                    Fill = strokeMode && radius <= strokeThickness ? brush : strokeMode ? null : brush,
                    StrokeThickness = strokeThickness,
                };
                Canvas.SetLeft(circle, SvgDouble(SvgAttribute(attrs, "cx"), 0) - radius);
                Canvas.SetTop(circle, SvgDouble(SvgAttribute(attrs, "cy"), 0) - radius);
                canvas.Children.Add(circle);
            }

            foreach (Match match in Regex.Matches(svg, "<(?:polyline|polygon)\\b([^>]*)/?>", RegexOptions.IgnoreCase))
            {
                var points = SvgAttribute(match.Groups[1].Value, "points");
                var pathData = SvgPointsToPath(points, match.Value.StartsWith("<polygon", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(pathData))
                {
                    AddSvgPath(canvas, pathData, strokeMode, brush, strokeThickness);
                }
            }

            if (canvas.Children.Count == 0) return null;

            return new Viewbox
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                Child = viewportCanvas,
            };
        }
        catch
        {
            return null;
        }
    }

    private static void AddSvgPath(Canvas canvas, string data, bool strokeMode, IBrush brush, double strokeThickness)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        canvas.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Stroke = strokeMode ? brush : null,
            Fill = strokeMode ? null : brush,
            StrokeThickness = strokeThickness,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        });
    }

    private static Rect SvgViewBox(string svg)
    {
        var raw = SvgAttribute(svg, "viewBox");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var values = Regex.Split(raw.Trim(), "[,\\s]+")
                .Where((value) => !string.IsNullOrWhiteSpace(value))
                .Select((value) => SvgDouble(value, 0))
                .ToArray();
            if (values.Length == 4 && values[2] > 0 && values[3] > 0)
            {
                return new Rect(values[0], values[1], values[2], values[3]);
            }
        }

        return new Rect(0, 0, SvgDouble(SvgAttribute(svg, "width"), 24), SvgDouble(SvgAttribute(svg, "height"), 24));
    }

    private static bool SvgUsesStroke(string svg)
    {
        var stroke = SvgAttribute(svg, "stroke");
        var fill = SvgAttribute(svg, "fill");
        return !string.IsNullOrWhiteSpace(stroke)
            && !stroke.Equals("none", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(fill) || fill.Equals("none", StringComparison.OrdinalIgnoreCase));
    }

    private static string SvgAttribute(string text, string name)
    {
        var match = Regex.Match(text, $"\\b{Regex.Escape(name)}\\s*=\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static double SvgDouble(string value, double fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        value = Regex.Replace(value.Trim(), "[a-z%]+$", "", RegexOptions.IgnoreCase);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static string SvgPointsToPath(string points, bool close)
    {
        var values = Regex.Split(points.Trim(), "[,\\s]+")
            .Where((value) => !string.IsNullOrWhiteSpace(value))
            .Select((value) => SvgDouble(value, 0))
            .ToArray();
        if (values.Length < 4) return "";

        var builder = new StringBuilder($"M {values[0].ToString(CultureInfo.InvariantCulture)} {values[1].ToString(CultureInfo.InvariantCulture)}");
        for (var i = 2; i + 1 < values.Length; i += 2)
        {
            builder.Append(" L ");
            builder.Append(values[i].ToString(CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(values[i + 1].ToString(CultureInfo.InvariantCulture));
        }
        if (close) builder.Append(" Z");
        return builder.ToString();
    }
}
