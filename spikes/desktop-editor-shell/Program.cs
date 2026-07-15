using Avalonia;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
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
        if (args.Contains("--migrate-database", StringComparer.Ordinal))
        {
            _ = new SpikeDatabase(SpikeDatabase.DefaultDatabasePath());
            Console.WriteLine("Desktop editor database migrations completed.");
            return;
        }

        if (SchemaV1DatabaseConsolidator.TryRun(args))
        {
            return;
        }

        var avaloniaArgs = DesktopEditorLaunchOptions.Configure(args);
        if (args.Contains("--svg-tint-smoke", StringComparer.Ordinal))
        {
            SvgMarkupNormalizerSmoke.Validate();
            Console.WriteLine("SVG tint normalization smoke validated.");
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(avaloniaArgs);
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
