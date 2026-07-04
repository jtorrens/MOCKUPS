using Avalonia.Controls;
using Mockups.DesktopEditorShell.VisualIr;
using System;

namespace Mockups.DesktopEditorShell.Preview.Avalonia;

internal static class VisualIrDebugRendererSmoke
{
    public static void Validate()
    {
        var document = VisualIrSmokeFixture.Create();
        var renderer = new AvaloniaVisualIrDebugRenderer();
        var rendered = renderer.Render(
            document,
            new VisualIrRenderOptions(
                SelectedColorVariant: "set_night",
                ShowBounds: true));

        if (rendered is not Canvas canvas)
        {
            throw new InvalidOperationException("Debug renderer must return a root Canvas.");
        }

        if (canvas.Width != document.Viewport.Width || canvas.Height != document.Viewport.Height)
        {
            throw new InvalidOperationException("Debug renderer root canvas size does not match document viewport.");
        }

        if (canvas.Children.Count != document.Root.Children.Count)
        {
            throw new InvalidOperationException("Debug renderer root child count does not match document root child count.");
        }
    }
}

