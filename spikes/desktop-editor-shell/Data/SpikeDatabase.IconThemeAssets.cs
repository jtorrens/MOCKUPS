using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private IconThemeAssetMoveResult DuplicateIconThemeAssets(SqliteConnection connection, IconThemeRow source, string targetName)
    {
        var sourceDirectory = IconThemeAssetDirectory(connection, source.ProjectId, source.AssetRoot);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new InvalidOperationException($"Missing icon theme asset directory '{source.AssetRoot}'.");
        }

        var mediaRoot = ResolveProjectPath(GetProjectSettings(connection, source.ProjectId).MediaRoot);
        var iconThemesRoot = Path.Combine(mediaRoot, "icon-themes");
        Directory.CreateDirectory(iconThemesRoot);
        var targetDirectory = UniqueIconThemeDirectory(iconThemesRoot, IconThemeDirectoryName(targetName));
        CopyDirectory(sourceDirectory, targetDirectory);
        RewriteIconThemeManifestName(targetDirectory, Path.GetFileName(targetDirectory));
        return new IconThemeAssetMoveResult(
            NormalizeRelativePath(Path.GetRelativePath(mediaRoot, targetDirectory)),
            Path.GetFileName(targetDirectory));
    }

    private IconThemeAssetMoveResult RenameIconThemeAssets(SqliteConnection connection, IconThemeRow source, string targetName)
    {
        var sourceDirectory = IconThemeAssetDirectory(connection, source.ProjectId, source.AssetRoot);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new InvalidOperationException($"Missing icon theme asset directory '{source.AssetRoot}'.");
        }

        var mediaRoot = ResolveProjectPath(GetProjectSettings(connection, source.ProjectId).MediaRoot);
        var iconThemesRoot = Path.Combine(mediaRoot, "icon-themes");
        Directory.CreateDirectory(iconThemesRoot);
        var targetDirectory = Path.Combine(iconThemesRoot, IconThemeDirectoryName(targetName));
        if (Path.GetFullPath(sourceDirectory).Equals(Path.GetFullPath(targetDirectory), StringComparison.Ordinal))
        {
            RewriteIconThemeManifestName(sourceDirectory, Path.GetFileName(sourceDirectory));
            return new IconThemeAssetMoveResult(
                NormalizeRelativePath(Path.GetRelativePath(mediaRoot, sourceDirectory)),
                Path.GetFileName(sourceDirectory));
        }

        if (Directory.Exists(targetDirectory))
        {
            throw new InvalidOperationException($"Icon theme folder '{Path.GetFileName(targetDirectory)}' already exists.");
        }

        Directory.Move(sourceDirectory, targetDirectory);
        RewriteIconThemeManifestName(targetDirectory, Path.GetFileName(targetDirectory));
        return new IconThemeAssetMoveResult(
            NormalizeRelativePath(Path.GetRelativePath(mediaRoot, targetDirectory)),
            Path.GetFileName(targetDirectory));
    }

    private static string IconThemeAssetDirectory(SqliteConnection connection, string projectId, string assetRoot)
    {
        var mediaRoot = ResolveProjectPath(GetProjectSettings(connection, projectId).MediaRoot);
        return Path.GetFullPath(Path.Combine(mediaRoot, assetRoot));
    }

    private static string UniqueIconThemeDirectory(string iconThemesRoot, string directoryName)
    {
        var safeName = string.IsNullOrWhiteSpace(directoryName) ? "Icon Theme" : directoryName;
        var candidate = Path.Combine(iconThemesRoot, safeName);
        var index = 2;
        while (Directory.Exists(candidate))
        {
            candidate = Path.Combine(iconThemesRoot, $"{safeName} {index}");
            index++;
        }

        return candidate;
    }

    private static string IconThemeDirectoryName(string name)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var directoryName = new string(name.Trim().Select((character) =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(directoryName) ? "Icon Theme" : directoryName;
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, file)), overwrite: false);
        }
    }

    private static void RewriteIconThemeManifestName(string directory, string setName)
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath)) return;

        try
        {
            var manifest = ParseJsonObject(File.ReadAllText(manifestPath));
            manifest["name"] = setName;
            File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (JsonException)
        {
            // A malformed manifest should not block duplicating or renaming the icon theme.
        }
    }

    private static void DeleteIconThemeAssetDirectory(SqliteConnection connection, string projectId, string assetRoot)
    {
        var mediaRoot = ResolveProjectPath(GetProjectSettings(connection, projectId).MediaRoot);
        var targetDirectory = Path.GetFullPath(Path.Combine(mediaRoot, assetRoot));
        var fullMediaRoot = Path.GetFullPath(mediaRoot);
        var relative = Path.GetRelativePath(fullMediaRoot, targetDirectory);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative)) return;
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }
    }
}
