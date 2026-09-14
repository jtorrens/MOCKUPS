using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Common;

public sealed class SystemAssetPathResolver
{
    private SystemAssetPathResolver(string root)
    {
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }

    public static SystemAssetPathResolver Discover()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "assets", "system");
            if (File.Exists(Path.Combine(directory.FullName, "package.json"))
                && Directory.Exists(candidate))
            {
                return new SystemAssetPathResolver(candidate);
            }

            directory = directory.Parent;
        }

        var packaged = Path.Combine(AppContext.BaseDirectory, "assets", "system");
        if (Directory.Exists(packaged))
        {
            return new SystemAssetPathResolver(packaged);
        }

        throw new InvalidOperationException(
            "Could not locate the MOCKUPS System asset root.");
    }

    public string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidOperationException(
                "System asset paths must be non-empty relative paths.");
        }

        var resolved = Path.GetFullPath(Path.Combine(Root, relativePath));
        var relative = Path.GetRelativePath(Root, resolved);
        if (relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relative))
        {
            throw new InvalidOperationException(
                $"System asset path '{relativePath}' escapes the System asset root.");
        }

        return resolved;
    }
}
