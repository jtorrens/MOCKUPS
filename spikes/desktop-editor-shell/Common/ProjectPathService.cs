using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Common;

internal static class ProjectPathService
{
    private static readonly object Gate = new();
    private static string? _projectRoot;

    public static void ConfigureProjectRoot(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) return;
        lock (Gate)
        {
            _projectRoot = Path.GetFullPath(projectRoot);
        }
    }

    public static string? RelativePathIfInsideMediaRoot(string path, string? mediaRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(mediaRoot)) return path;

        var fullPath = Path.GetFullPath(path);
        var fullRoot = ResolveProjectPath(mediaRoot);
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
                ? Path.GetFullPath(Path.Combine(ResolveProjectPath(mediaRoot), path))
                : ResolveProjectPath(path);
    }

    public static string ResolveProjectPath(string path)
    {
        if (Path.IsPathFullyQualified(path)) return path;
        return Path.GetFullPath(Path.Combine(ProjectRoot(), path));
    }

    public static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string ProjectRoot()
    {
        lock (Gate)
        {
            if (!string.IsNullOrWhiteSpace(_projectRoot))
            {
                return _projectRoot;
            }
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    }
}
