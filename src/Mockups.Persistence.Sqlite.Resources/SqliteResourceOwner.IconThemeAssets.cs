using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteResourceOwner
{
    internal IconThemeAssetMoveResult DuplicateIconThemeAssets(SqliteConnection connection, IconThemeRecord source, string targetName)
    {
        var sourceDirectory = IconThemeAssetDirectory(source.AssetRoot);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new InvalidOperationException($"Missing icon theme asset directory '{source.AssetRoot}'.");
        }

        var iconThemesRoot = SystemIconThemesRoot();
        Directory.CreateDirectory(iconThemesRoot);
        var targetDirectory = UniqueIconThemeDirectory(iconThemesRoot, IconThemeDirectoryName(targetName));
        CopyDirectory(sourceDirectory, targetDirectory);
        RewriteIconThemeManifestName(targetDirectory, Path.GetFileName(targetDirectory));
        return new IconThemeAssetMoveResult(
            NormalizeRelativePath(Path.GetRelativePath(_systemAssets.Root, targetDirectory)),
            Path.GetFileName(targetDirectory));
    }

    internal IconThemeAssetMoveResult RenameIconThemeAssets(SqliteConnection connection, IconThemeRecord source, string targetName)
    {
        var sourceDirectory = IconThemeAssetDirectory(source.AssetRoot);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new InvalidOperationException($"Missing icon theme asset directory '{source.AssetRoot}'.");
        }

        var iconThemesRoot = SystemIconThemesRoot();
        Directory.CreateDirectory(iconThemesRoot);
        var targetDirectory = Path.Combine(iconThemesRoot, IconThemeDirectoryName(targetName));
        if (Path.GetFullPath(sourceDirectory).Equals(Path.GetFullPath(targetDirectory), StringComparison.Ordinal))
        {
            RewriteIconThemeManifestName(sourceDirectory, Path.GetFileName(sourceDirectory));
            return new IconThemeAssetMoveResult(
                NormalizeRelativePath(Path.GetRelativePath(_systemAssets.Root, sourceDirectory)),
                Path.GetFileName(sourceDirectory));
        }

        if (Directory.Exists(targetDirectory))
        {
            throw new InvalidOperationException($"Icon theme folder '{Path.GetFileName(targetDirectory)}' already exists.");
        }

        Directory.Move(sourceDirectory, targetDirectory);
        RewriteIconThemeManifestName(targetDirectory, Path.GetFileName(targetDirectory));
        return new IconThemeAssetMoveResult(
            NormalizeRelativePath(Path.GetRelativePath(_systemAssets.Root, targetDirectory)),
            Path.GetFileName(targetDirectory));
    }

    internal string IconThemeAssetDirectory(string assetRoot)
    {
        var directory = ResolveSystemAssetPath(assetRoot);
        var relative = Path.GetRelativePath(SystemIconThemesRoot(), directory);
        if (relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relative))
        {
            throw new InvalidOperationException(
                $"Icon Theme asset root '{assetRoot}' is outside the System Icon Themes root.");
        }

        return directory;
    }

    internal string SystemIconThemesRoot() =>
        ResolveSystemAssetPath("icon-themes");

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

    internal void DeleteIconThemeAssetDirectory(string assetRoot)
    {
        var targetDirectory = IconThemeAssetDirectory(assetRoot);
        var relative = Path.GetRelativePath(SystemIconThemesRoot(), targetDirectory);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative)) return;
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }
    }
}
