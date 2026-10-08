using Microsoft.Data.Sqlite;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteResourceOwner
{
    internal ResourceAssetCleanupService AssetCleanup { get; }

    internal void DeleteProductionFont(SqliteConnection connection, string id)
    {
        var font = _productionFontRepository.Get(connection, id);
        var root = ResolveProjectPath(GetProjectSettings(connection, font.ProjectId).MediaRoot);
        var plan = ResourceAssetCleanupPlan.Capture(font.FamilyName, root, font.SourceDirectory);
        AssetCleanup.Commit(connection, [plan], transaction => _productionFontRepository.Delete(connection, id, transaction));
    }

    internal void DeleteIconTheme(SqliteConnection connection, string id)
    {
        var theme = _iconThemeRepository.Get(connection, id);
        var directory = IconThemeAssetDirectory(theme.AssetRoot);
        var plan = ResourceAssetCleanupPlan.Capture(theme.Name, SystemIconThemesRoot(), Path.GetRelativePath(SystemIconThemesRoot(), directory));
        AssetCleanup.Commit(connection, [plan], transaction => _iconThemeRepository.Delete(connection, id, transaction));
    }

    private bool CleanupTargetIsReferenced(SqliteConnection connection, ResourceAssetCleanupPlan plan)
    {
        var target = ResourceAssetCleanupPlan.ContainedPath(plan.Root, plan.Target);
        foreach (var font in _productionFontRepository.QueryAll(connection))
        {
            var mediaRoot = GetProjectSettings(connection, font.ProjectId).MediaRoot;
            if (string.IsNullOrWhiteSpace(mediaRoot) || string.IsNullOrWhiteSpace(font.SourceDirectory)) continue;
            var directory = ResourceAssetCleanupPlan.ContainedPath(ResourceAssetCleanupPlan.StoredPath(ResolveProjectPath(mediaRoot)),
                ResourceAssetCleanupPlan.StoredPath(font.SourceDirectory));
            if (ResourceAssetCleanupPlan.Overlaps(target, directory)) return true;
        }
        foreach (var theme in _iconThemeRepository.QueryAll(connection))
        {
            var directory = IconThemeAssetDirectory(theme.AssetRoot);
            if (plan.Entries[0].Kind == "directory")
            {
                if (ResourceAssetCleanupPlan.Overlaps(target, directory)) return true;
            }
            else if (IconThemeTokens(theme.MappingJson).Any(token =>
                         ResourceAssetCleanupPlan.Overlaps(target, Path.Combine(directory, token.File)))) return true;
        }
        return false;
    }
}
