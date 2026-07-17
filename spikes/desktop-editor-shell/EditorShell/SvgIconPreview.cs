using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Path = Avalonia.Controls.Shapes.Path;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class SvgIconPreview
{
    private sealed record CachedSvg(DateTime LastWriteUtc, long Length, string Content);
    private static readonly ConcurrentDictionary<string, CachedSvg> SvgCache = new(StringComparer.Ordinal);

    public static Control CreateIconThemePreview(SpikeDatabase database, string iconThemeId, string file, double size)
    {
        try
        {
            var path = database.ResolveIconThemeAssetPath(iconThemeId, file);
            if (!File.Exists(path)) return EditorIcons.Create(EditorIcons.Icon, size);

            return CreateFromSvg(ReadSvg(path), size);
        }
        catch
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }
    }

    public static Control CreateIconTokenPreview(SpikeDatabase database, string iconThemeId, string token, double size)
    {
        return CreateIconTokenPreview(
            token,
            size,
            (singleToken) =>
            {
                if (string.IsNullOrWhiteSpace(iconThemeId)) return null;
                var icon = database.GetIconThemeTokens(iconThemeId)
                    .FirstOrDefault((candidate) => candidate.Token == singleToken);
                return icon is null
                    ? null
                    : database.ResolveIconThemeAssetPath(iconThemeId, icon.File);
            });
    }

    public static Control CreateIconTokenPreview(
        string token,
        double size,
        Func<string, string?> resolveAssetPath)
    {
        try
        {
            var firstToken = token
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstToken)) return EditorIcons.Create(EditorIcons.Icon, size);

            var path = resolveAssetPath(firstToken);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return EditorIcons.Create(EditorIcons.Icon, size);

            return CreateFromSvg(ReadSvg(path), size);
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
            return CreateLightweightSvgPreview(svg, size) ?? EditorIcons.Create(EditorIcons.Icon, size);
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
            return CreateLightweightSvgPreview(svg, size) ?? EditorIcons.Create(EditorIcons.Icon, size);
        }
        catch
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }
    }

    private static string ReadSvg(string path)
    {
        var info = new FileInfo(path);
        if (SvgCache.TryGetValue(path, out var cached)
            && cached.LastWriteUtc == info.LastWriteTimeUtc
            && cached.Length == info.Length)
        {
            return cached.Content;
        }

        var content = File.ReadAllText(path);
        SvgCache[path] = new CachedSvg(info.LastWriteTimeUtc, info.Length, content);
        return content;
    }

    private static Control? CreateLightweightSvgPreview(string svg, double size)
    {
        var validatedSvg = SvgReplacementService.Validate(svg);
        var geometry = SvgReplacementService.TryGeometry(validatedSvg);
        if (geometry is null) return null;

        var document = XDocument.Parse(validatedSvg, LoadOptions.None);
        var root = document.Root;
        if (root is null) return null;

        var canvas = new Canvas
        {
            Width = geometry.Width,
            Height = geometry.Height,
            IsHitTestVisible = false,
        };
        var brush = IconBrush();
        var hasShapes = false;
        foreach (var element in root.Descendants())
        {
            var shape = CreateShape(element, root, geometry, brush);
            if (shape is null) continue;

            hasShapes = true;
            canvas.Children.Add(shape);
        }

        if (!hasShapes) return null;

        return new Border
        {
            Width = size,
            Height = size,
            ClipToBounds = true,
            Child = new Viewbox
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                Child = canvas,
            },
        };
    }

    private static Shape? CreateShape(XElement element, XElement root, SvgReplacementService.Geometry geometry, IBrush brush)
    {
        var name = element.Name.LocalName.ToLowerInvariant();
        return name switch
        {
            "path" => CreatePathShape(Attribute(element, "d"), element, root, geometry, brush),
            "line" => CreatePathShape(
                $"M {Number(element, "x1")} {Number(element, "y1")} L {Number(element, "x2")} {Number(element, "y2")}",
                element,
                root,
                geometry,
                brush),
            "polyline" => CreatePathShape(PointsPath(Attribute(element, "points"), false), element, root, geometry, brush),
            "polygon" => CreatePathShape(PointsPath(Attribute(element, "points"), true), element, root, geometry, brush),
            "rect" => CreatePathShape(RectPath(element), element, root, geometry, brush),
            "circle" => CreateGeometryShape(
                new EllipseGeometry(new Rect(
                    Number(element, "cx") - Number(element, "r"),
                    Number(element, "cy") - Number(element, "r"),
                    Number(element, "r") * 2,
                    Number(element, "r") * 2)),
                element,
                root,
                geometry,
                brush),
            "ellipse" => CreateGeometryShape(
                new EllipseGeometry(new Rect(
                    Number(element, "cx") - Number(element, "rx"),
                    Number(element, "cy") - Number(element, "ry"),
                    Number(element, "rx") * 2,
                    Number(element, "ry") * 2)),
                element,
                root,
                geometry,
                brush),
            _ => null,
        };
    }

    private static Shape? CreatePathShape(string data, XElement element, XElement root, SvgReplacementService.Geometry geometry, IBrush brush)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        try
        {
            return CreateGeometryShape(Geometry.Parse(data), element, root, geometry, brush);
        }
        catch
        {
            return null;
        }
    }

    private static Path CreateGeometryShape(Geometry data, XElement element, XElement root, SvgReplacementService.Geometry geometry, IBrush brush)
    {
        var path = new Path
        {
            Data = data,
            RenderTransform = new TranslateTransform(-geometry.MinX, -geometry.MinY),
            StrokeThickness = StrokeThickness(element, root),
            StrokeLineCap = LineCap(element, root),
            StrokeJoin = LineJoin(element, root),
        };

        if (!IsNone(EffectiveAttribute(element, root, "fill", "currentColor")))
        {
            path.Fill = brush;
        }

        if (!IsNone(EffectiveAttribute(element, root, "stroke", "none")))
        {
            path.Stroke = brush;
        }

        return path;
    }

    private static IBrush IconBrush()
    {
        return new SolidColorBrush(EditorSukiWindowTheme.IsDark(null)
            ? Color.Parse("#F3F5F8")
            : Color.Parse("#1E232B"));
    }

    private static string RectPath(XElement element)
    {
        var x = Number(element, "x");
        var y = Number(element, "y");
        var width = Number(element, "width");
        var height = Number(element, "height");
        if (width <= 0 || height <= 0) return "";

        return string.Create(CultureInfo.InvariantCulture, $"M {x} {y} H {x + width} V {y + height} H {x} Z");
    }

    private static string PointsPath(string points, bool close)
    {
        var values = points
            .Replace(',', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseNumber)
            .Where((value) => !double.IsNaN(value))
            .ToArray();
        if (values.Length < 4) return "";

        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"M {values[0]} {values[1]}");
        for (var i = 2; i + 1 < values.Length; i += 2)
        {
            builder.Append(CultureInfo.InvariantCulture, $" L {values[i]} {values[i + 1]}");
        }

        if (close) builder.Append(" Z");
        return builder.ToString();
    }

    private static string EffectiveAttribute(XElement element, XElement root, string name, string fallback)
    {
        var own = Attribute(element, name);
        if (!string.IsNullOrWhiteSpace(own)) return own;

        var rootValue = Attribute(root, name);
        return string.IsNullOrWhiteSpace(rootValue) ? fallback : rootValue;
    }

    private static string Attribute(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault((attribute) =>
            string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
    }

    private static double Number(XElement element, string name)
    {
        return ParseNumber(Attribute(element, name));
    }

    private static double ParseNumber(string value)
    {
        var cleaned = value.Trim().TrimEnd('p', 'x', '%');
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static double StrokeThickness(XElement element, XElement root)
    {
        var value = EffectiveAttribute(element, root, "stroke-width", "1");
        var parsed = ParseNumber(value);
        return parsed <= 0 ? 1 : parsed;
    }

    private static PenLineCap LineCap(XElement element, XElement root)
    {
        return EffectiveAttribute(element, root, "stroke-linecap", "") switch
        {
            "round" => PenLineCap.Round,
            "square" => PenLineCap.Square,
            _ => PenLineCap.Flat,
        };
    }

    private static PenLineJoin LineJoin(XElement element, XElement root)
    {
        return EffectiveAttribute(element, root, "stroke-linejoin", "") switch
        {
            "round" => PenLineJoin.Round,
            "bevel" => PenLineJoin.Bevel,
            _ => PenLineJoin.Miter,
        };
    }

    private static bool IsNone(string value)
    {
        return value.Equals("none", StringComparison.OrdinalIgnoreCase);
    }
}
