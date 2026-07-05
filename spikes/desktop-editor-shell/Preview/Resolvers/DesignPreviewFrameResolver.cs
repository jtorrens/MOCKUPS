using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Preview.Resolved;
using System;
using System.Collections.Generic;
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
            "componentClass" => ComponentClass(payload, designMetrics),
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

    private static ResolvedDesignNode ComponentClass(
        DesignPreviewPayload payload,
        DesignMetrics metrics)
    {
        return payload.ComponentType switch
        {
            "label" => LabelComponent(payload, metrics),
            _ => UnsupportedPayload(payload, metrics),
        };
    }

    private static ResolvedDesignGroupNode LabelComponent(
        DesignPreviewPayload payload,
        DesignMetrics metrics)
    {
        var config = JsonPath.ParseObject(payload.ConfigJson);
        var preview = JsonPath.ParseObject(payload.DesignPreviewJson);
        var label = config["label"] as JsonObject ?? [];
        var style = config["style"] as JsonObject ?? [];
        var size = SizePair(JsonPath.String(label, "size", "120|32"), 120, 32);
        var padding = SizePair(JsonPath.String(label, "padding", "8|4"), 8, 4);
        var backgroundVisible = JsonPath.Bool(label, "backgroundVisible", true);
        var text = JsonPath.String(preview, "sampleText", "Sample");
        var textSizeToken = JsonPath.String(label, "textSizeToken", "");
        var textSize = RequiredThemeNumber(payload.ThemeTokensJson, textSizeToken, "component.label.textSizeToken");
        var textStyle = JsonPath.String(label, "textStyle", "normal");
        var dimensionMode = JsonPath.String(label, "dimensionMode", "content");
        var contentSize = LabelContentSize(text, textSize, padding);
        var width = dimensionMode == "fixed" ? size.Width : contentSize.Width;
        var height = dimensionMode == "fixed" ? size.Height : contentSize.Height;
        var bounds = Centered(metrics, width, height);
        var borderWidth = JsonPath.Number(style, "borderWidth", 0);
        var cornerRadiusToken = RequiredString(style, "cornerRadiusToken", "component.style.cornerRadiusToken");
        var cornerRadius = RequiredThemeNumber(payload.ThemeTokensJson, cornerRadiusToken, "component.style.cornerRadiusToken");
        var borderColorToken = RequiredString(style, "borderColorToken", "component.style.borderColorToken");
        var shadowEnabled = JsonPath.Bool(style, "shadowEnabled", false);
        var backgroundColorToken = RequiredString(label, "backgroundColorToken", "component.label.backgroundColorToken");
        var textColorToken = RequiredString(label, "textColorToken", "component.label.textColorToken");

        return new ResolvedDesignGroupNode
        {
            Id = "component.label",
            Bounds = bounds,
            Children =
            [
                new ResolvedDesignRectNode
                {
                    Id = "component.label.background",
                    Bounds = new DesignRect(0, 0, bounds.Width, bounds.Height),
                    Fill = backgroundVisible
                        ? ThemePaint(backgroundColorToken)
                        : StaticPaint("component.label.background.transparent", "#00000000"),
                    Stroke = borderWidth > 0
                        ? new ResolvedDesignStroke(ThemePaint(borderColorToken), borderWidth)
                        : null,
                    Radius = cornerRadius,
                    Effects = shadowEnabled ? [new ResolvedDesignShadowEffect(0, 3, 10, new ResolvedDesignColorRef("component.style.shadow", null, "#33000000"))] : null,
                    Metadata = ComponentStyleMetadata(style, borderWidth, borderColorToken, cornerRadiusToken, cornerRadius, shadowEnabled),
                },
                new ResolvedDesignTextNode
                {
                    Id = "component.label.text",
                    Bounds = new DesignRect(
                        padding.Width,
                        padding.Height,
                        Math.Max(1, bounds.Width - padding.Width * 2),
                        Math.Max(1, bounds.Height - padding.Height * 2)),
                    Text = text,
                    Style = new ResolvedDesignTextStyle
                    {
                        Fill = ThemePaint(textColorToken),
                        FontFamily = "Inter",
                        FontSize = textSize,
                        FontStyle = textStyle == "italic" ? "italic" : null,
                        LineHeight = Math.Max(1, bounds.Height - padding.Height * 2),
                    },
                    TextAlign = "center",
                    VerticalAlign = "middle",
                },
            ],
            Metadata = new Dictionary<string, string>
            {
                ["legacyKind"] = payload.Kind,
                ["componentType"] = payload.ComponentType,
                ["dimensionMode"] = dimensionMode,
                ["textSizeToken"] = textSizeToken,
            },
        };
    }

    private static ResolvedDesignGroupNode StatusBar(
        DesignPreviewPayload payload,
        DesignMetrics metrics)
    {
        var config = JsonPath.ParseObject(payload.ConfigJson);
        var layout = config["layout"] as JsonObject ?? [];
        var height = JsonPath.Number(layout, "height", metrics.StatusBarHeight > 0 ? metrics.StatusBarHeight : 54);
        var itemSize = JsonPath.Number(layout, "itemSize", 18);
        var gap = JsonPath.Number(layout, "gap", 6);
        var sidePadding = JsonPath.Number(layout, "sidePadding", 24);
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
            .Where(IsVisibleStatusItem)
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
                Text = item.Value,
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
        var config = JsonPath.ParseObject(payload.ConfigJson);
        var layout = config["layout"] as JsonObject ?? [];
        var height = JsonPath.Number(layout, "height", 34);
        var itemSize = JsonPath.Number(layout, "itemSize", 18);
        var sidePadding = JsonPath.Number(layout, "sidePadding", 40);
        var strokeWidth = JsonPath.Number(layout, "strokeWidth", 2);
        var cornerRadius = JsonPath.Number(layout, "cornerRadius", 3);
        var type = JsonPath.String(config, "type", "buttons");
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

        if (type == "gestureBar")
        {
            var gesture = config["gesture"] as JsonObject ?? [];
            var gestureWidth = JsonPath.Number(gesture, "width", 134);
            var gestureHeight = JsonPath.Number(gesture, "height", 5);
            var gestureRadius = JsonPath.Number(gesture, "cornerRadius", gestureHeight / 2);
            children.Add(new ResolvedDesignRectNode
            {
                Id = "navigationBar.gesture",
                Bounds = new DesignRect(
                    (bounds.Width - gestureWidth) / 2,
                    (bounds.Height - gestureHeight) / 2,
                    gestureWidth,
                    gestureHeight),
                Fill = NavigationForegroundPaint,
                Radius = gestureRadius,
            });

            return NavigationGroup(payload, bounds, children);
        }

        var items = Items(config)
            .Where((item) => item.Zone is "left" or "center" or "right")
            .OrderBy((item) => item.Order)
            .ToList();
        foreach (var zone in new[] { "left", "center", "right" })
        {
            children.AddRange(NavigationItemsForZone(items.Where((item) => item.Zone == zone), zone, itemSize, sidePadding, gap, bounds, strokeWidth, cornerRadius, JsonPath.Bool(layout, "filled", false)));
        }

        return NavigationGroup(payload, bounds, children);
    }

    private static ResolvedDesignGroupNode NavigationGroup(
        DesignPreviewPayload payload,
        DesignRect bounds,
        IReadOnlyList<ResolvedDesignNode> children)
    {
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
                JsonPath.String(item, "id", $"item_{index}"),
                JsonPath.String(item, "label", $"Item {index + 1}"),
                JsonPath.String(item, "kind", "text"),
                JsonPath.String(item, "value", ""),
                JsonPath.String(item, "token", ""),
                JsonPath.String(item, "zone", "off"),
                JsonPath.Number(item, "order", index * 10),
                JsonPath.Bool(item, "charging", false)))
            .ToList();
    }

    private static double StatusItemWidth(PreviewItem item, double itemSize)
    {
        return item.Kind switch
        {
            "generatedBattery" => itemSize * 1.55,
            "generatedSignal" => itemSize * 1.08,
            "iconToken" => itemSize,
            _ => Math.Max(itemSize, item.Value.Length * itemSize * 0.58),
        };
    }

    private static bool IsVisibleStatusItem(PreviewItem item)
    {
        return item.Kind != "text" || !string.IsNullOrWhiteSpace(item.Value);
    }

    private static ResolvedDesignPaint ThemePaint(string tokenId)
    {
        return ThemePaint(tokenId, "debug_red");
    }

    private static ResolvedDesignPaint ThemePaint(string tokenId, string fallback)
    {
        return new ResolvedDesignPaint(new ResolvedDesignColorRef(tokenId, tokenId, fallback));
    }

    private static ResolvedDesignPaint StaticPaint(string id, string value)
    {
        return new ResolvedDesignPaint(new ResolvedDesignColorRef(id, null, value));
    }

    private static DesignRect Centered(DesignMetrics metrics, double width, double height)
    {
        return new DesignRect(
            metrics.ScreenX + (metrics.ScreenWidth - width) / 2,
            metrics.ScreenY + (metrics.ScreenHeight - height) / 2,
            width,
            height);
    }

    private static DesignSize SizePair(string value, double fallbackWidth, double fallbackHeight)
    {
        var parts = value.Split('|', 2, StringSplitOptions.TrimEntries);
        return new DesignSize(
            parts.Length > 0 ? NumericText.Double(parts[0], fallbackWidth) : fallbackWidth,
            parts.Length > 1 ? NumericText.Double(parts[1], fallbackHeight) : fallbackHeight);
    }

    private static DesignSize LabelContentSize(string text, double textSize, DesignSize padding)
    {
        var measuredWidth = Math.Max(textSize, text.Length * textSize * 0.58);
        var measuredHeight = Math.Max(textSize, textSize * 1.35);
        return new DesignSize(
            Math.Max(1, measuredWidth + padding.Width * 2),
            Math.Max(1, measuredHeight + padding.Height * 2));
    }

    private static double RequiredThemeNumber(string themeTokensJson, string tokenId, string fieldId)
    {
        if (string.IsNullOrWhiteSpace(tokenId) || !ThemeNumericTokenCatalog.TryGet(tokenId, out var token))
        {
            throw new InvalidOperationException($"Missing numeric theme token for {fieldId}.");
        }

        var tokens = JsonPath.ParseObject(themeTokensJson);
        var node = JsonPath.Get(tokens, token.Path);
        if (node is null)
        {
            throw new InvalidOperationException($"Theme token '{tokenId}' is not defined.");
        }

        return JsonPath.NumberAt(tokens, token.Path, 0);
    }

    private static string RequiredString(JsonObject root, string key, string fieldId)
    {
        var value = JsonPath.String(root, key, "");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required value for {fieldId}.");
        }

        return value;
    }

    private static IReadOnlyDictionary<string, string> ComponentStyleMetadata(
        JsonObject style,
        double borderWidth,
        string borderColorToken,
        string cornerRadiusToken,
        double cornerRadius,
        bool shadowEnabled)
    {
        return new Dictionary<string, string>
        {
            ["style.shadowEnabled"] = shadowEnabled ? "true" : "false",
            ["style.reliefEnabled"] = JsonPath.Bool(style, "reliefEnabled", false) ? "true" : "false",
            ["style.borderWidth"] = borderWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["style.borderColorToken"] = borderColorToken,
            ["style.cornerRadiusToken"] = cornerRadiusToken,
            ["style.cornerRadius"] = cornerRadius.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["style.reliefAngle"] = JsonPath.Number(style, "reliefAngle", -45).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["style.reliefExtent"] = JsonPath.Number(style, "reliefExtent", 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["style.reliefSpread"] = JsonPath.Number(style, "reliefSpread", 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["style.reliefTopIntensity"] = JsonPath.Number(style, "reliefTopIntensity", 12).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["style.reliefBottomIntensity"] = JsonPath.Number(style, "reliefBottomIntensity", -10).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
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
            var mapping = JsonPath.ParseObject(payload.IconMappingJson);
            var tokens = mapping["tokens"] as JsonObject;
            var tokenObject = tokens?[token] as JsonObject;
            var file = tokenObject is null ? "" : JsonPath.String(tokenObject, "file", "");
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

    private static double NumberValue(string value, double fallback)
    {
        return NumericText.Double(value, fallback);
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

    private sealed record DesignSize(double Width, double Height);
}
