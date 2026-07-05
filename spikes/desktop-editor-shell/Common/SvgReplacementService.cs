using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

internal static class SvgReplacementService
{
    public sealed record Geometry(double MinX, double MinY, double Width, double Height, double CenterX, double CenterY, string Label);
    public sealed record TransformOptions(string Mode, double Padding, double CornerRadius, double StrokeWidth, double Scale, double RotationDegrees, double OffsetX, double OffsetY, string? TargetSvgText = null);

    public static string Normalize(string value)
    {
        var trimmed = value.Trim('\uFEFF').Trim();
        var svgStart = trimmed.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        if (svgStart < 0) return trimmed;

        var svgText = trimmed[svgStart..].Trim();
        var lastClosingSvg = svgText.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
        return lastClosingSvg < 0
            ? svgText
            : svgText[..(lastClosingSvg + "</svg>".Length)].Trim();
    }

    public static string Validate(string value)
    {
        var svgText = Normalize(value);
        if (string.IsNullOrWhiteSpace(svgText))
        {
            throw new InvalidOperationException("SVG content is required.");
        }

        if (!Regex.IsMatch(svgText, "^<svg[\\s>]", RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException("SVG content must start with an <svg> element.");
        }

        if (!Regex.IsMatch(svgText, "(</svg>\\s*$)|(/>\\s*$)", RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException("SVG content must close the <svg> element.");
        }

        if (Regex.IsMatch(svgText, "<\\s*script\\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(svgText, "<\\s*foreignObject\\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(svgText, "\\bon[a-z]+\\s*=", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(svgText, "\\b(?:href|xlink:href)\\s*=\\s*[\"']\\s*javascript:", RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException("SVG content contains unsupported executable markup.");
        }

        return svgText;
    }

    public static Geometry? TryGeometry(string value)
    {
        try
        {
            var parts = Parts(value);
            return parts.Geometry;
        }
        catch
        {
            return null;
        }
    }

    public static string Transform(string value, TransformOptions options)
    {
        var source = Parts(NormalizePaintToBlack(value));
        var target = string.IsNullOrWhiteSpace(options.TargetSvgText) ? source : Parts(options.TargetSvgText);
        var mode = options.Mode.Equals("negative", StringComparison.OrdinalIgnoreCase) ? "negative" : "positive";
        var scale = Clamp(options.Scale, 0.05, 8);
        var rotation = Trim(options.RotationDegrees);
        var padding = Math.Max(0, options.Padding);
        var cornerRadius = Math.Max(0, options.CornerRadius);
        var strokeWidth = Clamp(options.StrokeWidth, 0, 32);
        var fitWidth = Math.Max(0.01, target.Geometry.Width - padding * 2);
        var fitHeight = Math.Max(0.01, target.Geometry.Height - padding * 2);
        var fitScale = Math.Min(fitWidth / source.Geometry.Width, fitHeight / source.Geometry.Height);
        var transform = string.Join(" ", new[]
        {
            $"translate({Trim(target.Geometry.CenterX + options.OffsetX)} {Trim(target.Geometry.CenterY + options.OffsetY)})",
            $"rotate({rotation})",
            $"scale({Trim(scale)})",
            $"scale({Trim(fitScale)})",
            $"translate({Trim(-source.Geometry.CenterX)} {Trim(-source.Geometry.CenterY)})",
        });
        var namespaces = string.IsNullOrWhiteSpace(source.NamespaceAttributes) ? "" : $" {source.NamespaceAttributes}";
        var presentationAttributes = strokeWidth == 0
            ? EffectivePresentationAttributes(source.Attributes)
            : OverrideStrokeWidth(EffectivePresentationAttributes(source.Attributes), strokeWidth);
        var sourceSvg = $"<svg{namespaces} x=\"{Trim(source.Geometry.MinX)}\" y=\"{Trim(source.Geometry.MinY)}\" width=\"{Trim(source.Geometry.Width)}\" height=\"{Trim(source.Geometry.Height)}\" viewBox=\"{source.ViewBox}\" overflow=\"visible\" color=\"#000\"{presentationAttributes}>\n{source.Body.Trim()}\n    </svg>";

        if (mode == "negative")
        {
            var backgroundX = Trim(target.Geometry.MinX + padding);
            var backgroundY = Trim(target.Geometry.MinY + padding);
            var backgroundWidth = Trim(fitWidth);
            var backgroundHeight = Trim(fitHeight);
            var radius = Trim(Math.Min(cornerRadius, Math.Min(fitWidth / 2, fitHeight / 2)));
            var radiusAttributes = radius == "0" ? "" : $" rx=\"{radius}\" ry=\"{radius}\"";
            return $"<svg{target.Attributes}>\n" +
                "  <defs>\n" +
                $"    <mask id=\"mockups-negative-cutout\" maskUnits=\"userSpaceOnUse\" x=\"{backgroundX}\" y=\"{backgroundY}\" width=\"{backgroundWidth}\" height=\"{backgroundHeight}\">\n" +
                $"      <rect x=\"{backgroundX}\" y=\"{backgroundY}\" width=\"{backgroundWidth}\" height=\"{backgroundHeight}\"{radiusAttributes} fill=\"#fff\"/>\n" +
                $"      <g data-mockups-transform=\"fit-center-scale-rotate\" data-mockups-fit-scale=\"{Trim(fitScale)}\" data-mockups-padding=\"{Trim(padding)}\" transform=\"{transform}\">\n" +
                $"        {sourceSvg}\n" +
                "      </g>\n" +
                "    </mask>\n" +
                "  </defs>\n" +
                $"  <rect x=\"{backgroundX}\" y=\"{backgroundY}\" width=\"{backgroundWidth}\" height=\"{backgroundHeight}\"{radiusAttributes} fill=\"#000\" mask=\"url(#mockups-negative-cutout)\"/>\n" +
                "</svg>";
        }

        return $"<svg{target.Attributes}>\n" +
            $"  <g data-mockups-transform=\"fit-center-scale-rotate\" data-mockups-fit-scale=\"{Trim(fitScale)}\" data-mockups-padding=\"{Trim(padding)}\" transform=\"{transform}\">\n" +
            $"    {sourceSvg}\n" +
            "  </g>\n" +
            "</svg>";
    }

    public static string NormalizePaintToBlack(string value)
    {
        var svgText = Validate(value);
        var normalized = RemoveNonSvgNamespacedAttributes(svgText);
        normalized = Regex.Replace(
            normalized,
            "\\s(fill|stroke)\\s*=\\s*(['\"])(?!none\\b|currentColor\\b)[^'\"]*\\2",
            (match) => $" {match.Groups[1].Value}={match.Groups[2].Value}#000{match.Groups[2].Value}",
            RegexOptions.IgnoreCase);
        normalized = Regex.Replace(
            normalized,
            "style\\s*=\\s*(['\"])(.*?)\\1",
            (match) =>
            {
                var quote = match.Groups[1].Value;
                var style = Regex.Replace(
                    match.Groups[2].Value,
                    "(^|;)\\s*(fill|stroke)\\s*:\\s*(?!none\\b|currentColor\\b)[^;]+",
                    (styleMatch) => $"{styleMatch.Groups[1].Value}{styleMatch.Groups[2].Value}:#000",
                    RegexOptions.IgnoreCase);
                return $"style={quote}{style}{quote}";
            },
            RegexOptions.IgnoreCase);
        return NormalizeCompoundPathFillRules(normalized);
    }

    private sealed record SvgParts(string Attributes, string Body, Geometry Geometry, string NamespaceAttributes, string ViewBox);

    private static SvgParts Parts(string value)
    {
        var svgText = Validate(value);
        var openClose = Regex.Match(svgText, "^<svg\\b([^>]*?)>([\\s\\S]*)</svg>\\s*$", RegexOptions.IgnoreCase);
        var selfClosing = Regex.Match(svgText, "^<svg\\b([^>]*?)/>\\s*$", RegexOptions.IgnoreCase);
        var attributes = openClose.Success ? openClose.Groups[1].Value : selfClosing.Success ? selfClosing.Groups[1].Value : null;
        var body = openClose.Success ? openClose.Groups[2].Value : "";
        if (attributes is null)
        {
            throw new InvalidOperationException("SVG content could not be parsed.");
        }

        var geometry = ViewBoxGeometry(attributes) ?? WidthHeightGeometry(attributes)
            ?? throw new InvalidOperationException("SVG content needs a viewBox or width/height.");
        var viewBox = Attribute(attributes, "viewBox");
        if (string.IsNullOrWhiteSpace(viewBox))
        {
            viewBox = $"{Trim(geometry.MinX)} {Trim(geometry.MinY)} {Trim(geometry.Width)} {Trim(geometry.Height)}";
        }

        return new SvgParts(attributes, body, geometry, NamespaceAttributes(attributes), viewBox);
    }

    private static Geometry? ViewBoxGeometry(string attributes)
    {
        var raw = Attribute(attributes, "viewBox");
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var values = Regex.Split(raw.Trim(), "[,\\s]+")
            .Where((value) => !string.IsNullOrWhiteSpace(value))
            .Select(ParseDouble)
            .ToArray();
        if (values.Length != 4 || values.Any(double.IsNaN) || values[2] <= 0 || values[3] <= 0) return null;
        return new Geometry(values[0], values[1], values[2], values[3], values[0] + values[2] / 2, values[1] + values[3] / 2, $"{Trim(values[2])} x {Trim(values[3])}");
    }

    private static Geometry? WidthHeightGeometry(string attributes)
    {
        var width = ParseDouble(Attribute(attributes, "width"));
        var height = ParseDouble(Attribute(attributes, "height"));
        if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0) return null;
        return new Geometry(0, 0, width, height, width / 2, height / 2, $"{Trim(width)} x {Trim(height)}");
    }

    private static string Attribute(string text, string name)
    {
        var match = Regex.Match(text, $"\\s{Regex.Escape(name)}\\s*=\\s*(['\"])(.*?)\\1", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[2].Value : "";
    }

    private static string NamespaceAttributes(string attributes)
    {
        return string.Join(" ", Regex.Matches(attributes, "\\s+xmlns(?::[\\w.-]+)?\\s*=\\s*(['\"]).*?\\1", RegexOptions.IgnoreCase).Select((match) => match.Value.Trim()));
    }

    private static string PresentationAttributes(string attributes)
    {
        var names = new[]
        {
            "fill",
            "stroke",
            "stroke-width",
            "stroke-linecap",
            "stroke-linejoin",
            "stroke-miterlimit",
            "fill-rule",
            "clip-rule",
            "opacity",
            "style",
        };
        return string.Concat(names.Select((name) => AttributeMatch(attributes, name)));
    }

    private static string EffectivePresentationAttributes(string attributes)
    {
        var presentation = PresentationAttributes(attributes);
        if (!HasPaintValue(attributes, "fill"))
        {
            presentation += " fill=\"#000\"";
        }

        if (!HasPaintValue(attributes, "stroke"))
        {
            presentation += " stroke=\"none\"";
        }

        return presentation;
    }

    private static bool HasPaintValue(string attributes, string name)
    {
        return Regex.IsMatch(attributes, $"\\s{Regex.Escape(name)}\\s*=", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(attributes, $"style\\s*=\\s*(['\"])(?:(?!\\1)[\\s\\S])*\\b{Regex.Escape(name)}\\s*:", RegexOptions.IgnoreCase);
    }

    private static string AttributeMatch(string text, string name)
    {
        var match = Regex.Match(text, $"\\s{Regex.Escape(name)}\\s*=\\s*(['\"])(.*?)\\1", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : "";
    }

    private static string OverrideStrokeWidth(string attributes, double strokeWidth)
    {
        var next = Regex.Replace(attributes, "\\sstroke-width\\s*=\\s*(['\"]).*?\\1", "", RegexOptions.IgnoreCase);
        if (!Regex.IsMatch(next, "\\sstroke\\s*=", RegexOptions.IgnoreCase))
        {
            next += " stroke=\"currentColor\"";
        }

        if (!Regex.IsMatch(next, "\\sfill\\s*=", RegexOptions.IgnoreCase))
        {
            next += " fill=\"none\"";
        }

        return $"{next} stroke-width=\"{Trim(strokeWidth)}\"";
    }

    private static string RemoveNonSvgNamespacedAttributes(string svgText)
    {
        return Regex.Replace(svgText, "\\s(?!xmlns\\b|xmlns:|xml:|xlink:)[A-Za-z_][\\w.-]*:[\\w.-]+\\s*=\\s*(['\"]).*?\\1", "", RegexOptions.IgnoreCase);
    }

    private static string NormalizeCompoundPathFillRules(string svgText)
    {
        return Regex.Replace(svgText, "<path\\b[^>]*>", (match) =>
        {
            var pathTag = match.Value;
            var pathData = Attribute(pathTag, "d");
            var moveCommandCount = Regex.Matches(pathData, "[Mm]").Count;
            return moveCommandCount < 2
                ? pathTag
                : AddPathRuleAttribute(AddPathRuleAttribute(pathTag, "fill-rule"), "clip-rule");
        }, RegexOptions.IgnoreCase);
    }

    private static string AddPathRuleAttribute(string pathTag, string attribute)
    {
        if (Regex.IsMatch(pathTag, $"\\s{Regex.Escape(attribute)}\\s*=", RegexOptions.IgnoreCase)) return pathTag;
        return Regex.Replace(pathTag, "\\s*/?>$", (match) => $" {attribute}=\"evenodd\"{(match.Value.Contains('/') ? "/>" : ">")}");
    }

    private static double ParseDouble(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return double.NaN;

        var match = Regex.Match(value.Trim(), "^[+-]?(?:\\d+\\.?\\d*|\\.\\d+)(?:[eE][+-]?\\d+)?");
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : double.NaN;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return min;
        return Math.Min(max, Math.Max(min, value));
    }

    private static string Trim(double value)
    {
        return Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
    }
}
