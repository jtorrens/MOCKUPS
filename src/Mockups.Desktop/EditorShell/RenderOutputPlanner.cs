using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record RenderOutputPlan(
    int Version,
    IReadOnlyDictionary<string, string> OutputPaths);

internal static class RenderOutputPlanner
{
    public static string SuggestedBaseName(string value)
    {
        var builder = new StringBuilder();
        var pendingSeparator = false;
        foreach (var character in value.Trim())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                }
                builder.Append(char.ToUpperInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = builder.Length > 0;
            }
        }
        return builder.ToString().Trim('_');
    }

    public static string RequireBaseName(string value)
    {
        var result = value.Trim();
        if (result.Length is < 1 or > 160
            || result is "." or ".."
            || result.Any((character) =>
                !(char.IsAsciiLetterOrDigit(character)
                    || character is '_' or '-' or '.')))
        {
            throw new InvalidOperationException(
                "The output base name may only contain letters, numbers, underscore, dash and dot.");
        }
        return result;
    }

    public static string FileStem(
        string baseName,
        string appearance,
        int version,
        int versionPadding)
    {
        var safeBaseName = RequireBaseName(baseName);
        if (appearance is not RenderQueueAppearance.Light
            and not RenderQueueAppearance.Dark
            || version <= 0
            || versionPadding is < 1 or > 8)
        {
            throw new InvalidOperationException(
                "Output naming requires a valid appearance, version and padding.");
        }
        return $"{safeBaseName}_{appearance.ToUpperInvariant()}_v{version.ToString().PadLeft(versionPadding, '0')}";
    }

    public static RenderOutputPlan Suggest(
        string rootPath,
        string relativeDirectory,
        string baseName,
        IReadOnlyList<string> appearances,
        RenderOutputModeDefinition mode,
        int versionPadding,
        IReadOnlySet<string>? reservedOutputPaths = null)
    {
        var directory = RenderOutputPathSecurity.RequirePlannedDirectory(
            rootPath,
            relativeDirectory);
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var reserved = reservedOutputPaths is null
            ? new HashSet<string>(comparer)
            : new HashSet<string>(
                reservedOutputPaths.Select(Path.GetFullPath),
                comparer);
        var uniqueAppearances = appearances
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (uniqueAppearances.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one render appearance is required.");
        }

        for (var version = 1; version <= 99_999_999; version++)
        {
            var paths = uniqueAppearances.ToDictionary(
                (appearance) => appearance,
                (appearance) =>
                {
                    var stem = FileStem(
                        baseName,
                        appearance,
                        version,
                        versionPadding);
                    return Path.Combine(
                        directory,
                        mode.Kind == "mov"
                            ? $"{stem}.{mode.Extension}"
                            : stem);
                },
                StringComparer.Ordinal);
            if (paths.Values.All((path) =>
                !File.Exists(path)
                && !Directory.Exists(path)
                && !reserved.Contains(Path.GetFullPath(path))))
            {
                return new RenderOutputPlan(version, paths);
            }
        }
        throw new InvalidOperationException(
            "No free output version could be resolved.");
    }
}

internal static class RenderOutputPathSecurity
{
    public static string RequirePlannedDirectory(
        string rootPath,
        string relativeDirectory)
    {
        return ResolveDirectory(
            rootPath,
            relativeDirectory,
            createMissing: false);
    }

    public static void EnsureOutputDirectory(
        RenderOutputTarget output)
    {
        RequireOutputTarget(output);
        _ = ResolveDirectory(
            output.RootPath,
            output.RelativeDirectory,
            createMissing: true);
        RequireOutputTarget(output);
    }

    private static string ResolveDirectory(
        string rootPath,
        string relativeDirectory,
        bool createMissing)
    {
        if (!Path.IsPathFullyQualified(rootPath))
        {
            throw new InvalidOperationException(
                "The local Production Output root is unavailable.");
        }
        var root = Path.GetFullPath(rootPath);
        RequireRealDirectory(root, "Production Output root");
        if (string.IsNullOrWhiteSpace(relativeDirectory)
            || Path.IsPathFullyQualified(relativeDirectory)
            || relativeDirectory.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Production Output route is not portable.");
        }
        var segments = relativeDirectory.Split('/');
        if (segments.Any((segment) =>
            string.IsNullOrWhiteSpace(segment)
            || segment is "." or ".."))
        {
            throw new InvalidOperationException(
                "The Production Output route is unsafe.");
        }
        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current))
            {
                RequireRealDirectory(
                    current,
                    "Production Output route");
                continue;
            }
            if (File.Exists(current))
            {
                throw new IOException(
                    $"Production Output route crosses a file: {current}");
            }
            if (createMissing)
            {
                Directory.CreateDirectory(current);
                RequireRealDirectory(
                    current,
                    "Production Output route");
            }
        }
        var relative = Path.GetRelativePath(root, current);
        if (Path.IsPathFullyQualified(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Production Output route escapes its local root.");
        }
        return current;
    }

    public static void RequireOutputTarget(RenderOutputTarget output)
    {
        var directory = RequirePlannedDirectory(
            output.RootPath,
            output.RelativeDirectory);
        var expectedStem = RenderOutputPlanner.FileStem(
            output.BaseName,
            output.Appearance,
            output.Version,
            output.VersionPadding);
        var mode = RenderOutputModes.Require(output.OutputModeId);
        var expected = Path.Combine(
            directory,
            mode.Kind == "mov"
                ? $"{expectedStem}.{mode.Extension}"
                : expectedStem);
        var comparer = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!Path.GetFullPath(expected).Equals(
            Path.GetFullPath(output.OutputPath),
            comparer))
        {
            throw new InvalidOperationException(
                "The queued output path does not match its frozen Production Output route and MOCKUPS name.");
        }
        if (File.Exists(expected) || Directory.Exists(expected))
        {
            throw new IOException(
                "The queued output already exists. Add a new render to resolve another version.");
        }
    }

    private static void RequireRealDirectory(string path, string label)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(
                $"{label} does not exist: {path}");
        }
        var info = new DirectoryInfo(path);
        if (info.LinkTarget is not null
            || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException(
                $"{label} cannot cross a symbolic link: {path}");
        }
    }
}
