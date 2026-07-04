using Avalonia;
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
