using System;
using System.IO;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class MediaPathService
{
    public static string? RelativePathIfInsideMediaRoot(string path, string? mediaRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(mediaRoot)) return path;

        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(mediaRoot);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative)
            ? path
            : relative;
    }

    public static string? ResolveLocalPath(string path, string? mediaRoot)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        return Path.IsPathFullyQualified(path)
            ? path
            : !string.IsNullOrWhiteSpace(mediaRoot)
                ? Path.GetFullPath(Path.Combine(mediaRoot, path))
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", path));
    }
}
