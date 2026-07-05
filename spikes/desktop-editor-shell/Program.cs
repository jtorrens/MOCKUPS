using Avalonia;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Preview.Avalonia;
using Mockups.DesktopEditorShell.Preview.Bridges;
using Mockups.DesktopEditorShell.VisualIr;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--visual-ir-smoke", StringComparer.Ordinal))
        {
            VisualIrSmokeFixture.ValidateRoundTrip();
            Console.WriteLine("Visual IR smoke fixture validated.");
            return;
        }

        if (args.Contains("--svg-tint-smoke", StringComparer.Ordinal))
        {
            SvgMarkupNormalizerSmoke.Validate();
            Console.WriteLine("SVG tint normalization smoke validated.");
            return;
        }

        if (args.Contains("--visual-ir-debug-renderer-smoke", StringComparer.Ordinal))
        {
            BuildAvaloniaApp().SetupWithoutStarting();
            VisualIrDebugRendererSmoke.Validate();
            Console.WriteLine("Visual IR debug renderer smoke validated.");
            return;
        }

        if (args.Contains("--design-preview-ir-bridge-smoke", StringComparer.Ordinal))
        {
            BuildAvaloniaApp().SetupWithoutStarting();
            DesignPreviewToVisualIrBridgeSmoke.Validate();
            Console.WriteLine("Design preview to Visual IR bridge smoke validated.");
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
