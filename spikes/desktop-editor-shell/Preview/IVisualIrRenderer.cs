using Avalonia.Controls;
using Mockups.DesktopEditorShell.VisualIr;

namespace Mockups.DesktopEditorShell.Preview;

internal interface IVisualIrRenderer
{
    Control Render(VisualIrDocument document, VisualIrRenderOptions options);
}

internal sealed record VisualIrRenderOptions(
    string? SelectedColorVariant = null,
    bool ShowBounds = false,
    bool ShowUnsupportedPlaceholders = true);

