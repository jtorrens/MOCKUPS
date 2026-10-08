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
            TransferIconTheme(connection, source, id, targetDirectory, false,
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
            TransferIconTheme(connection, source, source.Id, targetDirectory, true,
                (transaction, name, assetRoot, metadata) => IconThemeRepository.UpdateIdentity(
                    connection, source.Id, name, assetRoot, metadata, transaction));
        }
    }

    private void TransferIconTheme(SqliteConnection connection, IconThemeRecord source, string targetId, string destination, bool retireSource,
        Action<SqliteTransaction, string, string, string> writeRecord)
    {
        var sourceDirectory = IconThemeAssetDirectory(source.AssetRoot);
        var name = Path.GetFileName(destination);
        var assetRoot = NormalizeRelativePath(Path.GetRelativePath(_systemAssets.Root, destination));
        var metadata = IconThemeImportDocument.ValidateMetadata(source.MetadataJson);
        JsonPath.RequiredObject(metadata, "iconSet", source.Id)["setName"] = name;
        AssetCleanup.TransferDirectory(connection, $"Icon Theme '{source.Name}'", SystemIconThemesRoot(),
            NormalizeRelativePath(Path.GetRelativePath(SystemIconThemesRoot(), sourceDirectory)),
            NormalizeRelativePath(Path.GetRelativePath(SystemIconThemesRoot(), destination)), retireSource,
            files =>
            {
                if (!files.TryGetValue("manifest.json", out var bytes))
                    throw new InvalidOperationException("Icon Theme directory has no identity manifest.");
                var manifest = IconThemeImportDocument.Read(new UTF8Encoding(false, true).GetString(bytes));
                if (manifest.Id != source.Id) throw new InvalidOperationException("Icon Theme directory identity does not match its row.");
                files["manifest.json"] = Encoding.UTF8.GetBytes(new IconThemeImportDocument(
                    targetId, name, source.MappingJson, metadata.ToJsonString()).ToJson());
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
