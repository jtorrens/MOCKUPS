using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Preview.Avalonia;
using Mockups.DesktopEditorShell.VisualIr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

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
        ValidatePayload(NavigationGesturePayload(), expectGeneratedSvg: false);
        ValidatePayload(LabelComponentPayload(), expectGeneratedSvg: false);
    }

    private static void ValidatePayload(DesignPreviewPayload payload, bool expectGeneratedSvg = true)
    {
        var document = DesignPreviewToVisualIrBridge.Convert(payload, Metrics);
        VisualIrValidator.ThrowIfInvalid(document);
        if (expectGeneratedSvg
            && payload.Kind is "statusBar" or "navigationBar"
            && !Flatten(document.Root).OfType<VisualIrSvgNode>().Any((svg) => svg.Markup?.Contains("currentColor", StringComparison.Ordinal) == true))
        {
            throw new InvalidOperationException($"Expected generated SVG markup for {payload.Kind}.");
        }

        if (!expectGeneratedSvg
            && payload.Kind == "navigationBar"
            && !Flatten(document.Root).OfType<VisualIrRectNode>().Any((rect) => rect.Id == "navigationBar.gesture"))
        {
            throw new InvalidOperationException("Expected gesture navigation bar rect.");
        }

        if (payload.Kind == "componentClass"
            && payload.ComponentType == "label"
            && !Flatten(document.Root).OfType<VisualIrGroupNode>().Any((group) =>
                group.Id == "component.label"
                && group.Metadata?.TryGetValue("dimensionMode", out var dimensionMode) == true
                && dimensionMode == "content"))
        {
            throw new InvalidOperationException("Expected content-sized label component group.");
        }

        if (payload.Kind == "componentClass"
            && payload.ComponentType == "label"
            && !Flatten(document.Root).OfType<VisualIrTextNode>().Any((text) =>
                text.Id == "component.label.text"
                && text.Text == "Sample"
                && text.Style.FontSize == 18
                && text.Style.FontStyle == "italic"))
        {
            throw new InvalidOperationException("Expected label component text style.");
        }

        if (payload.Kind == "componentClass"
            && payload.ComponentType == "label"
            && !Flatten(document.Root).OfType<VisualIrRectNode>().Any((rect) =>
                rect.Id == "component.label.background"
                && rect.Radius == 9
                && rect.Stroke?.Width == 2
                && rect.Effects?.Count > 0))
        {
            throw new InvalidOperationException("Expected label component card style.");
        }

        if (payload.Kind == "componentClass"
            && payload.ComponentType == "label"
            && !Flatten(document.Root).OfType<VisualIrRectNode>().Any((rect) => rect.Id.StartsWith("component.label.relief.top.", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Expected label component relief layer.");
        }

        if (payload.Kind == "componentClass"
            && payload.ComponentType == "label"
            && !Flatten(document.Root).OfType<VisualIrGroupNode>().Any((group) =>
                group.Id == "component.label.relief"
                && group.ClipRect is not null
                && group.ClipRadius == 9))
        {
            throw new InvalidOperationException("Expected label component rounded relief clipping group.");
        }

        if (payload.Kind == "componentClass"
            && payload.ComponentType == "label"
            && !Flatten(document.Root).Any((node) => node.Effects?.OfType<VisualIrBlurEffect>().Any() == true))
        {
            throw new InvalidOperationException("Expected label component relief spread blur.");
        }

        if (payload.Kind == "componentClass"
            && payload.ComponentType == "label")
        {
            var zeroExtentPayload = WithReliefExtent(payload, 0);
            var zeroExtentDocument = DesignPreviewToVisualIrBridge.Convert(zeroExtentPayload, Metrics, "set_night");
            if (Flatten(zeroExtentDocument.Root).OfType<VisualIrRectNode>().Any((rect) => rect.Id.StartsWith("component.label.relief.", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Expected zero relief extent to emit no relief layers.");
            }
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

    private static DesignPreviewPayload WithReliefExtent(DesignPreviewPayload payload, double extent)
    {
        var config = JsonNode.Parse(payload.ConfigJson)?.AsObject()
            ?? throw new InvalidOperationException("Invalid label component config JSON.");
        var style = config["style"]?.AsObject()
            ?? throw new InvalidOperationException("Missing label component style config.");
        style["reliefExtent"] = extent;
        return payload with { ConfigJson = config.ToJsonString() };
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

    private static DesignPreviewPayload NavigationGesturePayload()
    {
        return new DesignPreviewPayload(
            "navigationBar",
            "Smoke Gesture Navigation Bar",
            """
            {
              "type": "gestureBar",
              "layout": { "height": 34, "itemSize": 18, "sidePadding": 40 },
              "gesture": { "width": 134, "height": 5, "cornerRadius": 3 },
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

    private static DesignPreviewPayload LabelComponentPayload()
    {
        return new DesignPreviewPayload(
            "componentClass",
            "Smoke Label Component",
            """
            {
              "style": {
                "shadowEnabled": true,
                "reliefEnabled": true,
                "borderWidth": 2,
                "borderColorToken": "theme.borders.primary",
                "cornerRadiusToken": "theme.radii.surface",
                "reliefAngle": -30,
                "reliefExtent": 2,
                "reliefSpread": 1,
                "reliefTopIntensity": 0.14,
                "reliefBottomIntensity": -0.08
              },
              "label": {
                "dimensionMode": "content",
                "size": "180|64",
                "padding": "12|6",
                "backgroundVisible": true,
                "backgroundColorToken": "theme.colors.background",
                "textColorToken": "theme.colors.textPrimary",
                "textSizeToken": "theme.typography.sizes.l",
                "textStyle": "italic"
              }
            }
            """,
            """
            {
              "radii": { "surface": 9 },
              "typography": { "sizes": { "l": 18 } },
              "modes": {
                "light": { "colors": { "background": "#FFFFFF", "textPrimary": "#111827", "borders.primary": "#111827" } },
                "dark": { "colors": { "background": "#111827", "textPrimary": "#FFFFFF", "borders.primary": "#FFFFFF" } }
              }
            }
            """,
            new Dictionary<string, string>(),
            "",
            "",
            "{}",
            "label",
            """{ "sampleText": "Sample" }""");
    }
}
