using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorPreviewAuthoringSurface(string Header, Control Content);

internal sealed record EditorPreparedPreviewAuthoringSurface(
    string Header,
    RuntimeInputsCollectionEditor Editor,
    RuntimeInputSurface Surface);

internal sealed class EditorCollectionCardFactory : IDisposable
{
    private readonly IModuleInstanceCollectionStore _moduleInstances;
    private readonly IIconThemeAssetStore _iconThemes;
    private readonly IComponentPreviewInputRepository _componentPreview;
    private readonly IDictionaryFieldContextRepository _dictionary;
    private readonly IActorPreviewRepository _actors;
    private readonly IRuntimeInputOwnerStore _runtimeInputOwners;
    private readonly IModuleInstanceTimelineStore _timeline;
    private readonly IRuntimeInputInstanceStore _runtimeInputInstances;
    private readonly IModuleInstanceAnimationStore _animation;
    private readonly IModuleInstanceThemeTokenQuery _moduleInstanceThemes;
    private readonly IReferenceUsageQuery _referenceUsage;
    private readonly EditorOperationCoordinator _operations;
    private readonly Func<bool> _isDark;
    private readonly Func<string, string, Task> _showInfo;
    private readonly EditorDomainDialogService _domainDialogs;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly Action _onChanged;
    private readonly EditorDictionaryFieldServices _dictionaryServices;
    private readonly IEditorShellMessageSink _messages;
    private readonly Action<string, string?> _triggerPreviewAction;
    private readonly Action<string> _restorePreviewAction;
    private readonly Func<string, bool> _canRestorePreviewAction;
    private readonly Func<string, bool> _isPreviewActionPlaying;
    private readonly Action<string, int, string?> _stepPreviewAction;
    private readonly Func<string, int, bool> _canStepPreviewAction;
    private readonly Action<string, int, string?> _setPreviewActionFrame;
    private readonly Func<string, int> _currentPreviewActionFrame;
    private readonly Func<string, int> _maximumPreviewActionFrame;
    private readonly Action<string, string> _setPreviewTestValue;
    private readonly Action<string, string, IReadOnlyDictionary<string, JsonNode?>>
        _setPreviewCollectionItemValues;
    private readonly Action<ProjectTreeNode, string, IReadOnlyList<JsonObject>> _setPreviewCollectionTestItems;
    private readonly Func<ProjectTreeNode, bool> _resetPreviewTestValues;
    private readonly PreviewPlaybackState _previewPlaybackState;
    private readonly Func<string, bool> _navigateToNode;
    private readonly Func<ReferenceUsageDetail, Task> _navigateToUsage;
    private readonly Action<EditorEmbeddedContext> _openEmbeddedContext;
    private readonly Func<int> _shotFrame;
    private readonly Action<int> _setShotFrame;
    private readonly Action _toggleProductionPlayback;
    private readonly EditorSessionUiState _sessionUiState;
    private CancellationTokenSource? _previewAuthoringPreparation;
    private bool _disposed;

    public EditorCollectionCardFactory(
        IModuleInstanceCollectionStore moduleInstances,
        IIconThemeAssetStore iconThemes,
        IComponentPreviewInputRepository componentPreview,
        IDictionaryFieldContextRepository dictionary,
        IActorPreviewRepository actors,
        IRuntimeInputOwnerStore runtimeInputOwners,
        IModuleInstanceTimelineStore timeline,
        IRuntimeInputInstanceStore runtimeInputInstances,
        IModuleInstanceAnimationStore animation,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes,
        IReferenceUsageQuery referenceUsage,
        EditorOperationCoordinator operations,
        Func<bool> isDark,
        Func<string, string, Task> showInfo,
        EditorDomainDialogService domainDialogs,
        Action<ProjectTreeNode> reloadAndSelect,
        Action onChanged,
        EditorDictionaryFieldServices dictionaryServices,
        IEditorShellMessageSink messages,
        Action<string, string?> triggerPreviewAction,
        Action<string> restorePreviewAction,
        Func<string, bool> canRestorePreviewAction,
        Func<string, bool> isPreviewActionPlaying,
        Action<string, int, string?> stepPreviewAction,
        Func<string, int, bool> canStepPreviewAction,
        Action<string, int, string?> setPreviewActionFrame,
        Func<string, int> currentPreviewActionFrame,
        Func<string, int> maximumPreviewActionFrame,
        Action<string, string> setPreviewTestValue,
        Action<string, string, IReadOnlyDictionary<string, JsonNode?>>
            setPreviewCollectionItemValues,
        Action<ProjectTreeNode, string, IReadOnlyList<JsonObject>> setPreviewCollectionTestItems,
        Func<ProjectTreeNode, bool> resetPreviewTestValues,
        PreviewPlaybackState previewPlaybackState,
        Func<string, bool> navigateToNode,
        Func<ReferenceUsageDetail, Task> navigateToUsage,
        Action<EditorEmbeddedContext> openEmbeddedContext,
        Func<int> shotFrame,
        Action<int> setShotFrame,
        Action toggleProductionPlayback,
        EditorSessionUiState sessionUiState)
    {
        _moduleInstances = moduleInstances;
        _iconThemes = iconThemes;
        _componentPreview = componentPreview;
        _dictionary = dictionary;
        _actors = actors;
        _runtimeInputOwners = runtimeInputOwners;
        _timeline = timeline;
        _runtimeInputInstances = runtimeInputInstances;
        _animation = animation;
        _moduleInstanceThemes = moduleInstanceThemes;
        _referenceUsage = referenceUsage;
        _operations = operations;
        _isDark = isDark;
        _showInfo = showInfo;
        _domainDialogs = domainDialogs;
        _reloadAndSelect = reloadAndSelect;
        _onChanged = onChanged;
        _dictionaryServices = dictionaryServices;
        _messages = messages;
        _triggerPreviewAction = triggerPreviewAction;
        _restorePreviewAction = restorePreviewAction;
        _canRestorePreviewAction = canRestorePreviewAction;
        _isPreviewActionPlaying = isPreviewActionPlaying;
        _stepPreviewAction = stepPreviewAction;
        _canStepPreviewAction = canStepPreviewAction;
        _setPreviewActionFrame = setPreviewActionFrame;
        _currentPreviewActionFrame = currentPreviewActionFrame;
        _maximumPreviewActionFrame = maximumPreviewActionFrame;
        _setPreviewTestValue = setPreviewTestValue;
        _setPreviewCollectionItemValues = setPreviewCollectionItemValues;
        _setPreviewCollectionTestItems = setPreviewCollectionTestItems;
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
        IReadOnlyList<InstantEditorCard> cards = node.Kind switch
        {
            ProjectTreeNodeKind.IconTheme =>
            [
                new IconThemeTokensCollectionEditor(
                    _iconThemes,
                    _operations,
                    _isDark(),
                    _showInfo,
                    _domainDialogs.ConfirmIconTokenDelete,
                    _domainDialogs.ShowIconThemeSearch,
                    _domainDialogs.ShowIconThemeSvgReplace,
                    _reloadAndSelect).Create(node),
            ],
            ProjectTreeNodeKind.Shot =>
                ShotCards(node),
            _ => [],
        };

        if (node.CanOpenEditor || node.Kind is ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant)
        {
            cards =
            [
                .. cards,
                new ReferenceUsageCollectionEditor(
                    _referenceUsage,
                    _operations,
                    _isDark(),
                    _navigateToUsage).Create(node),
            ];
        }

        return cards;
    }

    private IReadOnlyList<InstantEditorCard> ShotCards(
        ProjectTreeNode node)
    {
        var cards = new List<InstantEditorCard>
        {
            new ShotModuleInstancesCollectionEditor(
                _moduleInstances,
                _timeline,
                _moduleInstanceThemes,
                _operations,
                _onChanged,
                _reloadAndSelect,
                _domainDialogs.DefineModuleInstanceForShot,
                _domainDialogs.ConfirmModuleInstanceDelete,
                _shotFrame,
                _previewPlaybackState).Create(node),
        };
        return cards;
    }

    public async Task<EditorPreparedPreviewAuthoringSurface?>
        PreparePreviewAuthoringSurfaceAsync(
            ProjectTreeNode node,
            EditorWorkspace workspace,
            ComponentPreviewTransientState transientState)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var operation = BeginPreviewAuthoringPreparation();
        var cancellationToken = operation.Token;
        try
        {
            if (!SupportsPreviewAuthoringSurface(
                    node,
                    workspace))
            {
                return null;
            }

            var isProduction =
                workspace == EditorWorkspace.Production;
            var editor = CreateRuntimeInputsEditor(
                isProduction
                    ? CreateModuleInstanceAnimationEditor()
                    : null);
            var selectedThemeId =
                _dictionaryServices.CaptureSelectedThemeId();
            var surface = await _operations.ExecuteAsync(
                () => editor.PrepareSurface(
                    node,
                    transientState,
                    selectedThemeId,
                    cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new EditorPreparedPreviewAuthoringSurface(
                isProduction
                    ? "Screen Payload"
                    : "Test Values",
                editor,
                surface);
        }
        finally
        {
            CompletePreviewAuthoringPreparation(operation);
        }
    }

    public static bool SupportsPreviewAuthoringSurface(
        ProjectTreeNode node,
        EditorWorkspace workspace)
    {
        return workspace == EditorWorkspace.Production
            ? node.Kind == ProjectTreeNodeKind.ModuleInstance
            : node.Kind is ProjectTreeNodeKind.ComponentVariant
                or ProjectTreeNodeKind.ModuleVariant
                or ProjectTreeNodeKind.Module;
    }

    public static EditorPreviewAuthoringSurface?
        CreatePreparedPreviewAuthoringSurface(
            EditorPreparedPreviewAuthoringSurface prepared)
    {
        var content = prepared.Surface.Owner.IsInstance
            ? prepared.Editor
                .CreateProductionScreenPayloadSurface(
                    prepared.Surface)
            : prepared.Editor
                .CreateDesignTestValuesSurface(
                    prepared.Surface);
        return content is null
            ? null
            : new EditorPreviewAuthoringSurface(
                prepared.Header,
                content);
    }

    public void CancelPreviewAuthoringPreparation()
    {
        var operation = _previewAuthoringPreparation;
        _previewAuthoringPreparation = null;
        operation?.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var operation = _previewAuthoringPreparation;
        _previewAuthoringPreparation = null;
        operation?.Cancel();
    }

    private CancellationTokenSource
        BeginPreviewAuthoringPreparation()
    {
        var previous = _previewAuthoringPreparation;
        previous?.Cancel();
        var operation = new CancellationTokenSource();
        _previewAuthoringPreparation = operation;
        return operation;
    }

    private void CompletePreviewAuthoringPreparation(
        CancellationTokenSource operation)
    {
        if (ReferenceEquals(
                _previewAuthoringPreparation,
                operation))
        {
            _previewAuthoringPreparation = null;
        }
        operation.Dispose();
    }

    private ModuleInstanceAnimationEditor CreateModuleInstanceAnimationEditor()
    {
        return new ModuleInstanceAnimationEditor(
            _animation,
            _timeline,
            _moduleInstanceThemes,
            _dictionary,
            _actors,
            _operations,
            _dictionaryServices,
            _messages,
            _onChanged,
            _sessionUiState,
            _shotFrame,
            _setShotFrame,
            _previewPlaybackState,
            _toggleProductionPlayback);
    }

    private RuntimeInputsCollectionEditor CreateRuntimeInputsEditor(
        ModuleInstanceAnimationEditor? animationEditor)
    {
        return new RuntimeInputsCollectionEditor(
            _componentPreview,
            _dictionary,
            _actors,
            _runtimeInputOwners,
            _timeline,
            _runtimeInputInstances,
            _animation,
            _moduleInstanceThemes,
            _operations,
            _dictionaryServices,
            _onChanged,
            _triggerPreviewAction,
            _restorePreviewAction,
            _canRestorePreviewAction,
            _isPreviewActionPlaying,
            _stepPreviewAction,
            _canStepPreviewAction,
            _setPreviewActionFrame,
            _currentPreviewActionFrame,
            _maximumPreviewActionFrame,
            _setPreviewTestValue,
            _setPreviewCollectionItemValues,
            _setPreviewCollectionTestItems,
            _resetPreviewTestValues,
            _domainDialogs.ConfirmTestValueDefaults,
            _domainDialogs.ConfirmRuntimeCollectionItemDelete,
            _domainDialogs.ConfirmAnimationDisable,
            _previewPlaybackState,
            _sessionUiState,
            _navigateToNode,
            _openEmbeddedContext,
            animationEditor,
            _reloadAndSelect);
    }
}
