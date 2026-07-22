using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Mockups.DesktopEditorShell.Common;

internal static class DesktopChildProcess
{
    public static string ResolveNodeExecutable()
    {
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "node.exe"
            : "node";
        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[]
            {
                Path.Combine(AppContext.BaseDirectory, "node", executableName),
                executableName,
            }
            : new[]
            {
                Path.Combine(AppContext.BaseDirectory, "node", "bin", executableName),
                "/opt/homebrew/bin/node",
                "/usr/local/bin/node",
                "/usr/bin/node",
                executableName,
            };

        return candidates.FirstOrDefault(File.Exists) ?? executableName;
    }

    public static ProcessStartInfo CreateHiddenStartInfo(string fileName, string workingDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
    }
}
