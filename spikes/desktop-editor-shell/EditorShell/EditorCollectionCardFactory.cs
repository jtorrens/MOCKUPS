using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorCollectionCardFactory
{
    private readonly SpikeDatabase _database;
    private readonly Func<bool> _isDark;
    private readonly Func<string, string, Task> _showInfo;
    private readonly Func<string, Task<bool>> _confirmIconTokenDelete;
    private readonly Func<ProjectTreeNode, Task> _showIconThemeSearch;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly Func<string, ValueKind, Task<string?>> _browsePath;
    private readonly Func<string, string, bool, Task<string?>> _showIconTokenPicker;
    private readonly Action _onChanged;

    public EditorCollectionCardFactory(
        SpikeDatabase database,
        Func<bool> isDark,
        Func<string, string, Task> showInfo,
        Func<string, Task<bool>> confirmIconTokenDelete,
        Func<ProjectTreeNode, Task> showIconThemeSearch,
        Action<ProjectTreeNode> reloadAndSelect,
        Func<string, ValueKind, Task<string?>> browsePath,
        Func<string, string, bool, Task<string?>> showIconTokenPicker,
        Action onChanged)
    {
        _database = database;
        _isDark = isDark;
        _showInfo = showInfo;
        _confirmIconTokenDelete = confirmIconTokenDelete;
        _showIconThemeSearch = showIconThemeSearch;
        _reloadAndSelect = reloadAndSelect;
        _browsePath = browsePath;
        _showIconTokenPicker = showIconTokenPicker;
        _onChanged = onChanged;
    }

    public IReadOnlyList<InstantEditorCard> Create(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.IconTheme =>
            [
                new IconThemeTokensCollectionEditor(
                    _database,
                    _isDark(),
                    _showInfo,
                    _confirmIconTokenDelete,
                    _showIconThemeSearch,
                    _reloadAndSelect).Create(node),
            ],
            ProjectTreeNodeKind.StatusBar =>
            [
                new StatusBarItemsCollectionEditor(
                    _database,
                    _isDark(),
                    _browsePath,
                    _showIconTokenPicker,
                    _onChanged).Create(node),
            ],
            ProjectTreeNodeKind.NavigationBar =>
            [
                new NavigationBarItemsCollectionEditor(
                    _database,
                    _isDark(),
                    _browsePath,
                    _onChanged).Create(node),
            ],
            _ => [],
        };
    }
}
