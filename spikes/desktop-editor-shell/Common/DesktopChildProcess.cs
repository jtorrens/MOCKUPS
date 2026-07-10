using System.Diagnostics;

namespace Mockups.DesktopEditorShell.Common;

internal static class DesktopChildProcess
{
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
