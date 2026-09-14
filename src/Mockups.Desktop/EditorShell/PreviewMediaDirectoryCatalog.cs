using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class PreviewMediaDirectoryCatalog
{
    public static IReadOnlyList<string> Resolve(
        string projectMediaRoot,
        string preparedPreviewJson,
        string systemPreviewFixtureRoot = "")
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectMediaDirectories(
            JsonPath.ParseRequiredObject(
                preparedPreviewJson,
                "Prepared Preview media-directory inputs"),
            directories);
        if (directories.Count == 0) return [];

        var extensions = new HashSet<string>(
            [".png", ".jpg", ".jpeg", ".webp", ".heic", ".mp4", ".mov", ".m4v", ".webm"],
            StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            if (directory.StartsWith(
                    SystemPreviewFixtureCatalog.MediaScheme,
                    StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(systemPreviewFixtureRoot))
                {
                    throw new InvalidOperationException(
                        $"System Preview media directory '{directory}' is not allowed outside Design Preview.");
                }
                AddSystemFixtureFiles(
                    files,
                    directory,
                    systemPreviewFixtureRoot,
                    extensions);
                continue;
            }
            if (string.IsNullOrWhiteSpace(projectMediaRoot))
            {
                throw new InvalidOperationException(
                    $"Media directory '{directory}' requires a Project media root.");
            }
            var mediaRoot = Path.GetFullPath(projectMediaRoot);
            var fullDirectory = Path.GetFullPath(
                Path.IsPathFullyQualified(directory)
                    ? directory
                    : Path.Combine(mediaRoot, directory));
            if (!IsInsideRoot(fullDirectory, mediaRoot))
            {
                throw new InvalidOperationException(
                    $"Media directory '{directory}' must be inside the Project media root.");
            }
            if (!Directory.Exists(fullDirectory))
            {
                throw new InvalidOperationException(
                    $"Media directory '{directory}' does not exist in the Project media root.");
            }
            foreach (var file in Directory.EnumerateFiles(
                         fullDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                if (!extensions.Contains(Path.GetExtension(file))) continue;
                files.Add(Path.GetRelativePath(mediaRoot, file).Replace('\\', '/'));
            }
        }
        return files.OrderBy((file) => file, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddSystemFixtureFiles(
        ISet<string> files,
        string reference,
        string fixtureRoot,
        ISet<string> extensions)
    {
        var root = Path.GetFullPath(fixtureRoot);
        var relativeDirectory = reference[SystemPreviewFixtureCatalog.MediaScheme.Length..];
        var directory = Path.GetFullPath(Path.Combine(root, relativeDirectory));
        if (!IsInsideRoot(directory, root) || !Directory.Exists(directory))
        {
            throw new InvalidOperationException(
                $"System Preview media directory '{reference}' is missing or escapes its fixture root.");
        }
        foreach (var file in Directory.EnumerateFiles(
                     directory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if (!extensions.Contains(Path.GetExtension(file))) continue;
            files.Add(
                SystemPreviewFixtureCatalog.MediaScheme
                + Path.GetRelativePath(root, file).Replace('\\', '/'));
        }
    }

    private static void CollectMediaDirectories(
        JsonNode? node,
        ISet<string> directories)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array) CollectMediaDirectories(child, directories);
            return;
        }
        if (node is not JsonObject value) return;

        if (value["inputs"] is JsonArray inputs)
        {
            foreach (var definitionNode in inputs)
            {
                if (definitionNode is not JsonObject definition
                    || !JsonPath.RequiredString(
                            definition,
                            "valueKind",
                            "Prepared Preview Runtime Input")
                        .Equals(
                            ValueKind.MediaDirectoryPath.ToString(),
                            StringComparison.Ordinal))
                {
                    continue;
                }
                var jsonKey = JsonPath.RequiredString(
                    definition,
                    "jsonKey",
                    "Prepared Preview media-directory input");
                directories.Add(JsonPath.RequiredString(
                    value,
                    jsonKey,
                    "Prepared Preview media-directory value"));
            }
        }

        foreach (var (_, child) in value)
        {
            CollectMediaDirectories(child, directories);
        }
    }

    private static bool IsInsideRoot(string candidate, string root)
    {
        if (candidate.Equals(root, StringComparison.Ordinal)) return true;
        return candidate.StartsWith(
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
    }
}
