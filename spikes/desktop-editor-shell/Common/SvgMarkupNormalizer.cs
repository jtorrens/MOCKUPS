using System;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

internal static class SvgMarkupNormalizer
{
    public static string NormalizeTintableSvg(string markup)
    {
        var normalized = StripDeclarations(markup);
        normalized = NormalizeTintablePaint(normalized);
        return NormalizeSvgRoot(normalized);
    }

    public static string ApplyCurrentColorTint(string markup, string cssColor)
    {
        return markup.Replace("currentColor", cssColor, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripDeclarations(string markup)
    {
        var withoutXml = Regex.Replace(markup, @"<\?xml[\s\S]*?\?>", "", RegexOptions.IgnoreCase).Trim();
        return Regex.Replace(withoutXml, @"<!doctype[\s\S]*?>", "", RegexOptions.IgnoreCase).Trim();
    }

    private static string NormalizeTintablePaint(string markup)
    {
        var tinted = Regex.Replace(
            markup,
            "\\sfill=([\"'])(?!none\\1|transparent\\1|currentColor\\1)[^\"']*\\1",
            " fill=\"currentColor\"",
            RegexOptions.IgnoreCase);
        tinted = Regex.Replace(
            tinted,
            "\\sstroke=([\"'])(?!none\\1|transparent\\1|currentColor\\1)[^\"']*\\1",
            " stroke=\"currentColor\"",
            RegexOptions.IgnoreCase);
        tinted = Regex.Replace(
            tinted,
            "\\scolor=([\"'])(?!none\\1|transparent\\1|currentColor\\1)[^\"']*\\1",
            " color=\"currentColor\"",
            RegexOptions.IgnoreCase);
        tinted = Regex.Replace(
            tinted,
            "(?<!-)fill\\s*:\\s*(?!none\\b|transparent\\b|currentColor\\b)[^;\"]+",
            "fill:currentColor",
            RegexOptions.IgnoreCase);
        tinted = Regex.Replace(
            tinted,
            "(?<!-)stroke\\s*:\\s*(?!none\\b|transparent\\b|currentColor\\b)[^;\"]+",
            "stroke:currentColor",
            RegexOptions.IgnoreCase);
        return Regex.Replace(
            tinted,
            "(?<!-)color\\s*:\\s*(?!none\\b|transparent\\b|currentColor\\b)[^;\"]+",
            "color:currentColor",
            RegexOptions.IgnoreCase);
    }

    private static string NormalizeSvgRoot(string markup)
    {
        return Regex.Replace(
            markup,
            "<svg\\b([^>]*)>",
            (match) =>
            {
                var attrs = Regex.Replace(match.Groups[1].Value, "\\s(width|height|style|preserveAspectRatio)=([\"']).*?\\2", "", RegexOptions.IgnoreCase);
                if (!HasPaintValue(attrs, "fill"))
                {
                    attrs += " fill=\"currentColor\"";
                }

                if (!HasPaintValue(attrs, "color"))
                {
                    attrs += " color=\"currentColor\"";
                }

                return $"<svg{attrs} width=\"100%\" height=\"100%\" preserveAspectRatio=\"xMidYMid meet\" style=\"display:block;width:100%;height:100%;overflow:visible;\">";
            },
            RegexOptions.IgnoreCase);
    }

    private static bool HasPaintValue(string attributes, string name)
    {
        return Regex.IsMatch(attributes, $"\\s{Regex.Escape(name)}\\s*=", RegexOptions.IgnoreCase)
            || Regex.IsMatch(attributes, $"style\\s*=\\s*(['\"])(?:(?!\\1)[\\s\\S])*\\b{Regex.Escape(name)}\\s*:", RegexOptions.IgnoreCase);
    }
}
