using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
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
    private readonly EditorDictionaryFieldServices _dictionaryServices;
    private readonly Action<string> _triggerPreviewAction;
    private readonly Action<string, string> _setPreviewTestValue;
    private readonly Action<string, string, ComponentInputDefinition, string> _setPreviewCollectionTestValue;
    private readonly Func<JsonObject, JsonObject> _applyPreviewTransientTestValues;
    private readonly Func<bool> _resetPreviewTestValues;
    private readonly PreviewPlaybackState _previewPlaybackState;
    private readonly Func<string, bool> _navigateToNode;
    private readonly Func<string, int> _screenFrame;
    private readonly Action<string, int> _setScreenFrame;
    private readonly Action _toggleProductionPlayback;
    private readonly EditorSessionUiState _sessionUiState = new();

    public EditorCollectionCardFactory(
        SpikeDatabase database,
        Func<bool> isDark,
        Func<string, string, Task> showInfo,
        EditorDomainDialogService domainDialogs,
        Action<ProjectTreeNode> reloadAndSelect,
        Func<string, ValueKind, Task<string?>> browsePath,
        Action onChanged,
        EditorDictionaryFieldServices dictionaryServices,
        Action<string> triggerPreviewAction,
        Action<string, string> setPreviewTestValue,
        Action<string, string, ComponentInputDefinition, string> setPreviewCollectionTestValue,
        Func<JsonObject, JsonObject> applyPreviewTransientTestValues,
        Func<bool> resetPreviewTestValues,
        PreviewPlaybackState previewPlaybackState,
        Func<string, bool> navigateToNode,
        Func<string, int> screenFrame,
        Action<string, int> setScreenFrame,
        Action toggleProductionPlayback)
    {
        _database = database;
        _isDark = isDark;
        _showInfo = showInfo;
        _domainDialogs = domainDialogs;
        _reloadAndSelect = reloadAndSelect;
        _browsePath = browsePath;
        _onChanged = onChanged;
        _dictionaryServices = dictionaryServices;
        _triggerPreviewAction = triggerPreviewAction;
        _setPreviewTestValue = setPreviewTestValue;
        _setPreviewCollectionTestValue = setPreviewCollectionTestValue;
        _applyPreviewTransientTestValues = applyPreviewTransientTestValues;
        _resetPreviewTestValues = resetPreviewTestValues;
        _previewPlaybackState = previewPlaybackState;
        _navigateToNode = navigateToNode;
        _screenFrame = screenFrame;
        _setScreenFrame = setScreenFrame;
        _toggleProductionPlayback = toggleProductionPlayback;
    }

    public IReadOnlyList<InstantEditorCard> Create(ProjectTreeNode node)
    {
        var animationEditor = node.Kind == ProjectTreeNodeKind.ModuleInstance
            ? new ModuleInstanceAnimationEditor(
                _database,
                _dictionaryServices,
                _onChanged,
                _reloadAndSelect,
                _sessionUiState,
                _screenFrame,
                _setScreenFrame,
                _previewPlaybackState,
                _toggleProductionPlayback)
            : null;
        var cards = node.Kind switch
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
            ProjectTreeNodeKind.Module or ProjectTreeNodeKind.ComponentPreset or ProjectTreeNodeKind.ModuleInstance =>
            [
                new RuntimeInputsCollectionEditor(_database, _dictionaryServices, _onChanged, _triggerPreviewAction, _setPreviewTestValue, _setPreviewCollectionTestValue, _applyPreviewTransientTestValues, _resetPreviewTestValues, _domainDialogs.ConfirmTestValueDefaults, _domainDialogs.ConfirmRuntimeCollectionItemDelete, _domainDialogs.ConfirmAnimationDisable, _previewPlaybackState, _sessionUiState, animationEditor, _reloadAndSelect).Create(node),
            ],
            ProjectTreeNodeKind.Shot =>
                [new ShotModuleInstancesCollectionEditor(
                    _database,
                    _onChanged,
                    _reloadAndSelect,
                    _domainDialogs.SelectModuleForShot).Create(node)],
            _ => [],
        };

        if (node.CanOpenEditor || node.Kind == ProjectTreeNodeKind.ComponentPreset)
        {
            cards = [.. cards, new ReferenceUsageCollectionEditor(_database, _navigateToNode).Create(node)];
        }

        return cards;
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
                    _dictionaryServices,
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
