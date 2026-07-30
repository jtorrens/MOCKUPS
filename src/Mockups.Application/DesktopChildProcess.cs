using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Mockups.DesktopEditorShell.Common;

public static class DesktopChildProcess
{
    private static readonly Encoding Utf8WithoutBom =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: false);

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

    public static ProcessStartInfo CreateHiddenStartInfo(
        string fileName,
        string workingDirectory,
        bool redirectStandardInput = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Utf8WithoutBom,
            StandardErrorEncoding = Utf8WithoutBom,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        if (redirectStandardInput)
        {
            startInfo.StandardInputEncoding = Utf8WithoutBom;
        }

        return startInfo;
    }
}
