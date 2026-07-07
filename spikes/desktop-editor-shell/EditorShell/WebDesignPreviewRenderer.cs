using Mockups.DesktopEditorShell.Data;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class WebDesignPreviewRenderer
{
    public static async Task<string> RenderBodyAsync(
        SpikeDatabase.DevicePreviewMetrics metrics,
        string themeMode,
        bool showMarks,
        DesignPreviewPayload payload)
    {
        var root = FindRepositoryRoot();
        var requestPath = Path.Combine(Path.GetTempPath(), $"mockups-design-preview-{Guid.NewGuid():N}.json");
        try
        {
            var request = new
            {
                kind = payload.Kind,
                componentType = payload.ComponentType,
                configJson = payload.ConfigJson,
                designPreviewJson = payload.DesignPreviewJson,
                showMarks,
                themeMode = themeMode is "dark" ? "dark" : "light",
                themeTokensJson = payload.ThemeTokensJson,
                paletteColors = payload.PaletteColors,
                paletteNeutralColors = payload.PaletteNeutralColors,
                projectMediaRoot = payload.ProjectMediaRoot,
                fontFaces = payload.FontFaces.Select((face) => new
                {
                    fontId = face.FontId,
                    family = face.FamilyName,
                    category = face.Category,
                    relativePath = face.RelativePath,
                    weight = face.Weight,
                    style = face.Style,
                }),
                iconAssetRoot = payload.IconAssetRoot,
                iconMappingJson = payload.IconMappingJson,
                componentBaseConfigsJson = payload.ComponentBaseConfigsJson,
                previewFrame = new
                {
                    canvasWidth = metrics.CanvasWidth,
                    canvasHeight = metrics.CanvasHeight,
                    screenX = metrics.ScreenX,
                    screenY = metrics.ScreenY,
                    screenWidth = metrics.ScreenWidth,
                    screenHeight = metrics.ScreenHeight,
                    scaleToPixels = metrics.ScaleToPixels,
                },
            };

            await File.WriteAllTextAsync(
                requestPath,
                JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return await RunRendererAsync(root, requestPath);
        }
        finally
        {
            try
            {
                if (File.Exists(requestPath))
                {
                    File.Delete(requestPath);
                }
            }
            catch
            {
                // Temporary payload cleanup must not block preview rendering.
            }
        }
    }

    private static async Task<string> RunRendererAsync(string root, string requestPath)
    {
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "tsx.cmd"
            : "tsx";
        var executable = Path.Combine(root, "node_modules", ".bin", executableName);
        var script = Path.Combine(root, "src", "desktop-preview", "renderDesignPreviewHtml.tsx");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add(requestPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start design preview renderer.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Design preview renderer failed with exit code {process.ExitCode}: {stderr}");
        }

        return stdout;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "package.json"))
                && Directory.Exists(Path.Combine(directory.FullName, "src", "visual")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate MOCKUPS repository root.");
    }
}
