using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.VisualIr;

internal static class VisualIrSmokeFixture
{
    public static VisualIrDocument Create()
    {
        var variantTextColor = VisualIrColor.Variant(
            new Dictionary<string, string>
            {
                ["set_day"] = "#111111",
                ["set_night"] = "#f7f7f7",
            },
            "#f7f7f7");
        var variantSurfaceColor = VisualIrColor.Variant(
            new Dictionary<string, string>
            {
                ["set_day"] = "#ffffff",
                ["set_night"] = "#10151f",
            },
            "#10151f");
        var iconTint = VisualIrColor.Variant(
            new Dictionary<string, string>
            {
                ["set_day"] = "#1a1a1a",
                ["set_night"] = "#ffffff",
            },
            "#ffffff");

        return new VisualIrDocument
        {
            Viewport = new VisualIrViewport(390, 844, 1),
            Resources = new VisualIrResources
            {
                ColorVariants = ["set_day", "set_night"],
                DefaultColorVariant = "set_night",
            },
            Root = new VisualIrGroupNode
            {
                Id = "root",
                Bounds = new VisualIrRect(0, 0, 390, 844),
                Children =
                [
                    new VisualIrRectNode
                    {
                        Id = "surface",
                        Bounds = new VisualIrRect(0, 0, 390, 96),
                        Fill = new VisualIrSolidPaint(variantSurfaceColor),
                    },
                    new VisualIrTextNode
                    {
                        Id = "title",
                        Bounds = new VisualIrRect(24, 38, 240, 24),
                        Text = "Visual IR smoke",
                        Style = new VisualIrTextStyle
                        {
                            Fill = new VisualIrSolidPaint(variantTextColor),
                            FontFamily = "Inter",
                            FontSize = 17,
                            FontWeight = 700,
                            LineHeight = 22,
                        },
                    },
                    new VisualIrSvgNode
                    {
                        Id = "wifi",
                        Bounds = new VisualIrRect(338, 38, 24, 24),
                        Markup = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M4 10 C8 6 16 6 20 10\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"/></svg>",
                        Fit = "contain",
                        Tint = new VisualIrSolidPaint(iconTint),
                    },
                ],
            },
        };
    }

    public static void ValidateRoundTrip()
    {
        var document = Create();
        VisualIrValidator.ThrowIfInvalid(document);
        var json = VisualIrSerialization.Serialize(document);
        var roundTripped = VisualIrSerialization.Deserialize(json);
        VisualIrValidator.ThrowIfInvalid(roundTripped);
    }
}

