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
    private readonly Action _onChanged;
    private readonly EditorDictionaryFieldServices _dictionaryServices;
    private readonly Action<string, string?> _triggerPreviewAction;
    private readonly Action<string> _restorePreviewAction;
    private readonly Func<string, bool> _canRestorePreviewAction;
    private readonly Action<string, string> _setPreviewTestValue;
    private readonly Action<string, string, ComponentInputDefinition, string> _setPreviewCollectionTestValue;
    private readonly Action<string, IReadOnlyList<JsonObject>> _setPreviewCollectionTestItems;
    private readonly Func<ProjectTreeNode, JsonObject, JsonObject> _applyPreviewTransientTestValues;
    private readonly Func<ProjectTreeNode, bool> _resetPreviewTestValues;
    private readonly PreviewPlaybackState _previewPlaybackState;
    private readonly Func<string, bool> _navigateToNode;
    private readonly Func<SpikeDatabase.ReferenceUsageDetail, Task> _navigateToUsage;
    private readonly Action<EditorEmbeddedContext> _openEmbeddedContext;
    private readonly Func<int> _shotFrame;
    private readonly Action<int> _setShotFrame;
    private readonly Action _toggleProductionPlayback;
    private readonly EditorSessionUiState _sessionUiState;

    public EditorCollectionCardFactory(
        SpikeDatabase database,
        Func<bool> isDark,
        Func<string, string, Task> showInfo,
        EditorDomainDialogService domainDialogs,
        Action<ProjectTreeNode> reloadAndSelect,
        Action onChanged,
        EditorDictionaryFieldServices dictionaryServices,
        Action<string, string?> triggerPreviewAction,
        Action<string> restorePreviewAction,
        Func<string, bool> canRestorePreviewAction,
        Action<string, string> setPreviewTestValue,
        Action<string, string, ComponentInputDefinition, string> setPreviewCollectionTestValue,
        Action<string, IReadOnlyList<JsonObject>> setPreviewCollectionTestItems,
        Func<ProjectTreeNode, JsonObject, JsonObject> applyPreviewTransientTestValues,
        Func<ProjectTreeNode, bool> resetPreviewTestValues,
        PreviewPlaybackState previewPlaybackState,
        Func<string, bool> navigateToNode,
        Func<SpikeDatabase.ReferenceUsageDetail, Task> navigateToUsage,
        Action<EditorEmbeddedContext> openEmbeddedContext,
        Func<int> shotFrame,
        Action<int> setShotFrame,
        Action toggleProductionPlayback,
        EditorSessionUiState sessionUiState)
    {
        _database = database;
        _isDark = isDark;
        _showInfo = showInfo;
        _domainDialogs = domainDialogs;
        _reloadAndSelect = reloadAndSelect;
        _onChanged = onChanged;
        _dictionaryServices = dictionaryServices;
        _triggerPreviewAction = triggerPreviewAction;
        _restorePreviewAction = restorePreviewAction;
        _canRestorePreviewAction = canRestorePreviewAction;
        _setPreviewTestValue = setPreviewTestValue;
        _setPreviewCollectionTestValue = setPreviewCollectionTestValue;
        _setPreviewCollectionTestItems = setPreviewCollectionTestItems;
        _applyPreviewTransientTestValues = applyPreviewTransientTestValues;
        _resetPreviewTestValues = resetPreviewTestValues;
        _previewPlaybackState = previewPlaybackState;
        _navigateToNode = navigateToNode;
        _navigateToUsage = navigateToUsage;
        _openEmbeddedContext = openEmbeddedContext;
        _shotFrame = shotFrame;
        _setShotFrame = setShotFrame;
        _toggleProductionPlayback = toggleProductionPlayback;
        _sessionUiState = sessionUiState;
    }

    public IReadOnlyList<InstantEditorCard> Create(ProjectTreeNode node)
    {
        var animationEditor = node.Kind == ProjectTreeNodeKind.ModuleInstance
            ? new ModuleInstanceAnimationEditor(
                _database,
                _dictionaryServices,
                _onChanged,
                _sessionUiState,
                _shotFrame,
                _setShotFrame,
                _previewPlaybackState,
                _toggleProductionPlayback)
            : null;
        IReadOnlyList<InstantEditorCard> cards = node.Kind switch
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
            ProjectTreeNodeKind.Module or ProjectTreeNodeKind.ComponentPreset or ProjectTreeNodeKind.ModuleInstance or ProjectTreeNodeKind.ModuleVariant =>
            [
                CreateRuntimeInputsCard(node, animationEditor),
            ],
            ProjectTreeNodeKind.Shot =>
                [new ShotModuleInstancesCollectionEditor(
                    _database,
                    _onChanged,
                    _reloadAndSelect,
                    _domainDialogs.DefineModuleInstanceForShot,
                    _domainDialogs.ConfirmModuleInstanceDelete).Create(node)],
            _ => [],
        };

        if (node.CanOpenEditor || node.Kind is ProjectTreeNodeKind.ComponentPreset or ProjectTreeNodeKind.ModuleVariant)
        {
            cards = [.. cards, new ReferenceUsageCollectionEditor(_database, _isDark(), _navigateToUsage).Create(node)];
        }

        return cards;
    }

    private InstantEditorCard CreateRuntimeInputsCard(
        ProjectTreeNode node,
        ModuleInstanceAnimationEditor? animationEditor)
    {
        return new RuntimeInputsCollectionEditor(
            _database,
            _dictionaryServices,
            _onChanged,
            _triggerPreviewAction,
            _restorePreviewAction,
            _canRestorePreviewAction,
            _setPreviewTestValue,
            _setPreviewCollectionTestValue,
            _setPreviewCollectionTestItems,
            _applyPreviewTransientTestValues,
            _resetPreviewTestValues,
            _domainDialogs.ConfirmTestValueDefaults,
            _domainDialogs.ConfirmRuntimeCollectionItemDelete,
            _domainDialogs.ConfirmAnimationDisable,
            _previewPlaybackState,
            _sessionUiState,
            _navigateToNode,
            _openEmbeddedContext,
            animationEditor,
            _reloadAndSelect).Create(node);
    }
}
