using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorFieldPostCommitEffects
{
    private readonly SpikeDatabase _database;
    private readonly Func<string?> _selectedPreviewDeviceId;
    private readonly Action<string> _setEditorTitle;
    private readonly Action _rebuildNavigation;
    private readonly Action _refreshPreview;
    private readonly Action _refreshPreviewOptions;

    public EditorFieldPostCommitEffects(
        SpikeDatabase database,
        Func<string?> selectedPreviewDeviceId,
        Action<string> setEditorTitle,
        Action rebuildNavigation,
        Action refreshPreview,
        Action refreshPreviewOptions)
    {
        _database = database;
        _selectedPreviewDeviceId = selectedPreviewDeviceId;
        _setEditorTitle = setEditorTitle;
        _rebuildNavigation = rebuildNavigation;
        _refreshPreview = refreshPreview;
        _refreshPreviewOptions = refreshPreviewOptions;
    }

    public void Apply(ProjectTreeNode node, string fieldId, string value)
    {
        if (fieldId == "core.name")
        {
            _setEditorTitle(node.Name);
            _refreshPreviewOptions();
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
            var settings = _database.GetThemeSettings(node.Id);
            var linkedCount = new[] { settings.IconThemeId, settings.StatusBarId, settings.NavigationBarId }
                .Count((id) => !string.IsNullOrWhiteSpace(id));
            node.Notes = $"{settings.Family} · {linkedCount}/3 refs";
            _rebuildNavigation();
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.ProductionFont && fieldId == "font.category")
        {
            var fileCount = _database
                .GetProductionFontFieldValue(node.Id, "font.files")
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Length;
            node.Notes = $"{value} · {fileCount} files";
            _rebuildNavigation();
            return;
        }

    }
}
