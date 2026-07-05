using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
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
        var document = DesignPreviewToVisualIrBridge.Convert(UnsupportedStatusPayload(), Metrics);
        VisualIrValidator.ThrowIfInvalid(document);
        if (!Flatten(document.Root).Any((node) => node.Id == "unsupported.statusBar"))
        {
            throw new InvalidOperationException("Expected migrated system bars to be unsupported by the Visual IR bridge.");
        }
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

    private static DesignPreviewPayload UnsupportedStatusPayload()
    {
        return new DesignPreviewPayload(
            "statusBar",
            "Migrated Status Bar",
            "{}",
            "{}",
            new Dictionary<string, string>(),
            new Dictionary<string, bool>(),
            "",
            "",
            "{}");
    }
}
