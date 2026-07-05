using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.VisualIr;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Preview.Bridges;

internal static class DesignPreviewToVisualIrBridge
{
    public static VisualIrDocument Convert(
        DesignPreviewPayload payload,
        SpikeDatabase.DevicePreviewMetrics metrics,
        string selectedColorVariant = "set_night")
    {
        var rootChildren = new List<VisualIrNode>
        {
            new VisualIrRectNode
            {
                Id = "device.screen",
                Bounds = new VisualIrRect(metrics.ScreenX, metrics.ScreenY, metrics.ScreenWidth, metrics.ScreenHeight),
                Fill = new VisualIrSolidPaint(VariantColor("#F7F9FC", "#101827")),
                Radius = metrics.CornerRadius,
            },
        };

        rootChildren.Add(payload.Kind switch
        {
            "statusBar" => StatusBar(payload, metrics),
            "navigationBar" => NavigationBar(payload, metrics),
            _ => UnsupportedPayload(payload, metrics),
        });

        var document = new VisualIrDocument
        {
            Viewport = new VisualIrViewport(metrics.CanvasWidth, metrics.CanvasHeight, metrics.ScaleToPixels),
            Resources = new VisualIrResources
            {
                ColorVariants = ["set_day", "set_night"],
                DefaultColorVariant = selectedColorVariant,
            },
            Root = new VisualIrGroupNode
            {
                Id = "root",
                Bounds = new VisualIrRect(0, 0, metrics.CanvasWidth, metrics.CanvasHeight),
                Children = rootChildren,
            },
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "DesignPreviewPayload",
                ["sourceKind"] = payload.Kind,
                ["name"] = payload.Name,
            },
        };
        VisualIrValidator.ThrowIfInvalid(document);
        return document;
    }

    private static VisualIrGroupNode StatusBar(
        DesignPreviewPayload payload,
        SpikeDatabase.DevicePreviewMetrics metrics)
    {
        var config = ParseObject(payload.ConfigJson);
        var layout = config["layout"] as JsonObject ?? [];
        var scale = RenderScale(metrics);
        var height = ScaledNumber(layout, "height", metrics.StatusBarHeight > 0 ? metrics.StatusBarHeight : 54 * scale, scale);
        var itemSize = ScaledNumber(layout, "itemSize", 18 * scale, scale);
        var gap = ScaledNumber(layout, "gap", 6 * scale, scale);
        var sidePadding = ScaledNumber(layout, "sidePadding", 24 * scale, scale);
        var bounds = new VisualIrRect(metrics.ScreenX, metrics.ScreenY, metrics.ScreenWidth, height);
        var children = new List<VisualIrNode>
        {
            new VisualIrRectNode
            {
                Id = "statusBar.background",
                Bounds = new VisualIrRect(0, 0, bounds.Width, bounds.Height),
                Fill = new VisualIrSolidPaint(VariantColor("#ffffff", "#10151f")),
            },
        };

        var items = Items(config)
            .Where((item) => item.Zone is "left" or "right")
            .OrderBy((item) => item.Order)
            .ToList();
        children.AddRange(StatusItemsForZone(payload, items.Where((item) => item.Zone == "left"), "left", itemSize, gap, sidePadding, bounds));
        children.AddRange(StatusItemsForZone(payload, items.Where((item) => item.Zone == "right"), "right", itemSize, gap, sidePadding, bounds));

        return new VisualIrGroupNode
        {
            Id = "statusBar",
            Bounds = bounds,
            Children = children,
            Metadata = new Dictionary<string, string>
            {
                ["legacyKind"] = payload.Kind,
            },
        };
    }

    private static IEnumerable<VisualIrNode> StatusItemsForZone(
        DesignPreviewPayload payload,
        IEnumerable<PreviewItem> items,
        string zone,
        double itemSize,
        double gap,
        double sidePadding,
        VisualIrRect barBounds)
    {
        var list = items.ToList();
        var widths = list.Select((item) => StatusItemWidth(item, itemSize)).ToList();
        var totalWidth = widths.Sum() + Math.Max(0, widths.Count - 1) * gap;
        var x = zone == "left" ? sidePadding : barBounds.Width - sidePadding - totalWidth;
        var y = (barBounds.Height - itemSize) / 2;

        for (var index = 0; index < list.Count; index++)
        {
            var item = list[index];
            var width = widths[index];
            yield return StatusItem(payload, item, x, y, width, itemSize);
            x += width + gap;
        }
    }

    private static VisualIrNode StatusItem(
        DesignPreviewPayload payload,
        PreviewItem item,
        double x,
        double y,
        double width,
        double height)
    {
        if (item.Kind == "text")
        {
            return new VisualIrTextNode
            {
                Id = $"statusBar.item.{item.Id}",
                Bounds = new VisualIrRect(x, y, width, height),
                Text = string.IsNullOrWhiteSpace(item.Value) ? item.Label : item.Value,
                Style = new VisualIrTextStyle
                {
                    Fill = new VisualIrSolidPaint(VariantColor("#111111", "#f7f7f7")),
                    FontFamily = "Inter",
                    FontSize = height,
                    FontWeight = 600,
                    LineHeight = height,
                },
                Metadata = MetadataFor(item),
            };
        }

        if (item.Kind == "generatedSignal")
        {
            var primitive = GeneratedSvgPrimitives.StatusSignal(NumberValue(item.Value, 0), height);
            return new VisualIrSvgNode
            {
                Id = $"statusBar.item.{item.Id}",
                Bounds = new VisualIrRect(x, y + (height - primitive.Height) / 2, primitive.Width, primitive.Height),
                Markup = primitive.Markup,
                Fit = "fill",
                Tint = new VisualIrSolidPaint(VariantColor("#111111", "#f7f7f7")),
                Metadata = MetadataFor(item),
            };
        }

        if (item.Kind == "generatedBattery")
        {
            var primitive = GeneratedSvgPrimitives.StatusBattery(NumberValue(item.Value, 100), item.Charging, height);
            return new VisualIrSvgNode
            {
                Id = $"statusBar.item.{item.Id}",
                Bounds = new VisualIrRect(x, y + (height - primitive.Height) / 2, primitive.Width, primitive.Height),
                Markup = primitive.Markup,
                Fit = "fill",
                Tint = new VisualIrSolidPaint(VariantColor("#111111", "#f7f7f7")),
                Metadata = MetadataFor(item),
            };
        }

        var iconMarkup = IconMarkup(payload, item.Token);
        if (!string.IsNullOrWhiteSpace(iconMarkup))
        {
            var glyph = new VisualIrSvgNode
            {
                Id = $"statusBar.item.{item.Id}.glyph",
                Bounds = new VisualIrRect(0, 0, height, height),
                Markup = iconMarkup,
                Fit = "contain",
                Tint = new VisualIrSolidPaint(VariantColor("#111111", "#f7f7f7")),
                Metadata = MetadataFor(item),
            };
            return new VisualIrGroupNode
            {
                Id = $"statusBar.item.{item.Id}",
                Bounds = new VisualIrRect(x, y, height, height),
                ClipRect = new VisualIrRect(0, 0, height, height),
                Children = [glyph],
                Metadata = MetadataFor(item),
            };
        }

        return new VisualIrSvgNode
        {
            Id = $"statusBar.item.{item.Id}",
            Bounds = new VisualIrRect(x, y, width, height),
            Markup = FallbackIconSvg(item.TokenOrLabel),
            Fit = "contain",
            Tint = new VisualIrSolidPaint(VariantColor("#111111", "#f7f7f7")),
            Metadata = MetadataFor(item),
        };
    }

    private static VisualIrGroupNode NavigationBar(
        DesignPreviewPayload payload,
        SpikeDatabase.DevicePreviewMetrics metrics)
    {
        var config = ParseObject(payload.ConfigJson);
        var layout = config["layout"] as JsonObject ?? [];
        var scale = RenderScale(metrics);
        var height = ScaledNumber(layout, "height", 34 * scale, scale);
        var itemSize = ScaledNumber(layout, "itemSize", 18 * scale, scale);
        var sidePadding = ScaledNumber(layout, "sidePadding", 40 * scale, scale);
        var gap = 10 * scale;
        var bounds = new VisualIrRect(metrics.ScreenX, metrics.ScreenY + metrics.ScreenHeight - height, metrics.ScreenWidth, height);
        var children = new List<VisualIrNode>
        {
            new VisualIrRectNode
            {
                Id = "navigationBar.background",
                Bounds = new VisualIrRect(0, 0, bounds.Width, bounds.Height),
                Fill = new VisualIrSolidPaint(VariantColor("#ffffff", "#10151f")),
            },
        };

        var items = Items(config)
            .Where((item) => item.Zone is "left" or "center" or "right")
            .OrderBy((item) => item.Order)
            .ToList();
        foreach (var zone in new[] { "left", "center", "right" })
        {
            children.AddRange(NavigationItemsForZone(items.Where((item) => item.Zone == zone), zone, itemSize, sidePadding, gap, bounds, layout));
        }

        return new VisualIrGroupNode
        {
            Id = "navigationBar",
            Bounds = bounds,
            Children = children,
            Metadata = new Dictionary<string, string>
            {
                ["legacyKind"] = payload.Kind,
            },
        };
    }

    private static IEnumerable<VisualIrNode> NavigationItemsForZone(
        IEnumerable<PreviewItem> items,
        string zone,
        double itemSize,
        double sidePadding,
        double gap,
        VisualIrRect barBounds,
        JsonObject layout)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            yield break;
        }

        var totalWidth = list.Count * itemSize + Math.Max(0, list.Count - 1) * gap;
        var x = zone switch
        {
            "left" => sidePadding,
            "center" => (barBounds.Width - totalWidth) / 2,
            _ => barBounds.Width - sidePadding - totalWidth,
        };
        var y = (barBounds.Height - itemSize) / 2;

        foreach (var item in list)
        {
            var primitive = GeneratedSvgPrimitives.NavigationButton(
                item.Kind,
                itemSize,
                Number(layout, "strokeWidth", 2),
                Number(layout, "cornerRadius", 3),
                Bool(layout, "filled", false));
            yield return new VisualIrSvgNode
            {
                Id = $"navigationBar.item.{item.Id}",
                Bounds = new VisualIrRect(x, y, primitive.Width, primitive.Height),
                Markup = primitive.Markup,
                Fit = "fill",
                Tint = new VisualIrSolidPaint(VariantColor("#111111", "#f7f7f7")),
                Metadata = MetadataFor(item),
            };
            x += itemSize + gap;
        }
    }

    private static VisualIrGroupNode UnsupportedPayload(
        DesignPreviewPayload payload,
        SpikeDatabase.DevicePreviewMetrics metrics)
    {
        return new VisualIrGroupNode
        {
            Id = $"unsupported.{payload.Kind}",
            Bounds = new VisualIrRect(metrics.ScreenX, metrics.ScreenY, metrics.ScreenWidth, metrics.ScreenHeight),
            Children =
            [
                new VisualIrTextNode
                {
                    Id = $"unsupported.{payload.Kind}.label",
                    Bounds = new VisualIrRect(24, 24, Math.Max(1, metrics.ScreenWidth - 48), 32),
                    Text = $"Unsupported design payload: {payload.Kind}",
                    Style = new VisualIrTextStyle
                    {
                        Fill = new VisualIrSolidPaint(VisualIrColor.Static("#ff00ff")),
                        FontFamily = "Inter",
                        FontSize = 16,
                        FontWeight = 700,
                    },
                },
            ],
        };
    }

    private static IReadOnlyList<PreviewItem> Items(JsonObject config)
    {
        if (config["items"] is not JsonArray array)
        {
            return [];
        }

        return array
            .OfType<JsonObject>()
            .Select((item, index) => new PreviewItem(
                String(item, "id", $"item_{index}"),
                String(item, "label", $"Item {index + 1}"),
                String(item, "kind", "text"),
                String(item, "value", ""),
                String(item, "token", ""),
                String(item, "zone", "off"),
                Number(item, "order", index * 10),
                Bool(item, "charging", false)))
            .ToList();
    }

    private static double StatusItemWidth(PreviewItem item, double itemSize)
    {
        return item.Kind switch
        {
            "generatedBattery" => itemSize * 1.55,
            "generatedSignal" => itemSize * 1.08,
            "iconToken" => itemSize,
            _ => Math.Max(itemSize, (string.IsNullOrWhiteSpace(item.Value) ? item.Label : item.Value).Length * itemSize * 0.58),
        };
    }

    private static double RenderScale(SpikeDatabase.DevicePreviewMetrics metrics)
    {
        return metrics.ScaleToPixels > 0 ? metrics.ScaleToPixels : 1;
    }

    private static double ScaledNumber(JsonObject json, string key, double fallback, double scale)
    {
        return json.TryGetPropertyValue(key, out var node)
            && node is not null
            && double.TryParse(node.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed * scale
            : fallback;
    }

    private static VisualIrVariantColor VariantColor(string setDay, string setNight)
    {
        return VisualIrColor.Variant(
            new Dictionary<string, string>
            {
                ["set_day"] = setDay,
                ["set_night"] = setNight,
            },
            setNight);
    }

    private static string? IconMarkup(DesignPreviewPayload payload, string token)
    {
        if (string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(payload.ProjectMediaRoot)
            || string.IsNullOrWhiteSpace(payload.IconAssetRoot))
        {
            return null;
        }

        try
        {
            var mapping = ParseObject(payload.IconMappingJson);
            var tokens = mapping["tokens"] as JsonObject;
            var tokenObject = tokens?[token] as JsonObject;
            var file = tokenObject is null ? "" : String(tokenObject, "file", "");
            if (string.IsNullOrWhiteSpace(file))
            {
                return null;
            }

            var path = Path.Combine(payload.ProjectMediaRoot, payload.IconAssetRoot, file);
            return File.Exists(path) ? NormalizeInlineSvg(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeInlineSvg(string markup)
    {
        var withoutDeclarations = Regex
            .Replace(markup, @"<\?xml[\s\S]*?\?>", "", RegexOptions.IgnoreCase)
            .Replace("<!DOCTYPE", "<!doctype", StringComparison.Ordinal)
            .Trim();
        withoutDeclarations = Regex.Replace(withoutDeclarations, @"<!doctype[\s\S]*?>", "", RegexOptions.IgnoreCase).Trim();
        var tinted = Regex.Replace(
            withoutDeclarations,
            "\\sfill=([\"'])(?!none\\1|transparent\\1|currentColor\\1)[^\"']*\\1",
            " fill=\"currentColor\"",
            RegexOptions.IgnoreCase);
        tinted = Regex.Replace(
            tinted,
            "\\sstroke=([\"'])(?!none\\1|transparent\\1|currentColor\\1)[^\"']*\\1",
            " stroke=\"currentColor\"",
            RegexOptions.IgnoreCase);
        tinted = Regex.Replace(tinted, "fill\\s*:\\s*(?!none\\b|transparent\\b|currentColor\\b)[^;\"]+", "fill:currentColor", RegexOptions.IgnoreCase);
        tinted = Regex.Replace(tinted, "stroke\\s*:\\s*(?!none\\b|transparent\\b|currentColor\\b)[^;\"]+", "stroke:currentColor", RegexOptions.IgnoreCase);
        return Regex.Replace(
            tinted,
            "<svg\\b([^>]*)>",
            (match) =>
            {
                var attrs = Regex.Replace(match.Groups[1].Value, "\\s(width|height|style|preserveAspectRatio)=([\"']).*?\\2", "", RegexOptions.IgnoreCase);
                return $"<svg{attrs} width=\"100%\" height=\"100%\" preserveAspectRatio=\"xMidYMid meet\" style=\"display:block;width:100%;height:100%;overflow:visible;\">";
            },
            RegexOptions.IgnoreCase);
    }

    private static string FallbackIconSvg(string label)
    {
        var escapedLabel = label
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100%" height="100%" style="display:block;overflow:visible">
              <circle cx="50" cy="50" r="30" fill="none" stroke="currentColor" stroke-width="8"/>
              <text x="50" y="57" text-anchor="middle" font-size="18" fill="currentColor">{escapedLabel[..Math.Min(escapedLabel.Length, 4)]}</text>
            </svg>
            """;
    }

    private static IReadOnlyDictionary<string, string> MetadataFor(PreviewItem item)
    {
        return new Dictionary<string, string>
        {
            ["legacyKind"] = item.Kind,
            ["legacyZone"] = item.Zone,
            ["legacyLabel"] = item.Label,
        };
    }

    private static JsonObject ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string String(JsonObject json, string key, string fallback)
    {
        return json.TryGetPropertyValue(key, out var node) && node is not null
            ? node.ToString()
            : fallback;
    }

    private static double Number(JsonObject json, string key, double fallback)
    {
        if (!json.TryGetPropertyValue(key, out var node) || node is null)
        {
            return fallback;
        }

        return double.TryParse(node.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static double NumberValue(string value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool Bool(JsonObject json, string key, bool fallback)
    {
        if (!json.TryGetPropertyValue(key, out var node) || node is null)
        {
            return fallback;
        }

        return bool.TryParse(node.ToString(), out var parsed) ? parsed : fallback;
    }

    private sealed record PreviewItem(
        string Id,
        string Label,
        string Kind,
        string Value,
        string Token,
        string Zone,
        double Order,
        bool Charging)
    {
        public string TokenOrLabel => string.IsNullOrWhiteSpace(Token) ? Label : Token;
    }
}
