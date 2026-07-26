using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Common;

public interface IProjectPathResolver
{
    string ProjectRoot { get; }

    string? RelativePathIfInsideMediaRoot(string path, string? mediaRoot);

    string? ResolveLocalPath(string path, string? mediaRoot);

    string ResolveProjectPath(string path);

    string NormalizeRelativePath(string path);
}

public sealed class ProjectPathResolver : IProjectPathResolver
{
    public ProjectPathResolver(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException(
                "Project root is required.",
                nameof(projectRoot));
        }

        ProjectRoot = Path.GetFullPath(projectRoot);
    }

    public string ProjectRoot { get; }

    public string? RelativePathIfInsideMediaRoot(
        string path,
        string? mediaRoot)
    {
        if (string.IsNullOrWhiteSpace(path)
            || string.IsNullOrWhiteSpace(mediaRoot))
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        var fullRoot = ResolveProjectPath(mediaRoot);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        return relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relative)
            ? path
            : relative;
    }

    public string? ResolveLocalPath(string path, string? mediaRoot)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        return Path.IsPathFullyQualified(path)
            ? path
            : !string.IsNullOrWhiteSpace(mediaRoot)
                ? Path.GetFullPath(Path.Combine(ResolveProjectPath(mediaRoot), path))
                : ResolveProjectPath(path);
    }

    public string ResolveProjectPath(string path)
    {
        if (Path.IsPathFullyQualified(path)) return path;
        return Path.GetFullPath(Path.Combine(ProjectRoot, path));
    }

    public string NormalizeRelativePath(string path)
    {
        return path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
