using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.Integrations.ShotManager;

internal sealed record ShotManagerFolderCreation(
    IReadOnlyList<string> CreatedDirectories);

internal sealed class ShotManagerFolderMaterializer
{
    public Task<ShotManagerFolderCreation> CreateAsync(
        ShotManagerExternalShotPlan plan,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ShotManagerPortableStructure structure;
        try
        {
            structure = plan.ToPortableStructure();
        }
        catch (InvalidOperationException exception)
        {
            throw new IOException(
                "Shot Manager returned an unsafe portable directory plan.",
                exception);
        }
        var resolved = ValidateAndResolve(
            plan.RootPath,
            structure.Directories,
            plan.Directories.ToDictionary(
                (directory) => directory.RelativePath,
                (directory) => directory.ResolvedPath,
                StringComparer.Ordinal));
        foreach (var relativeRoot in structure.ShotOwnedDirectories)
        {
            var fullRoot = resolved[relativeRoot];
            if (File.Exists(fullRoot) || Directory.Exists(fullRoot))
            {
                throw new IOException(
                    $"The official Shot folder already exists: {fullRoot}");
            }
        }
        return Task.FromResult(CreateDirectories(
            plan.RootPath,
            structure.Directories,
            resolved,
            cancellationToken));
    }

    public Task<ShotManagerFolderCreation> RepairAsync(
        string rootPath,
        ShotManagerPortableStructure structure,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        structure.Validate("Stored Shot Manager Shot structure");
        var resolved = ValidateAndResolve(
            rootPath,
            structure.Directories,
            expectedResolvedPaths: null);
        return Task.FromResult(CreateDirectories(
            rootPath,
            structure.Directories,
            resolved,
            cancellationToken));
    }

    public Task RollbackAsync(
        ShotManagerFolderCreation creation,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<Exception>();
        foreach (var directory in creation.CreatedDirectories.Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (Directory.Exists(directory)
                    && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory, recursive: false);
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        if (failures.Count > 0)
        {
            throw new AggregateException(
                "The local database write failed and some newly created empty Shot folders could not be removed.",
                failures);
        }
        return Task.CompletedTask;
    }

    private static ShotManagerFolderCreation CreateDirectories(
        string rootPath,
        IReadOnlyList<string> relativeDirectories,
        IReadOnlyDictionary<string, string> resolved,
        CancellationToken cancellationToken)
    {
        var created = new List<string>();
        try
        {
            foreach (var relativePath in relativeDirectories
                .OrderBy(PathDepth)
                .ThenBy((path) => path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullPath = resolved[relativePath];
                ValidateExistingChain(rootPath, fullPath);
                if (Directory.Exists(fullPath))
                {
                    continue;
                }
                if (File.Exists(fullPath))
                {
                    throw new IOException(
                        $"A file occupies the official Shot directory: {fullPath}");
                }
                Directory.CreateDirectory(fullPath);
                created.Add(fullPath);
            }
            return new ShotManagerFolderCreation(created);
        }
        catch (Exception creationError)
        {
            var cleanupFailures = new List<Exception>();
            foreach (var directory in created.AsEnumerable().Reverse())
            {
                try
                {
                    if (Directory.Exists(directory)
                        && !Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory, recursive: false);
                    }
                }
                catch (Exception cleanupError)
                {
                    cleanupFailures.Add(cleanupError);
                }
            }
            if (cleanupFailures.Count == 0)
            {
                throw;
            }
            throw new AggregateException(
                "Shot folder creation failed and some newly created empty folders could not be removed.",
                [creationError, .. cleanupFailures]);
        }
    }

    private static IReadOnlyDictionary<string, string> ValidateAndResolve(
        string rootPath,
        IReadOnlyList<string> relativeDirectories,
        IReadOnlyDictionary<string, string>? expectedResolvedPaths)
    {
        if (!Path.IsPathFullyQualified(rootPath))
        {
            throw new IOException(
                "Shot Manager returned a non-absolute Production root.");
        }
        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"The Shot Manager Production root is not available: {root}");
        }
        if (IsSymbolicLink(new DirectoryInfo(root)))
        {
            throw new IOException(
                "The Shot Manager Production root cannot be a symbolic link.");
        }
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var resolved = new Dictionary<string, string>(
            relativeDirectories.Count,
            StringComparer.Ordinal);
        foreach (var relativePath in relativeDirectories)
        {
            ValidatePortableRelativePath(relativePath);
            var segments = relativePath.Split('/');
            var fullPath = Path.GetFullPath(
                Path.Combine([root, .. segments]));
            ValidateContained(root, fullPath);
            if (expectedResolvedPaths is not null
                && (!expectedResolvedPaths.TryGetValue(
                        relativePath,
                        out var expected)
                    || !Path.IsPathFullyQualified(expected)
                    || !comparer.Equals(
                        Path.GetFullPath(expected),
                        fullPath)))
            {
                throw new IOException(
                    $"Shot Manager returned an inconsistent resolved path for '{relativePath}'.");
            }
            resolved.Add(relativePath, fullPath);
            ValidateExistingChain(root, fullPath);
        }
        return resolved;
    }

    private static void ValidatePortableRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || relativePath.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relativePath))
        {
            throw new IOException(
                $"Shot Manager returned an invalid portable directory: '{relativePath}'.");
        }
        var segments = relativePath.Split('/');
        if (segments.Any((segment) =>
            string.IsNullOrWhiteSpace(segment)
            || segment is "." or ".."))
        {
            throw new IOException(
                $"Shot Manager returned an unsafe portable directory: '{relativePath}'.");
        }
    }

    private static void ValidateContained(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        if (Path.IsPathFullyQualified(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new IOException(
                "A Shot Manager directory escapes the Production root.");
        }
    }

    private static void ValidateExistingChain(
        string rootPath,
        string targetPath)
    {
        var root = Path.GetFullPath(rootPath);
        ValidateContained(root, targetPath);
        var relative = Path.GetRelativePath(root, targetPath);
        var current = root;
        foreach (var segment in relative.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) && !Directory.Exists(current))
            {
                throw new IOException(
                    $"A file occupies part of the official Shot path: {current}");
            }
            if (Directory.Exists(current)
                && IsSymbolicLink(new DirectoryInfo(current)))
            {
                throw new IOException(
                    $"The official Shot path crosses a symbolic link: {current}");
            }
        }
    }

    private static bool IsSymbolicLink(FileSystemInfo info)
    {
        return info.LinkTarget is not null
            || info.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private static int PathDepth(string path)
    {
        return path.Count((character) => character == '/');
    }
}
