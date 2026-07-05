using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Preview.Resolved;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Preview.Resolvers;

internal static class DesignPreviewFrameResolver
{
    private sealed record DesignMetrics(
        double CanvasWidth,
        double CanvasHeight,
        double ScreenX,
        double ScreenY,
        double ScreenWidth,
        double ScreenHeight,
        double CornerRadius,
        double StatusBarHeight)
    {
        public static DesignMetrics From(SpikeDatabase.DevicePreviewMetrics metrics)
        {
            var scale = metrics.ScaleToPixels > 0 ? metrics.ScaleToPixels : 1;
            return new DesignMetrics(
                metrics.CanvasWidth / scale,
                metrics.CanvasHeight / scale,
                metrics.ScreenX / scale,
                metrics.ScreenY / scale,
                metrics.ScreenWidth / scale,
                metrics.ScreenHeight / scale,
                metrics.CornerRadius / scale,
                metrics.StatusBarHeight / scale);
        }
    }

    private static readonly ResolvedDesignPaint ScreenBackgroundPaint = ThemePaint(
        "theme.colors.background");

    private static readonly ResolvedDesignPaint StatusForegroundPaint = ThemePaint(
        "theme.statusBar.foreground");

    private static readonly ResolvedDesignPaint StatusBackgroundPaint = ThemePaint(
        "theme.statusBar.background");

    private static readonly ResolvedDesignPaint NavigationForegroundPaint = ThemePaint(
        "theme.navigationBar.foreground");

    private static readonly ResolvedDesignPaint NavigationBackgroundPaint = ThemePaint(
        "theme.navigationBar.background");

    public static ResolvedDesignFrame Resolve(
        DesignPreviewPayload payload,
        SpikeDatabase.DevicePreviewMetrics metrics)
    {
        var designMetrics = DesignMetrics.From(metrics);
        var rootChildren = new List<ResolvedDesignNode>
        {
            new ResolvedDesignRectNode
            {
                Id = "device.screen",
                Bounds = new DesignRect(designMetrics.ScreenX, designMetrics.ScreenY, designMetrics.ScreenWidth, designMetrics.ScreenHeight),
                Fill = ScreenBackgroundPaint,
                Radius = designMetrics.CornerRadius,
            },
        };

        rootChildren.Add(payload.Kind switch
        {
            "statusBar" => StatusBar(payload, designMetrics),
            "navigationBar" => NavigationBar(payload, designMetrics),
            _ => UnsupportedPayload(payload, designMetrics),
        });

        return new ResolvedDesignFrame
        {
            Width = designMetrics.CanvasWidth,
            Height = designMetrics.CanvasHeight,
            Children = rootChildren,
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "DesignPreviewPayload",
                ["sourceKind"] = payload.Kind,
                ["name"] = payload.Name,
                ["coordinateSpace"] = "design",
                ["bridge"] = "legacy-design-preview",
            },
        };
    }

    private static ResolvedDesignGroupNode StatusBar(
        DesignPreviewPayload payload,
        DesignMetrics metrics)
    {
        var config = ParseObject(payload.ConfigJson);
        var layout = config["layout"] as JsonObject ?? [];
        var height = Number(layout, "height", metrics.StatusBarHeight > 0 ? metrics.StatusBarHeight : 54);
        var itemSize = Number(layout, "itemSize", 18);
        var gap = Number(layout, "gap", 6);
        var sidePadding = Number(layout, "sidePadding", 24);
        var bounds = new DesignRect(metrics.ScreenX, metrics.ScreenY, metrics.ScreenWidth, height);
        var children = new List<ResolvedDesignNode>
        {
            new ResolvedDesignRectNode
            {
                Id = "statusBar.background",
                Bounds = new DesignRect(0, 0, bounds.Width, bounds.Height),
                Fill = StatusBackgroundPaint,
            },
        };

        var items = Items(config)
            .Where((item) => item.Zone is "left" or "right")
            .OrderBy((item) => item.Order)
            .ToList();
        children.AddRange(StatusItemsForZone(payload, items.Where((item) => item.Zone == "left"), "left", itemSize, gap, sidePadding, bounds));
        children.AddRange(StatusItemsForZone(payload, items.Where((item) => item.Zone == "right"), "right", itemSize, gap, sidePadding, bounds));

        return new ResolvedDesignGroupNode
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

    private static IEnumerable<ResolvedDesignNode> StatusItemsForZone(
        DesignPreviewPayload payload,
        IEnumerable<PreviewItem> items,
        string zone,
        double itemSize,
        double gap,
        double sidePadding,
        DesignRect barBounds)
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

    private static ResolvedDesignNode StatusItem(
        DesignPreviewPayload payload,
        PreviewItem item,
        double x,
        double y,
        double width,
        double height)
    {
        if (item.Kind == "text")
        {
            return new ResolvedDesignTextNode
            {
                Id = $"statusBar.item.{item.Id}",
                Bounds = new DesignRect(x, y, width, height),
                Text = string.IsNullOrWhiteSpace(item.Value) ? item.Label : item.Value,
                Style = new ResolvedDesignTextStyle
                {
                    Fill = StatusForegroundPaint,
                    FontFamily = "Inter",
                    FontSize = height,
                    FontWeight = 600,
                    LineHeight = height,
                },
                TextAlign = "center",
                VerticalAlign = "middle",
                Metadata = MetadataFor(item),
            };
        }

        if (item.Kind == "generatedSignal")
        {
            var primitive = GeneratedSvgPrimitives.StatusSignal(NumberValue(item.Value, 0), height);
            return new ResolvedDesignSvgNode
            {
                Id = $"statusBar.item.{item.Id}",
                Bounds = new DesignRect(x, y + (height - primitive.Height) / 2, primitive.Width, primitive.Height),
                Markup = primitive.Markup,
                Fit = "fill",
                Tint = StatusForegroundPaint,
                Metadata = MetadataFor(item),
            };
        }

        if (item.Kind == "generatedBattery")
        {
            var primitive = GeneratedSvgPrimitives.StatusBattery(NumberValue(item.Value, 100), item.Charging, height);
            return new ResolvedDesignSvgNode
            {
                Id = $"statusBar.item.{item.Id}",
                Bounds = new DesignRect(x, y + (height - primitive.Height) / 2, primitive.Width, primitive.Height),
                Markup = primitive.Markup,
                Fit = "fill",
                Tint = StatusForegroundPaint,
                Metadata = MetadataFor(item),
            };
        }

        var iconMarkup = IconMarkup(payload, item.Token);
        if (!string.IsNullOrWhiteSpace(iconMarkup))
        {
            var glyph = new ResolvedDesignSvgNode
            {
                Id = $"statusBar.item.{item.Id}.glyph",
                Bounds = new DesignRect(0, 0, height, height),
                Markup = iconMarkup,
                Fit = "contain",
                Tint = StatusForegroundPaint,
                Metadata = MetadataFor(item),
            };
            return new ResolvedDesignGroupNode
            {
                Id = $"statusBar.item.{item.Id}",
                Bounds = new DesignRect(x, y, height, height),
                ClipRect = new DesignRect(0, 0, height, height),
                Children = [glyph],
                Metadata = MetadataFor(item),
            };
        }

        return new ResolvedDesignSvgNode
        {
            Id = $"statusBar.item.{item.Id}",
            Bounds = new DesignRect(x, y, width, height),
            Markup = FallbackIconSvg(item.TokenOrLabel),
            Fit = "contain",
            Tint = StatusForegroundPaint,
            Metadata = MetadataFor(item),
        };
    }

    private static ResolvedDesignGroupNode NavigationBar(
        DesignPreviewPayload payload,
        DesignMetrics metrics)
    {
        var config = ParseObject(payload.ConfigJson);
        var layout = config["layout"] as JsonObject ?? [];
        var height = Number(layout, "height", 34);
        var itemSize = Number(layout, "itemSize", 18);
        var sidePadding = Number(layout, "sidePadding", 40);
        var strokeWidth = Number(layout, "strokeWidth", 2);
        var cornerRadius = Number(layout, "cornerRadius", 3);
        var gap = 6;
        var bounds = new DesignRect(metrics.ScreenX, metrics.ScreenY + metrics.ScreenHeight - height, metrics.ScreenWidth, height);
        var children = new List<ResolvedDesignNode>
        {
            new ResolvedDesignRectNode
            {
                Id = "navigationBar.background",
                Bounds = new DesignRect(0, 0, bounds.Width, bounds.Height),
                Fill = NavigationBackgroundPaint,
            },
        };

        var items = Items(config)
            .Where((item) => item.Zone is "left" or "center" or "right")
            .OrderBy((item) => item.Order)
            .ToList();
        foreach (var zone in new[] { "left", "center", "right" })
        {
            children.AddRange(NavigationItemsForZone(items.Where((item) => item.Zone == zone), zone, itemSize, sidePadding, gap, bounds, strokeWidth, cornerRadius, Bool(layout, "filled", false)));
        }

        return new ResolvedDesignGroupNode
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

    private static IEnumerable<ResolvedDesignNode> NavigationItemsForZone(
        IEnumerable<PreviewItem> items,
        string zone,
        double itemSize,
        double sidePadding,
        double gap,
        DesignRect barBounds,
        double strokeWidth,
        double cornerRadius,
        bool filled)
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
                strokeWidth,
                cornerRadius,
                filled);
            yield return new ResolvedDesignSvgNode
            {
                Id = $"navigationBar.item.{item.Id}",
                Bounds = new DesignRect(x, y, primitive.Width, primitive.Height),
                Markup = primitive.Markup,
                Fit = "fill",
                Tint = NavigationForegroundPaint,
                Metadata = MetadataFor(item),
            };
            x += itemSize + gap;
        }
    }

    private static ResolvedDesignGroupNode UnsupportedPayload(
        DesignPreviewPayload payload,
        DesignMetrics metrics)
    {
        return new ResolvedDesignGroupNode
        {
            Id = $"unsupported.{payload.Kind}",
            Bounds = new DesignRect(metrics.ScreenX, metrics.ScreenY, metrics.ScreenWidth, metrics.ScreenHeight),
            Children =
            [
                new ResolvedDesignTextNode
                {
                    Id = $"unsupported.{payload.Kind}.label",
                    Bounds = new DesignRect(24, 24, Math.Max(1, metrics.ScreenWidth - 48), 32),
                    Text = $"Unsupported design payload: {payload.Kind}",
                    Style = new ResolvedDesignTextStyle
                    {
                        Fill = new ResolvedDesignPaint(new ResolvedDesignColorRef(
                            "debug.unsupported",
                            null,
                            "debug_red")),
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

    private static ResolvedDesignPaint ThemePaint(string tokenId)
    {
        return new ResolvedDesignPaint(new ResolvedDesignColorRef(tokenId, tokenId, "debug_red"));
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
            return File.Exists(path) ? SvgMarkupNormalizer.NormalizeTintableSvg(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null;
        }
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

    private static IReadOnlyDictionary<string, string> MetadataFor(
        PreviewItem item,
        params (string Key, string Value)[] extra)
    {
        var metadata = new Dictionary<string, string>
        {
            ["legacyKind"] = item.Kind,
            ["legacyZone"] = item.Zone,
            ["legacyLabel"] = item.Label,
        };
        foreach (var (key, value) in extra)
        {
            metadata[key] = value;
        }

        return metadata;
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
