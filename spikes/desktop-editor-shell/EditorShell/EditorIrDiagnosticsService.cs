using Mockups.DesktopEditorShell.Preview.Avalonia;
using Mockups.DesktopEditorShell.Preview.Bridges;
using Mockups.DesktopEditorShell.VisualIr;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorIrDiagnosticsResult(bool Success, string Message);

internal static class EditorIrDiagnosticsService
{
    public static EditorIrDiagnosticsResult Run()
    {
        try
        {
            VisualIrSmokeFixture.ValidateRoundTrip();
            VisualIrDebugRendererSmoke.Validate();
            DesignPreviewToVisualIrBridgeSmoke.Validate();
            return new EditorIrDiagnosticsResult(true, $"IR checks OK · {DateTime.Now:HH:mm:ss}");
        }
        catch (Exception exception)
        {
            return new EditorIrDiagnosticsResult(false, $"IR checks failed · {exception.Message}");
        }
    }
}

