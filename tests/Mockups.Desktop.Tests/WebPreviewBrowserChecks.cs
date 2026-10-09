using System.Diagnostics;
using System.Reflection;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

internal static class WebPreviewBrowserChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"mockups-preview-browser-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var metrics = new DevicePreviewMetrics("Test", 360, 800, 0, 0, 360, 800, 0, 0, 0, 0, 0,
                DeviceModuleTransparencyOverride.Disabled);
            var document = (string)typeof(WebPreviewPane).GetMethod("DeviceHtml",
                BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null,
                [metrics, true, "Test", "light", "fit", "Design preview", false, false, false, false,
                    "<div data-renderable-id=\"owner\" style=\"width:100%;height:100%;background:red\">A</div>",
                    "", null, null])!;
            var html = Path.Combine(directory, "preview.html");
            File.WriteAllText(html, document);
            var start = DesktopChildProcess.CreateHiddenStartInfo(DesktopChildProcess.ResolveNodeExecutable(),
                Directory.GetCurrentDirectory());
            start.ArgumentList.Add("tests/Mockups.Desktop.Tests/webPreviewBrowserChecks.mjs");
            start.ArgumentList.Add(html);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Preview browser regression exceeded 60 seconds.");
            }
            Console.WriteLine(output.GetAwaiter().GetResult());
            if (process.ExitCode != 0) throw new InvalidOperationException(error.GetAwaiter().GetResult());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
