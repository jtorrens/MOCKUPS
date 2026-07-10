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
    private readonly EditorDomainDialogService _domainDialogs;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly Func<string, ValueKind, Task<string?>> _browsePath;
    private readonly Action _onChanged;

    public EditorCollectionCardFactory(
        SpikeDatabase database,
        Func<bool> isDark,
        Func<string, string, Task> showInfo,
        EditorDomainDialogService domainDialogs,
        Action<ProjectTreeNode> reloadAndSelect,
        Func<string, ValueKind, Task<string?>> browsePath,
        Action onChanged)
    {
        _database = database;
        _isDark = isDark;
        _showInfo = showInfo;
        _domainDialogs = domainDialogs;
        _reloadAndSelect = reloadAndSelect;
        _browsePath = browsePath;
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
                    _domainDialogs.ConfirmIconTokenDelete,
                    _domainDialogs.ShowIconThemeSearch,
                    _domainDialogs.ShowIconThemeSvgReplace,
                    _reloadAndSelect).Create(node),
            ],
            ProjectTreeNodeKind.ComponentClass =>
                CreateComponentClassCollectionCards(node),
            ProjectTreeNodeKind.ModuleInstance =>
                [new ConversationMessagesCollectionEditor(_database, _onChanged).Create(node)],
            _ => [],
        };
    }

    private IReadOnlyList<InstantEditorCard> CreateComponentClassCollectionCards(ProjectTreeNode node)
    {
        var settings = _database.GetComponentClassSettings(node.Id);
        return settings.ComponentType switch
        {
            "status_bar" =>
            [
                new StatusBarItemsCollectionEditor(
                    _database,
                    _isDark(),
                    _browsePath,
                    _domainDialogs.ShowIconTokenPicker,
                    _onChanged).Create(node),
            ],
            "navigation_bar" =>
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
