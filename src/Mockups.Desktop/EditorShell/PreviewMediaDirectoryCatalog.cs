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
        string preparedPreviewJson)
    {
        if (string.IsNullOrWhiteSpace(projectMediaRoot)) return [];
        var mediaRoot = Path.GetFullPath(projectMediaRoot);
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
