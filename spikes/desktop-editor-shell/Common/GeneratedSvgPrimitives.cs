using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record GeneratedSvgPrimitive(string Markup, double Width, double Height);

internal static class GeneratedSvgPrimitives
{
    public static GeneratedSvgPrimitive StatusSignal(double bars, double itemSize)
    {
        var clampedBars = Math.Max(0, Math.Min(4, bars));
        var width = itemSize * 1.08;
        var height = itemSize * 0.85;
        var barWidth = itemSize * 0.18;
        var gap = itemSize * 0.12;
        var markup = "";
        for (var bar = 1; bar <= 4; bar++)
        {
            var barHeight = height * bar * 0.22;
            var barX = (bar - 1) * (barWidth + gap);
            var opacity = bar <= clampedBars ? 1 : 0.24;
            markup += $"""<rect x="{N(barX)}" y="{N(height - barHeight)}" width="{N(barWidth)}" height="{N(barHeight)}" rx="{N(itemSize * 0.06)}" fill="currentColor" opacity="{N(opacity)}"/>""";
        }

        return new GeneratedSvgPrimitive(
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {N(width)} {N(height)}" width="100%" height="100%" style="display:block;overflow:visible">{markup}</svg>""",
            width,
            height);
    }

    public static GeneratedSvgPrimitive StatusBattery(double level, bool charging, double itemSize)
    {
        var clampedLevel = Math.Max(0, Math.Min(100, level));
        var bodyWidth = itemSize * 1.55;
        var bodyHeight = itemSize * 0.72;
        var outlineWidth = itemSize * 0.11;
        var chargePadding = itemSize * 0.07;
        var capWidth = itemSize * 0.16;
        var capHeight = itemSize * 0.24;
        var innerX = outlineWidth;
        var innerY = outlineWidth;
        var innerWidth = bodyWidth - outlineWidth * 2;
        var innerHeight = bodyHeight - outlineWidth * 2;
        var chargeX = innerX + chargePadding;
        var chargeY = innerY + chargePadding;
        var chargeMaxWidth = Math.Max(0, innerWidth - chargePadding * 2);
        var chargeHeight = Math.Max(0, innerHeight - chargePadding * 2);
        var chargeWidth = chargeMaxWidth * (clampedLevel / 100);
        var capX = bodyWidth;
        var visualWidth = bodyWidth + capWidth;
        var visualWidthPercent = visualWidth / bodyWidth * 100;
        var outerRadius = itemSize * 0.18;
        var innerRadius = itemSize * 0.08;
        var chargingMarkup = charging
            ? $"""<path d="M58 0 L18 48 L45 48 L32 100 L84 38 L56 38 Z" fill="#34c759" transform="translate({N(bodyWidth * 0.36)} {N(-itemSize * 0.05)}) scale({N(itemSize * 0.8 / 100)} {N(itemSize * 0.82 / 100)})"/>"""
            : "";

        var markup = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {{N(visualWidth)}} {{N(bodyHeight)}}" width="{{N(visualWidthPercent)}}%" height="100%" style="display:block;overflow:visible">
              <path fill="currentColor" fill-rule="evenodd" d="
                M {{N(outerRadius)}} 0
                H {{N(bodyWidth - outerRadius)}}
                Q {{N(bodyWidth)}} 0 {{N(bodyWidth)}} {{N(outerRadius)}}
                V {{N(bodyHeight - outerRadius)}}
                Q {{N(bodyWidth)}} {{N(bodyHeight)}} {{N(bodyWidth - outerRadius)}} {{N(bodyHeight)}}
                H {{N(outerRadius)}}
                Q 0 {{N(bodyHeight)}} 0 {{N(bodyHeight - outerRadius)}}
                V {{N(outerRadius)}}
                Q 0 0 {{N(outerRadius)}} 0
                Z
                M {{N(innerX + innerRadius)}} {{N(innerY)}}
                H {{N(innerX + innerWidth - innerRadius)}}
                Q {{N(innerX + innerWidth)}} {{N(innerY)}} {{N(innerX + innerWidth)}} {{N(innerY + innerRadius)}}
                V {{N(innerY + innerHeight - innerRadius)}}
                Q {{N(innerX + innerWidth)}} {{N(innerY + innerHeight)}} {{N(innerX + innerWidth - innerRadius)}} {{N(innerY + innerHeight)}}
                H {{N(innerX + innerRadius)}}
                Q {{N(innerX)}} {{N(innerY + innerHeight)}} {{N(innerX)}} {{N(innerY + innerHeight - innerRadius)}}
                V {{N(innerY + innerRadius)}}
                Q {{N(innerX)}} {{N(innerY)}} {{N(innerX + innerRadius)}} {{N(innerY)}}
                Z"/>
              <rect x="{{N(chargeX)}}" y="{{N(chargeY)}}" width="{{N(chargeWidth)}}" height="{{N(chargeHeight)}}" rx="{{N(itemSize * 0.05)}}" fill="currentColor"/>
              <rect x="{{N(capX)}}" y="{{N(itemSize * 0.2)}}" width="{{N(capWidth)}}" height="{{N(capHeight)}}" rx="{{N(itemSize * 0.08)}}" fill="currentColor"/>
              {{chargingMarkup}}
            </svg>
            """;
        return new GeneratedSvgPrimitive(markup, bodyWidth, bodyHeight);
    }

    public static GeneratedSvgPrimitive NavigationButton(string role, double itemSize, double strokeWidth, double cornerRadius, bool filled)
    {
        var fill = filled ? "currentColor" : "none";
        var radius = Math.Clamp(cornerRadius, 0, 22);
        var common = $"vector-effect=\"non-scaling-stroke\" stroke=\"currentColor\" stroke-width=\"{N(strokeWidth)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"";
        var child = role switch
        {
            "generatedBack" => $"""<path d="{RoundedBackPath(radius)}" fill="{fill}" {common}/>""",
            "generatedRecents" => $"""<rect x="28" y="28" width="44" height="44" rx="{N(radius)}" ry="{N(radius)}" fill="{fill}" {common}/>""",
            _ => $"""<rect x="27" y="27" width="46" height="46" rx="{N(radius)}" ry="{N(radius)}" fill="{fill}" {common}/>""",
        };
        return new GeneratedSvgPrimitive(
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100%" height="100%" style="display:block;overflow:visible">{child}</svg>""",
            itemSize,
            itemSize);
    }

    private static string RoundedBackPath(double radius)
    {
        if (radius <= 0)
        {
            return "M 64 20 L 28 50 L 64 80 Z";
        }

        var points = new[] { (X: 64.0, Y: 20.0), (X: 28.0, Y: 50.0), (X: 64.0, Y: 80.0) };
        var segments = new string[points.Length];
        for (var index = 0; index < points.Length; index++)
        {
            var point = points[index];
            var previous = points[(index - 1 + points.Length) % points.Length];
            var next = points[(index + 1) % points.Length];
            var previousVector = (X: previous.X - point.X, Y: previous.Y - point.Y);
            var nextVector = (X: next.X - point.X, Y: next.Y - point.Y);
            var previousLength = Math.Sqrt(previousVector.X * previousVector.X + previousVector.Y * previousVector.Y);
            var nextLength = Math.Sqrt(nextVector.X * nextVector.X + nextVector.Y * nextVector.Y);
            var offset = Math.Min(radius, Math.Min(previousLength * 0.42, nextLength * 0.42));
            var previousPoint = (X: point.X + previousVector.X / previousLength * offset, Y: point.Y + previousVector.Y / previousLength * offset);
            var nextPoint = (X: point.X + nextVector.X / nextLength * offset, Y: point.Y + nextVector.Y / nextLength * offset);
            segments[index] = $"{(index == 0 ? "M" : "L")} {N(previousPoint.X)} {N(previousPoint.Y)} Q {N(point.X)} {N(point.Y)} {N(nextPoint.X)} {N(nextPoint.Y)}";
        }

        return string.Join(" ", segments) + " Z";
    }

    private static string N(double value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
