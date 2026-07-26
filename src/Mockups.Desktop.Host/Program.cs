using Avalonia;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (CurrentDatabaseMaintenance.TryRun(args))
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

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HostedApp>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
