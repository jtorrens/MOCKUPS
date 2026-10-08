using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System.Text;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteResourceOwner
{
    internal IconThemeRecord DuplicateIconTheme(SqliteConnection connection, IconThemeRecord source, string id, string targetName)
    {
        lock (_context.WriteGate)
        {
            source = IconThemeRepository.Get(connection, source.Id);
            var targetDirectory = UniqueIconThemeDirectory(SystemIconThemesRoot(), IconThemeDirectoryName(targetName));
            TransferIconTheme(connection, source, targetDirectory, false,
                (transaction, name, assetRoot, metadata) => IconThemeRepository.CreateDuplicate(
                    connection, source.Id, id, name, assetRoot, metadata, transaction));
            return IconThemeRepository.Get(connection, id);
        }
    }

    internal void RenameIconTheme(SqliteConnection connection, IconThemeRecord source, string targetName)
    {
        lock (_context.WriteGate)
        {
            source = IconThemeRepository.Get(connection, source.Id);
            var targetDirectory = Path.Combine(SystemIconThemesRoot(), IconThemeDirectoryName(targetName));
            TransferIconTheme(connection, source, targetDirectory, true,
                (transaction, name, assetRoot, metadata) => IconThemeRepository.UpdateIdentity(
                    connection, source.Id, name, assetRoot, metadata, transaction));
        }
    }

    private void TransferIconTheme(SqliteConnection connection, IconThemeRecord source, string destination, bool retireSource,
        Action<SqliteTransaction, string, string, string> writeRecord)
    {
        var sourceDirectory = IconThemeAssetDirectory(source.AssetRoot);
        var name = Path.GetFileName(destination);
        var assetRoot = NormalizeRelativePath(Path.GetRelativePath(_systemAssets.Root, destination));
        var metadata = JsonPath.ParseRequiredObject(source.MetadataJson, $"Icon Theme '{source.Id}' metadata");
        JsonPath.RequiredObject(metadata, "iconSet", source.Id)["setName"] = name;
        if (metadata.ContainsKey("manifest"))
            JsonPath.RequiredObject(metadata, "manifest", source.Id)["name"] = name;
        AssetCleanup.TransferDirectory(connection, $"Icon Theme '{source.Name}'", SystemIconThemesRoot(),
            NormalizeRelativePath(Path.GetRelativePath(SystemIconThemesRoot(), sourceDirectory)),
            NormalizeRelativePath(Path.GetRelativePath(SystemIconThemesRoot(), destination)), retireSource,
            files =>
            {
                if (files.TryGetValue("manifest.json", out var bytes))
                {
                    var manifest = JsonPath.ParseRequiredObject(new UTF8Encoding(false, true).GetString(bytes), source.Id + " manifest");
                    manifest["name"] = name;
                    files["manifest.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString());
                    metadata["manifest"] = manifest.DeepClone();
                }
            }, transaction => writeRecord(transaction, name, assetRoot, metadata.ToJsonString()));
    }

    internal string IconThemeAssetDirectory(string assetRoot)
    {
        var directory = ResolveSystemAssetPath(assetRoot);
        var relative = Path.GetRelativePath(SystemIconThemesRoot(), directory);
        ResourceAssetCleanupPlan.ContainedPath(ResourceAssetCleanupPlan.StoredPath(SystemIconThemesRoot()),
            ResourceAssetCleanupPlan.StoredPath(relative));
        return directory;
    }

    internal string SystemIconThemesRoot() => ResolveSystemAssetPath("icon-themes");

    private static string UniqueIconThemeDirectory(string iconThemesRoot, string directoryName)
    {
        var candidate = Path.Combine(iconThemesRoot, directoryName);
        var index = 2;
        while (Directory.Exists(candidate) || File.Exists(candidate))
            candidate = Path.Combine(iconThemesRoot, $"{directoryName} {index++}");
        return candidate;
    }

    private static string IconThemeDirectoryName(string name)
    {
        var directoryName = name.Trim();
        if (string.IsNullOrWhiteSpace(directoryName) || directoryName is "." or ".."
            || directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || directoryName.Contains('\\') || directoryName.Contains(':'))
            throw new InvalidOperationException("Icon Theme name must be a valid directory name.");
        return directoryName;
    }
}
