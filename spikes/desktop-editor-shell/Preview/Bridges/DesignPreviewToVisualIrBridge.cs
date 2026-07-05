using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.VisualIr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

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
        var height = Number(layout, "height", metrics.StatusBarHeight > 0 ? metrics.StatusBarHeight : 54);
        var itemSize = Number(layout, "itemSize", 18);
        var gap = Number(layout, "gap", 6);
        var sidePadding = Number(layout, "sidePadding", 24);
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
        children.AddRange(StatusItemsForZone(items.Where((item) => item.Zone == "left"), "left", itemSize, gap, sidePadding, bounds));
        children.AddRange(StatusItemsForZone(items.Where((item) => item.Zone == "right"), "right", itemSize, gap, sidePadding, bounds));

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
            yield return StatusItem(item, x, y, width, itemSize);
            x += width + gap;
        }
    }

    private static VisualIrNode StatusItem(
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

        return new VisualIrSvgNode
        {
            Id = $"statusBar.item.{item.Id}",
            Bounds = new VisualIrRect(x, y, width, height),
            Markup = PlaceholderSvg(item.Kind, item.TokenOrLabel),
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
        var height = Number(layout, "height", 34);
        var itemSize = Number(layout, "itemSize", 18);
        var sidePadding = Number(layout, "sidePadding", 40);
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
            children.AddRange(NavigationItemsForZone(items.Where((item) => item.Zone == zone), zone, itemSize, sidePadding, bounds));
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
        VisualIrRect barBounds)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            yield break;
        }

        const double gap = 10;
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
            yield return new VisualIrSvgNode
            {
                Id = $"navigationBar.item.{item.Id}",
                Bounds = new VisualIrRect(x, y, itemSize, itemSize),
                Markup = PlaceholderSvg(item.Kind, item.Label),
                Fit = "contain",
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
                Number(item, "order", index * 10)))
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

    private static string PlaceholderSvg(string kind, string label)
    {
        var escapedLabel = label
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100%" height="100%">
              <rect x="8" y="8" width="84" height="84" rx="16" fill="none" stroke="currentColor" stroke-width="8"/>
              <text x="50" y="56" text-anchor="middle" font-size="18" fill="currentColor">{escapedLabel[..Math.Min(escapedLabel.Length, 4)]}</text>
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

        return double.TryParse(node.ToString(), out var parsed) ? parsed : fallback;
    }

    private sealed record PreviewItem(
        string Id,
        string Label,
        string Kind,
        string Value,
        string Token,
        string Zone,
        double Order)
    {
        public string TokenOrLabel => string.IsNullOrWhiteSpace(Token) ? Label : Token;
    }
}
