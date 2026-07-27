using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorFieldPostCommitEffects
{
    private readonly EditorPresentationContextDataSource _contextData;
    private readonly EditorOperationCoordinator _operations;
    private readonly Func<string?> _selectedPreviewDeviceId;
    private readonly Action<string> _setEditorTitle;
    private readonly Action _rebuildNavigation;
    private readonly Action _refreshPreview;
    private readonly Action _refreshPreviewOptions;
    private readonly Action _refreshProductionNavigation;

    public EditorFieldPostCommitEffects(
        IEditorPresentationContextRepository database,
        EditorOperationCoordinator operations,
        Func<string?> selectedPreviewDeviceId,
        Action<string> setEditorTitle,
        Action rebuildNavigation,
        Action refreshPreview,
        Action refreshPreviewOptions,
        Action refreshProductionNavigation)
    {
        _contextData = new EditorPresentationContextDataSource(database);
        _operations = operations;
        _selectedPreviewDeviceId = selectedPreviewDeviceId;
        _setEditorTitle = setEditorTitle;
        _rebuildNavigation = rebuildNavigation;
        _refreshPreview = refreshPreview;
        _refreshPreviewOptions = refreshPreviewOptions;
        _refreshProductionNavigation = refreshProductionNavigation;
    }

    public async Task ApplyAsync(
        ProjectTreeNode node,
        string fieldId,
        string value)
    {
        var prepared = RequiresPersistenceRead(
            node,
            fieldId)
            ? await _operations.ExecuteAsync(
                () => Prepare(
                    node,
                    fieldId))
            : EditorPostCommitReadSnapshot.Empty;
        ApplyVisual(
            node,
            fieldId,
            value,
            prepared);
    }

    private void ApplyVisual(
        ProjectTreeNode node,
        string fieldId,
        string value,
        EditorPostCommitReadSnapshot prepared)
    {
        if (fieldId == "core.name")
        {
            _setEditorTitle(node.Name);
            _rebuildNavigation();
            _refreshPreviewOptions();
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Project
            && fieldId.StartsWith(
                "project.production",
                StringComparison.Ordinal))
        {
            _refreshProductionNavigation();
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.PaletteColor && fieldId == "palette.token")
        {
            node.Name = value;
            _setEditorTitle(value);
            _rebuildNavigation();
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.PaletteColor && fieldId == "palette.valueHex")
        {
            node.ColorHex = value;
            _rebuildNavigation();
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Device && _selectedPreviewDeviceId() == node.Id)
        {
            _refreshPreview();
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Actor && fieldId == "actor.shortName")
        {
            node.Notes = value;
            _rebuildNavigation();
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Theme &&
            fieldId is "theme.family" or "theme.iconThemeId" or "theme.statusBarId" or "theme.navigationBarId")
        {
            var settings = prepared.Theme
                ?? throw new InvalidOperationException(
                    "Theme post-commit context was not prepared.");
            var linkedCount = new[] { settings.IconThemeId, settings.StatusBarId, settings.NavigationBarId }
                .Count((id) => !string.IsNullOrWhiteSpace(id));
            node.Notes = $"{settings.Family} · {linkedCount}/3 refs";
            _rebuildNavigation();
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.ProductionFont && fieldId == "font.category")
        {
            node.Notes =
                $"{value} · {prepared.ProductionFontFileCount} files";
            _rebuildNavigation();
            return;
        }

    }

    private EditorPostCommitReadSnapshot Prepare(
        ProjectTreeNode node,
        string fieldId)
    {
        if (node.Kind == ProjectTreeNodeKind.Theme)
        {
            return new EditorPostCommitReadSnapshot(
                _contextData.ThemeNavigation(node.Id),
                0);
        }
        if (node.Kind == ProjectTreeNodeKind.ProductionFont
            && fieldId == "font.category")
        {
            var fileCount = _contextData
                .ProductionFontFiles(node.Id)
                .Split(
                    Environment.NewLine,
                    StringSplitOptions.RemoveEmptyEntries)
                .Length;
            return new EditorPostCommitReadSnapshot(
                null,
                fileCount);
        }
        return EditorPostCommitReadSnapshot.Empty;
    }

    private static bool RequiresPersistenceRead(
        ProjectTreeNode node,
        string fieldId) =>
        node.Kind == ProjectTreeNodeKind.Theme
            && fieldId is
                "theme.family"
                or "theme.iconThemeId"
                or "theme.statusBarId"
                or "theme.navigationBarId"
        || node.Kind == ProjectTreeNodeKind.ProductionFont
            && fieldId == "font.category";

    private sealed record EditorPostCommitReadSnapshot(
        EditorThemeNavigationSource? Theme,
        int ProductionFontFileCount)
    {
        public static EditorPostCommitReadSnapshot Empty { get; } =
            new(null, 0);
    }
}
