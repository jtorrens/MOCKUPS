using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Data;

public static class ProjectMediaRootContract
{
    public static string Require(string value, string owner)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return "";
        if (!Path.IsPathFullyQualified(trimmed))
        {
            throw new InvalidOperationException(
                $"{owner} media root must be an absolute external directory path.");
        }
        return Path.GetFullPath(trimmed);
    }
}
