using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Preview.Resolved;
using System;
using System.Collections.Generic;

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
        double CornerRadius)
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
                metrics.CornerRadius / scale);
        }
    }

    private static readonly ResolvedDesignPaint ScreenBackgroundPaint = ThemePaint(
        "theme.colors.background");

    public static ResolvedDesignFrame Resolve(
        DesignPreviewPayload payload,
        SpikeDatabase.DevicePreviewMetrics metrics)
    {
        var designMetrics = DesignMetrics.From(metrics);
        return new ResolvedDesignFrame
        {
            Width = designMetrics.CanvasWidth,
            Height = designMetrics.CanvasHeight,
            Children =
            [
                new ResolvedDesignRectNode
                {
                    Id = "device.screen",
                    Bounds = new DesignRect(designMetrics.ScreenX, designMetrics.ScreenY, designMetrics.ScreenWidth, designMetrics.ScreenHeight),
                    Fill = ScreenBackgroundPaint,
                    Radius = designMetrics.CornerRadius,
                },
                UnsupportedPayload(payload, designMetrics),
            ],
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "DesignPreviewPayload",
                ["sourceKind"] = payload.Kind,
                ["name"] = payload.Name,
                ["coordinateSpace"] = "design",
                ["bridge"] = "unsupported-migrated-design-preview",
            },
        };
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
                    Text = $"Unsupported legacy design payload: {payload.Kind}",
                    Style = new ResolvedDesignTextStyle
                    {
                        Fill = new ResolvedDesignSolidPaint(new ResolvedDesignColorRef(
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

    private static ResolvedDesignPaint ThemePaint(string tokenId)
    {
        return new ResolvedDesignSolidPaint(new ResolvedDesignColorRef(tokenId, tokenId, "debug_red"));
    }
}
