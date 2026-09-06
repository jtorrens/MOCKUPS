using System;
using System.Diagnostics;
using System.IO;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorLocalPathActions
{
    public static void Reveal(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var start = new ProcessStartInfo
                {
                    FileName = "open",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                start.ArgumentList.Add("-R");
                start.ArgumentList.Add(path);
                Process.Start(start);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Directory.Exists(path)
                    ? path
                    : Path.GetDirectoryName(path) ?? path,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Revealing is a convenience; the authored reference remains intact.
        }
    }
}
