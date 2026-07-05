using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Preview.Avalonia;
using Mockups.DesktopEditorShell.VisualIr;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Preview.Bridges;

internal static class DesignPreviewToVisualIrBridgeSmoke
{
    private static readonly SpikeDatabase.DevicePreviewMetrics Metrics = new(
        "Smoke device",
        CanvasWidth: 390,
        CanvasHeight: 844,
        ScreenX: 0,
        ScreenY: 0,
        ScreenWidth: 390,
        ScreenHeight: 844,
        CornerRadius: 36,
        StatusBarHeight: 54,
        SafeAreaBottom: 34,
        ScaleToPixels: 1);

    public static void Validate()
    {
        ValidatePayload(StatusPayload());
        ValidatePayload(NavigationPayload());
    }

    private static void ValidatePayload(DesignPreviewPayload payload)
    {
        var document = DesignPreviewToVisualIrBridge.Convert(payload, Metrics);
        VisualIrValidator.ThrowIfInvalid(document);
        if (payload.Kind is "statusBar" or "navigationBar"
            && !Flatten(document.Root).OfType<VisualIrSvgNode>().Any((svg) => svg.Markup?.Contains("currentColor", StringComparison.Ordinal) == true))
        {
            throw new InvalidOperationException($"Expected generated SVG markup for {payload.Kind}.");
        }

        var renderer = new AvaloniaVisualIrDebugRenderer();
        renderer.Render(document, new VisualIrRenderOptions(ShowBounds: true));
    }

    private static IEnumerable<VisualIrNode> Flatten(VisualIrNode node)
    {
        yield return node;
        if (node is not VisualIrGroupNode group)
        {
            yield break;
        }

        foreach (var child in group.Children.SelectMany(Flatten))
        {
            yield return child;
        }
    }

    private static DesignPreviewPayload StatusPayload()
    {
        return new DesignPreviewPayload(
            "statusBar",
            "Smoke Status Bar",
            """
            {
              "layout": { "height": 54, "itemSize": 18, "gap": 6, "sidePadding": 24 },
              "items": [
                { "id": "time", "label": "Time", "kind": "text", "value": "9:41", "zone": "left", "order": 10 },
                { "id": "wifi", "label": "Wi-Fi", "kind": "iconToken", "token": "status_wifi", "zone": "right", "order": 20 },
                { "id": "battery", "label": "Battery", "kind": "generatedBattery", "value": 85, "zone": "right", "order": 30 }
              ]
            }
            """,
            "{}",
            new Dictionary<string, string>(),
            "",
            "",
            "{}");
    }

    private static DesignPreviewPayload NavigationPayload()
    {
        return new DesignPreviewPayload(
            "navigationBar",
            "Smoke Navigation Bar",
            """
            {
              "layout": { "height": 34, "itemSize": 18, "sidePadding": 40 },
              "items": [
                { "id": "back", "label": "Back", "kind": "generatedBack", "zone": "left", "order": 10 },
                { "id": "home", "label": "Home", "kind": "generatedHome", "zone": "center", "order": 10 },
                { "id": "recents", "label": "Recents", "kind": "generatedRecents", "zone": "right", "order": 10 }
              ]
            }
            """,
            "{}",
            new Dictionary<string, string>(),
            "",
            "",
            "{}");
    }
}
