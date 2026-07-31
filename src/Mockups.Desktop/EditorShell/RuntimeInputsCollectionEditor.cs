using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record RuntimeInputOwner(
    ProjectTreeNode Node,
    string ConfigJson,
    string DesignPreviewJson,
    Func<string, Task> Save,
    bool IsInstance);

internal sealed record RuntimeInputSurface(
    RuntimeInputOwner Owner,
    JsonObject Preview,
    IReadOnlyList<ComponentInputDefinition> Inputs,
    IReadOnlyList<RuntimeInputCollectionDefinition> Collections,
    IReadOnlyList<ComponentPreviewActionDefinition> Actions,
    EditorDictionaryContextSnapshot? DictionaryContext = null,
    ModuleInstanceAnimationSnapshot? AnimationSnapshot = null,
    RuntimeInputTimelineMutation? TimelineMutation = null);

internal sealed record RuntimeInputTimelineMutation(
    Func<string, string, IReadOnlyDictionary<string, JsonNode?>, Task> UpdateCollectionValuesAsync,
    Func<string, Task<string>> SaveAnimationJsonAsync);

internal sealed class RuntimeInputsCollectionEditor
{
    private const double RuntimeNavigationWidth = 160;
    private readonly ComponentPreviewInputDataSource _previewInputData;
    private readonly RuntimeInputOwnerDocumentStore _ownerDocuments;
    private readonly RuntimeInputInstanceDocumentStore _instanceDocuments;
    private readonly RuntimeInputOptionsDataSource _runtimeInputOptions;
    private readonly EditorDictionaryFieldServices _dictionaryServices;
    private readonly Action _onChanged;
    private readonly Action<string, string?> _triggerAction;
    private readonly Action<string> _restoreAction;
    private readonly Func<string, bool> _canRestoreAction;
    private readonly Action<string, int, string?> _stepAction;
    private readonly Func<string, int, bool> _canStepAction;
    private readonly Action<string, int, string?> _setActionFrame;
    private readonly Func<string, int> _currentActionFrame;
    private readonly Func<string, int> _maximumActionFrame;
    private readonly Action<string, string> _setPreviewTestValue;
    private readonly Action<string, string, IReadOnlyDictionary<string, JsonNode?>>
        _setPreviewCollectionItemValues;
    private readonly Action<ProjectTreeNode, string, IReadOnlyList<JsonObject>> _setPreviewCollectionTestItems;
    private readonly Func<ProjectTreeNode, bool> _resetTestValues;
    private readonly Func<string, IReadOnlyList<string>, Task<bool>> _confirmSaveDefaults;
    private readonly Func<string, Task<bool>> _confirmCollectionItemDelete;
    private readonly Func<string, Task<bool>> _confirmAnimationDisable;
    private readonly PreviewPlaybackState _playbackState;
    private readonly Action<ProjectTreeNode>? _reloadAndSelect;
    private readonly EditorSessionUiState _sessionUiState;
    private readonly ModuleInstanceAnimationEditor? _animationEditor;
    private readonly Func<string, bool> _navigateToNode;
    private readonly Action<EditorEmbeddedContext> _openEmbeddedContext;
    private Action _testValuesChanged = () => { };
    private EditorDictionaryContextSnapshot?
        _preparedDictionaryContext;
    private string? _preparedAnimationJson;
    private IRuntimeInputOptionsDataSource ActiveInputOptions =>
        _preparedDictionaryContext is null
            ? _runtimeInputOptions
            : new PreparedRuntimeInputOptionsDataSource(
                _preparedDictionaryContext);

    public RuntimeInputsCollectionEditor(
        IComponentPreviewInputRepository componentPreview,
        IDictionaryFieldContextRepository dictionary,
        IActorPreviewRepository actors,
        IRuntimeInputOwnerStore ownerStore,
        IModuleInstanceTimelineStore timeline,
        IRuntimeInputInstanceStore instanceStore,
        IModuleInstanceAnimationStore animationStore,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes,
        EditorOperationCoordinator operations,
        EditorDictionaryFieldServices dictionaryServices,
        Action onChanged,
        Action<string, string?> triggerAction,
        Action<string> restoreAction,
        Func<string, bool> canRestoreAction,
        Action<string, int, string?> stepAction,
        Func<string, int, bool> canStepAction,
        Action<string, int, string?> setActionFrame,
        Func<string, int> currentActionFrame,
        Func<string, int> maximumActionFrame,
        Action<string, string> setPreviewTestValue,
        Action<string, string, IReadOnlyDictionary<string, JsonNode?>>
            setPreviewCollectionItemValues,
        Action<ProjectTreeNode, string, IReadOnlyList<JsonObject>> setPreviewCollectionTestItems,
        Func<ProjectTreeNode, bool> resetTestValues,
        Func<string, IReadOnlyList<string>, Task<bool>> confirmSaveDefaults,
        Func<string, Task<bool>> confirmCollectionItemDelete,
        Func<string, Task<bool>> confirmAnimationDisable,
        PreviewPlaybackState playbackState,
        EditorSessionUiState sessionUiState,
        Func<string, bool> navigateToNode,
        Action<EditorEmbeddedContext> openEmbeddedContext,
        ModuleInstanceAnimationEditor? animationEditor = null,
        Action<ProjectTreeNode>? reloadAndSelect = null)
    {
        _previewInputData =
            new ComponentPreviewInputDataSource(
                componentPreview,
                actors);
        _ownerDocuments =
            new RuntimeInputOwnerDocumentStore(
                ownerStore,
                timeline,
                operations);
        _instanceDocuments =
            new RuntimeInputInstanceDocumentStore(
                instanceStore,
                animationStore,
                timeline,
                moduleInstanceThemes,
                operations);
        _runtimeInputOptions =
            new RuntimeInputOptionsDataSource(dictionary, actors);
        _dictionaryServices = dictionaryServices;
        _onChanged = onChanged;
        _triggerAction = triggerAction;
        _restoreAction = restoreAction;
        _canRestoreAction = canRestoreAction;
        _stepAction = stepAction;
        _canStepAction = canStepAction;
        _setActionFrame = setActionFrame;
        _currentActionFrame = currentActionFrame;
        _maximumActionFrame = maximumActionFrame;
        _setPreviewTestValue = setPreviewTestValue;
        _setPreviewCollectionItemValues = setPreviewCollectionItemValues;
        _setPreviewCollectionTestItems = setPreviewCollectionTestItems;
        _resetTestValues = resetTestValues;
        _confirmSaveDefaults = confirmSaveDefaults;
        _confirmCollectionItemDelete = confirmCollectionItemDelete;
        _confirmAnimationDisable = confirmAnimationDisable;
        _playbackState = playbackState;
        _sessionUiState = sessionUiState;
        _navigateToNode = navigateToNode;
        _openEmbeddedContext = openEmbeddedContext;
        _animationEditor = animationEditor;
        _reloadAndSelect = reloadAndSelect;
    }

    public Control CreateProductionScreenPayloadSurface(
        RuntimeInputSurface surface)
    {
        UsePreparedContext(surface);
        if (!surface.Owner.IsInstance)
        {
            throw new InvalidOperationException(
                "Only a Production Screen instance can expose a persisted Screen Payload surface.");
        }

        return new Border
        {
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = CreateTestValuesTab(
                surface.Owner,
                surface.Preview,
                surface.Inputs,
                surface.Collections,
                surface.Actions),
        };
    }

    public AnimationTargetEditorContent CreateScreenTimelineAnimationContent(
        RuntimeInputSurface surface,
        string targetId)
    {
        UsePreparedContext(surface);
        if (!surface.Owner.IsInstance || _animationEditor is null)
        {
            throw new InvalidOperationException(
                "Only a prepared Production Screen can expose Timeline animation content.");
        }

        return _animationEditor.CreateScreenTimelineTargetContent(
            surface.Owner.Node,
            targetId);
    }

    public Control? CreateDesignTestValuesSurface(
        RuntimeInputSurface surface)
    {
        UsePreparedContext(surface);
        if (surface.Owner.IsInstance)
        {
            return null;
        }
        if (surface.Inputs.Count == 0
            && surface.Collections.Count == 0
            && surface.Actions.Count == 0)
        {
            return null;
        }

        return CreateTestValuesTab(
            surface.Owner,
            surface.Preview,
            surface.Inputs,
            surface.Collections,
            surface.Actions);
    }

    public RuntimeInputSurface PrepareSurface(
        ProjectTreeNode node,
        ComponentPreviewTransientState transientState,
        string? selectedThemeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = ResolveOwner(node);
        cancellationToken.ThrowIfCancellationRequested();
        var persistedPreview =
            DesignPreviewTestValues.Parse(
                owner.DesignPreviewJson);
        var config = DesignPreviewTestValues.Parse(
            owner.ConfigJson);
        var preview = ComponentPreviewTransientValues.Apply(
            persistedPreview,
            config,
            transientState,
            _previewInputData.ComponentVariantConfig);
        cancellationToken.ThrowIfCancellationRequested();
        var inputs =
            RuntimeInputDefinitionReader.ReadInputs(
                preview,
                config);
        var collections =
            RuntimeInputDefinitionReader.ReadCollections(
                preview,
                config);
        var actions = ComponentPreviewActions.ReadWithEmbedded(
            preview,
            _previewInputData.ComponentVariantRuntimeContract);
        cancellationToken.ThrowIfCancellationRequested();
        var surface = new RuntimeInputSurface(
            owner,
            preview,
            inputs,
            collections,
            actions);
        var dictionaryContext =
            _dictionaryServices.PrepareRuntimeContext(
                node,
                selectedThemeId,
                surface,
                cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var animationSnapshot = owner.IsInstance
            ? _animationEditor?.PrepareSnapshot(node)
                ?? throw new InvalidOperationException(
                    $"Production Screen '{node.Id}' requires its animation snapshot owner.")
            : null;
        var timelineMutation = owner.IsInstance
            ? new RuntimeInputTimelineMutation(
                async (collectionJsonKey, itemId, values) =>
                {
                    await _instanceDocuments.UpdateCollectionValuesAsync(
                        owner.Node.Id,
                        collectionJsonKey,
                        itemId,
                        values);
                    _onChanged();
                },
                async (animationJson) =>
                {
                    var saved = await _instanceDocuments.SaveAnimationJsonAsync(
                        owner.Node.Id,
                        animationJson);
                    _onChanged();
                    return saved;
                })
            : null;
        cancellationToken.ThrowIfCancellationRequested();
        return surface with
        {
            DictionaryContext = dictionaryContext,
            AnimationSnapshot = animationSnapshot,
            TimelineMutation = timelineMutation,
        };
    }

    private void UsePreparedContext(
        RuntimeInputSurface surface)
    {
        _preparedDictionaryContext =
            surface.DictionaryContext;
        _preparedAnimationJson =
            surface.AnimationSnapshot?.Source.AnimationJson;
        _animationEditor?.UsePreparedContext(
            surface.DictionaryContext,
            surface.AnimationSnapshot);
    }

    private Control CreateTestValuesTab(
        RuntimeInputOwner owner,
        JsonObject preview,
        IReadOnlyList<ComponentInputDefinition> inputs,
        IReadOnlyList<RuntimeInputCollectionDefinition> collections,
        IReadOnlyList<ComponentPreviewActionDefinition> actions)
    {
        var fixedPanel = new StackPanel { Spacing = 8 };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(new TextBlock
        {
            Text = owner.IsInstance ? "Screen Payload" : "Temporary Preview data",
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        if (!owner.IsInstance)
        {
            var config =
                DesignPreviewTestValues.Parse(
                    owner.ConfigJson);
            JsonObject DefaultPreview() =>
                PrepareDefaultPreview(
                    owner.DesignPreviewJson,
                    config);
            var reset = new Button
            {
                MinWidth = 150,
                Content = "Reset test values",
            };
            ToolTip.SetTip(reset, "Discard temporary changes for this Preview.");
            reset.Click += (_, args) =>
            {
                args.Handled = true;
                if (!_resetTestValues(owner.Node)) return;
                _onChanged();
                _reloadAndSelect?.Invoke(owner.Node);
            };
            buttons.Children.Add(reset);
            var saveDefaults = new Button
            {
                MinWidth = 170,
                Content = "Save as defaults…",
            };
            void RefreshSaveState()
            {
                var current = preview.DeepClone().AsObject();
                var baseline = DefaultPreview();
                var currentInputs =
                    RuntimeInputDefinitionReader.ReadInputs(
                        current,
                        config);
                var currentCollections =
                    RuntimeInputDefinitionReader.ReadCollections(
                        current,
                        config);
                var currentDifferences = DesignPreviewTestValues.Differences(current, baseline, currentInputs, currentCollections);
                saveDefaults.IsEnabled = currentDifferences.Count > 0;
                ToolTip.SetTip(saveDefaults, currentDifferences.Count == 0
                    ? "There are no differences from the default values."
                    : $"Save {currentDifferences.Count} field(s) as default values.");
            }
            _testValuesChanged = RefreshSaveState;
            saveDefaults.Click += async (_, args) =>
            {
                args.Handled = true;
                var current = preview.DeepClone().AsObject();
                var differences =
                    DesignPreviewTestValues.Differences(
                        current,
                        DefaultPreview(),
                        inputs,
                        collections);
                if (differences.Count == 0 || !await _confirmSaveDefaults(owner.Node.Name, differences.Select((difference) => difference.Label).ToList())) return;
                DesignPreviewTestValues.PromoteToDefaults(current, inputs, collections);
                await owner.Save(current.ToJsonString());
                _resetTestValues(owner.Node);
                _onChanged();
            };
            buttons.Children.Add(saveDefaults);
            RefreshSaveState();
        }
        Grid.SetColumn(buttons, 1);
        header.Children.Add(buttons);
        fixedPanel.Children.Add(header);
        if (owner.IsInstance)
        {
            fixedPanel.Children.Add(new TextBlock
            {
                Text = "Saved with this Screen instance.",
                FontSize = 11,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        else
        {
            fixedPanel.Children.Add(new TextBlock
            {
                Text = "These values affect only the current Preview until you choose to save them as defaults.",
                FontSize = 11,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        var rootActions = owner.IsInstance
            ? []
            : actions.Where((candidate) => !candidate.IsCollectionItemAction).ToList();
        if (rootActions.Count > 0)
        {
            var actionPanel = CreateActionPanel();
            foreach (var action in rootActions)
            {
                AddActionControl(actionPanel, CreateActionControl(action, inputs, preview));
            }
            fixedPanel.Children.Add(actionPanel);
        }
        var valuesPanel = new StackPanel { Spacing = 8 };
        if (inputs.Count == 0 && collections.Count == 0)
        {
            valuesPanel.Children.Add(new TextBlock { Text = "No test values are required.", Opacity = 0.68 });
        }
        else
        {
            var visibleInputs = inputs.Where((input) => IsVisibleRuntimeValue(owner, input)).ToList();
            var groups = ComponentInputGrouping.EmbeddedGroups(visibleInputs);
            var sections = new List<EditorInternalNavigationSection>();
            var promotedCollectionFooters = new List<Control>();
            var topLevelGroupIds = ComponentInputGrouping.TopLevelGroupIds(groups).ToList();
            var ownInputs = ComponentInputGrouping.OwnInputs(visibleInputs).ToList();
            if (ownInputs.Count > 0)
            {
                var generalSubcards = new List<EditorInternalNavigationSection>();
                if (owner.IsInstance
                    && _animationEditor is not null
                    && inputs.Any((input) => input.Animation is not null))
                {
                    var animation = _animationEditor.CreateTargetContent(owner.Node, "");
                    generalSubcards.Add(new EditorInternalNavigationSection(
                        "animation",
                        "Animation",
                        EditorUiText.Count(animation.ActiveTrackCount, "animated property"),
                        EditorIcons.Animation,
                        animation.Content));
                }
                sections.Add(new EditorInternalNavigationSection(
                    "general",
                    "General",
                    "Runtime inputs",
                    EditorIcons.General,
                    CreateSeparatedInputContent(owner, preview, ownInputs),
                    Subcards: generalSubcards,
                    SubcardLayout: EditorSubcardLayout.FlatStack,
                    ShowLabel: false));
            }
            foreach (var groupId in topLevelGroupIds)
            {
                sections.Add(CreateTestValueGroupSubcard(owner, preview, groupId, groups));
            }
            foreach (var collection in collections.Where((collection) => string.IsNullOrWhiteSpace(collection.UiParentCollectionJsonKey)))
            {
                var items = DisplayItems(preview, collection);
                var childCollections = collections
                    .Where((candidate) => candidate.UiParentCollectionJsonKey.Equals(collection.JsonKey, StringComparison.Ordinal))
                    .ToList();
                if (collection.UiPresentation.Equals("itemSections", StringComparison.Ordinal))
                {
                    sections.AddRange(CreateTestValueCollectionItemSections(
                        owner,
                        preview,
                        collection,
                        actions,
                        items,
                        childCollections,
                        out var collectionFooter));
                    if (collectionFooter is not null)
                    {
                        promotedCollectionFooters.Add(collectionFooter);
                    }
                    continue;
                }
                var collectionContent = childCollections.Count == 0
                    ? CreateTestValueCollectionContent(owner, preview, collection, actions, items)
                    : CreateTestValueCollectionContent(owner, preview, collection, actions, items, childCollections);
                sections.Add(new EditorInternalNavigationSection(
                    collection.Id,
                    collection.Label,
                    $"{items.Count} active {EditorUiText.Noun(items.Count, "instance")}",
                    EditorIcons.Component,
                    collectionContent));
            }
            valuesPanel.Children.Add(CreateSessionSubcardLayout(
                $"{owner.Node.Id}:test-values",
                sections,
                EditorSubcardLayout.VerticalCards,
                RuntimeNavigationWidth));
            foreach (var collectionFooter in promotedCollectionFooters)
            {
                valuesPanel.Children.Add(collectionFooter);
            }
        }

        if (owner.IsInstance)
        {
            fixedPanel.Name = "PreviewScreenPayloadFixedHeader";
        }
        else
        {
            fixedPanel.Name = "PreviewTestValuesFixedActions";
        }
        fixedPanel.Margin = new Thickness(12, 8, 12, 0);
        fixedPanel.Children.Add(EditorGroupBlock.CreateSeparator());
        var valuesScroll = new ScrollViewer
        {
            Name = owner.IsInstance
                ? "PreviewScreenPayloadEditorScroll"
                : "PreviewTestValuesEditorScroll",
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Padding = new Thickness(12, 8, 12, 12),
            Content = valuesPanel,
        };
        Grid.SetRow(valuesScroll, 1);
        var surface = new Grid
        {
            Name = owner.IsInstance
                ? "PreviewScreenPayloadSplitLayout"
                : "PreviewTestValuesSplitLayout",
            RowDefinitions = new RowDefinitions("Auto,*"),
            MinHeight = 0,
            Children =
            {
                fixedPanel,
                valuesScroll,
            },
        };

        void UpdatePlaybackState()
        {
            surface.IsEnabled = !_playbackState.IsBusy;
        }
        PreviewPlaybackStateBinding.Attach(surface, _playbackState, UpdatePlaybackState);
        return surface;
    }

    private Control CreateSeparatedInputContent(
        RuntimeInputOwner owner,
        JsonObject preview,
        IReadOnlyList<ComponentInputDefinition> inputs)
    {
        var content = new StackPanel { Spacing = 8 };
        var sectionLabel = "";
        foreach (var input in inputs)
        {
            if (!string.IsNullOrWhiteSpace(input.UiSectionLabel)
                && !string.Equals(sectionLabel, input.UiSectionLabel, StringComparison.Ordinal))
            {
                content.Children.Add(EditorGroupBlock.CreateInlineSection(input.UiSectionLabel));
                sectionLabel = input.UiSectionLabel;
            }
            content.Children.Add(CreateTestValueControl(owner, preview, input, inputs));
        }
        return content;
    }

    private Control CreateTestValueControl(
        RuntimeInputOwner owner,
        JsonObject preview,
        ComponentInputDefinition input,
        IReadOnlyList<ComponentInputDefinition> ownerInputs)
    {
        var value = DesignPreviewTestValues.Value(preview, input);
        var definition = RuntimeInputFieldDefinitionFactory.Create(
            ActiveInputOptions,
            owner.Node,
            input);
        if (!string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey))
        {
            definition = definition with
            {
                Options = RuntimeInputDynamicOptions.Resolve(ActiveInputOptions, input, preview),
            };
        }
        var control = new DictionaryFieldControl(
            new FieldValue(definition, value),
            DictionaryServices(
                owner,
                (_) => "",
                openComponentVariantReference: (reference) =>
                {
                    _navigateToNode(reference);
                    return Task.CompletedTask;
                },
                openRuntimeComponentOverrides: _openEmbeddedContext));
        control.IsEnabled = RuntimeInputIsEnabled(preview, DesignPreviewTestValues.Parse(owner.ConfigJson), input);
        control.ValueChanged += (_, next) =>
        {
            if (!owner.IsInstance)
            {
                DesignPreviewTestValues.SetValue(
                    preview,
                    input,
                    next);
            }
            _setPreviewTestValue(input.JsonKey, next);
            _testValuesChanged();
        };
        control.ValueCommitted += async (_, next) =>
        {
            if (owner.IsInstance)
            {
                await _instanceDocuments.UpdateRuntimeValueAsync(
                    owner.Node.Id,
                    input.JsonKey,
                    DesignPreviewTestValues.ValueNode(input, next));
                _onChanged();
            }
            else
            {
                DesignPreviewTestValues.SetValue(preview, input, next);
            }
            if (input.RefreshOnCommit)
            {
                _reloadAndSelect?.Invoke(owner.Node);
            }
        };
        return DecorateAnimationToggle(owner, input, "", control, ownerInputs);
    }

    private static bool RuntimeInputIsEnabled(
        JsonObject preview,
        JsonObject config,
        ComponentInputDefinition input)
    {
        if (string.IsNullOrWhiteSpace(input.EnabledWhenPath)
            || string.IsNullOrWhiteSpace(input.EnabledWhenValue))
        {
            return true;
        }

        var path = input.EnabledWhenPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = JsonPath.Get(preview, path) ?? JsonPath.Get(config, path);
        return current is JsonValue value
            && value.TryGetValue<string>(out var text)
            && text.Equals(input.EnabledWhenValue, StringComparison.Ordinal);
    }

    private IReadOnlyList<EditorInternalNavigationSection> CreateTestValueCollectionItemSections(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        IReadOnlyList<JsonObject> items,
        IReadOnlyList<RuntimeInputCollectionDefinition> childCollections,
        out Control? footer)
    {
        var canEditStructure = collection.CanEditStructure
            && string.IsNullOrWhiteSpace(collection.StorageCollectionJsonKey);
        void Changed()
        {
            _onChanged();
            _reloadAndSelect?.Invoke(owner.Node);
        }
        var selectionKey = $"{owner.Node.Id}:test-values";
        var collectionActions = CreateTestValueCollectionActions(
            owner,
            preview,
            collection,
            items,
            (item, fallbackIndex) => _sessionUiState.Select(
                selectionKey,
                $"{collection.Id}:{ItemId(item, fallbackIndex)}"),
            Changed);
        var sections = new List<EditorInternalNavigationSection>();
        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            var item = items[itemIndex];
            var itemId = ItemId(item, itemIndex);
            var presentation = RuntimeCollectionItemPresentation.Resolve(
                collection,
                item,
                itemIndex,
                $"{collection.ItemLabel} {itemIndex + 1}",
                $"Runtime {collection.ItemLabel.ToLowerInvariant()} {itemIndex + 1}",
                EditorIcons.Component);
            sections.Add(new EditorInternalNavigationSection(
                $"{collection.Id}:{itemId}",
                presentation.Title,
                presentation.Subtitle,
                presentation.Icon,
                CreatePromotedCollectionItemContent(
                    owner,
                    preview,
                    collection,
                    actions,
                    itemIndex,
                    item,
                    childCollections),
                canEditStructure
                    ? EditorCollectionItemControls.CreateActions(
                        collection.ItemLabel,
                        itemIndex,
                        items.Count,
                        collectionActions.AddAfter,
                        collectionActions.Duplicate,
                        collectionActions.Move,
                        collectionActions.Delete)
                    : null));
        }
        var collectionFooter = EditorCollectionItemControls.CreateFooter(
            collection.ItemLabel,
            items.Count,
            canEditStructure,
            collectionActions.AddFirst,
            collectionActions.AddAfter);
        footer = collectionFooter is Panel panel && panel.Children.Count > 0
            ? collectionFooter
            : null;
        return sections;
    }

    private Control CreatePromotedCollectionItemContent(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        int itemIndex,
        JsonObject item,
        IReadOnlyList<RuntimeInputCollectionDefinition> childCollections)
    {
        if (collection.ItemRuntimePresentation.Equals("sections", StringComparison.Ordinal))
        {
            return CreatePromotedRuntimeContractContent(
                owner,
                preview,
                collection,
                actions,
                itemIndex,
                item);
        }

        var result = new StackPanel { Spacing = EditorUiDensity.Card(8) };
        var ownContent = CreateTestValueCollectionItemContent(
            owner,
            preview,
            collection,
            actions,
            itemIndex,
            item,
            () => { },
            out var ownSubcards);
        if (ownContent is Panel ownPanel && ownPanel.Children.Count > 0)
        {
            result.Children.Add(ownContent);
        }
        if (ownSubcards.Count > 0)
        {
            result.Children.Add(new EditorSubcardLayoutHost(
                ownSubcards,
                EditorSubcardLayout.SeparatedSections));
        }

        var editorHost = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        void ShowChildContent(Control content)
        {
            editorHost.Content = content;
            editorHost.InvalidateMeasure();
            result.InvalidateMeasure();
            Dispatcher.UIThread.Post(() =>
            {
                editorHost.InvalidateMeasure();
                result.InvalidateMeasure();
                foreach (var ancestor in result.GetVisualAncestors().OfType<Control>())
                {
                    ancestor.InvalidateMeasure();
                    if (ancestor is ScrollViewer) break;
                }
            }, DispatcherPriority.Background);
        }
        var parentItemId = ItemId(item, itemIndex);
        var selectedKey = $"{owner.Node.Id}:{collection.Id}:{parentItemId}:runtime-child";
        var selectedChildId = _sessionUiState.Selection(selectedKey);
        var childRows = new StackPanel { Spacing = 0 };
        foreach (var childCollection in childCollections)
        {
            var allChildItems = DesignPreviewTestValues.CollectionItems(preview, childCollection).ToList();
            var childItems = allChildItems
                .Where((candidate) =>
                    candidate[childCollection.UiParentItemIdJsonKey] is JsonValue parentValue
                    && parentValue.TryGetValue<string>(out var parentId)
                    && parentId.Equals(parentItemId, StringComparison.Ordinal))
                .ToList();
            foreach (var childItem in childItems)
            {
                var childItemId = ItemId(childItem, 0);
                var globalChildIndex = allChildItems.FindIndex((candidate) =>
                    ItemId(candidate, 0).Equals(childItemId, StringComparison.Ordinal));
                if (globalChildIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Runtime child item '{childItemId}' is not present in collection '{childCollection.Id}'.");
                }
                Control ChildContent() => CreateDirectChildRuntimeContent(
                    owner,
                    preview,
                    childCollection,
                    actions,
                    globalChildIndex,
                    childItem);
                var button = new Button
                {
                    Content = "···",
                    Width = 40,
                    Height = 32,
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };
                EditorOverrideVisuals.ApplyActionButton(button);
                EditorAccessibility.Describe(
                    button,
                    $"Edit runtime values for {childCollection.Label}");
                button.Click += (_, args) =>
                {
                    args.Handled = true;
                    _sessionUiState.Select(selectedKey, childItemId);
                    ShowChildContent(ChildContent());
                };
                var label = new TextBlock
                {
                    Text = childCollection.Label,
                    FontWeight = FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 8,
                    MinHeight = 48,
                    Children =
                    {
                        label,
                        button,
                    },
                };
                Grid.SetColumn(button, 1);
                childRows.Children.Add(new Border
                {
                    Padding = new Thickness(0, 7),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = EditorUiVisuals.ScrollbarSeparatorBrush(
                        Application.Current?.ActualThemeVariant != Avalonia.Styling.ThemeVariant.Light),
                    Child = row,
                });
                if (childItemId.Equals(selectedChildId, StringComparison.Ordinal))
                {
                    ShowChildContent(ChildContent());
                }
            }
        }
        result.Children.Add(childRows);
        result.Children.Add(editorHost);
        return result;
    }

    internal static JsonObject PrepareDefaultPreview(
        string designPreviewJson,
        JsonObject config) =>
        RuntimeInputForwardingContract.EffectivePreview(
            DesignPreviewTestValues.Parse(
                designPreviewJson),
            config);

    private Control CreatePromotedRuntimeContractContent(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        int itemIndex,
        JsonObject item)
    {
        var runtimeContractJsonKey = RuntimeContractJsonKey(collection);
        var itemId = ItemId(item, itemIndex);
        var runtimeContract = JsonPath.RequiredObject(
            item,
            runtimeContractJsonKey,
            $"Runtime collection '{collection.Id}' item '{itemId}'");
        var runtimeInputs = RuntimeInputDefinitionReader.ReadInputs(
            runtimeContract,
            new JsonObject());
        var runtimeCollections = RuntimeInputDefinitionReader.ReadCollections(
            runtimeContract,
            new JsonObject());
        var hiddenInputIds = (collection.ItemRuntimeHiddenInputIds ?? [])
            .ToHashSet(StringComparer.Ordinal);

        async Task PersistRuntimeContract(bool committed)
        {
            item[runtimeContractJsonKey] = runtimeContract.DeepClone();
            if (owner.IsInstance && committed)
            {
                await _instanceDocuments.UpdateCollectionValueAsync(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    runtimeContractJsonKey,
                    runtimeContract);
                _onChanged();
                return;
            }

            _setPreviewCollectionItemValues(
                collection.JsonKey,
                itemId,
                new Dictionary<string, JsonNode?>
                {
                    [runtimeContractJsonKey] = runtimeContract,
                });
            _testValuesChanged();
            await Task.CompletedTask;
        }

        var sections = new List<EditorInternalNavigationSection>();
        var general = new StackPanel { Spacing = 8 };
        var actionRow = CreateCollectionItemActionPanel(
            owner,
            preview,
            collection,
            actions,
            itemId,
            item,
            out var refreshActionVisibility);
        if (actionRow is not null)
        {
            general.Children.Add(actionRow);
        }
        foreach (var input in ComponentInputGrouping.OwnInputs(
                     collection.Fields
                         .Where((candidate) => IsVisibleRuntimeValue(owner, candidate))
                         .ToList()))
        {
            general.Children.Add(CreateTestValueCollectionControl(
                owner,
                collection,
                itemIndex,
                item,
                input,
                refreshActionVisibility,
                () => { }));
        }
        foreach (var input in ComponentInputGrouping.OwnInputs(
                     runtimeInputs.Where((candidate) =>
                         IsVisibleRuntimeValue(owner, candidate)
                         && !hiddenInputIds.Contains(candidate.Id))
                         .ToList()))
        {
            general.Children.Add(CreateEmbeddedRuntimeInputControl(
                owner,
                runtimeContract,
                input,
                itemId,
                PersistRuntimeContract));
        }
        if (general.Children.Count > 0)
        {
            sections.Add(new EditorInternalNavigationSection(
                "general",
                "General",
                "Runtime inputs",
                EditorIcons.General,
                general,
                ShowLabel: false));
        }

        foreach (var runtimeCollection in runtimeCollections
                     .Where((candidate) => string.IsNullOrWhiteSpace(candidate.UiParentCollectionJsonKey)))
        {
            var childCollections = runtimeCollections
                .Where((candidate) => candidate.UiParentCollectionJsonKey.Equals(
                    runtimeCollection.JsonKey,
                    StringComparison.Ordinal))
                .ToList();
            var runtimeItems = DisplayItems(runtimeContract, runtimeCollection);
            if (runtimeCollection.UiPresentation.Equals("itemSections", StringComparison.Ordinal))
            {
                for (var runtimeItemIndex = 0; runtimeItemIndex < runtimeItems.Count; runtimeItemIndex++)
                {
                    var runtimeItem = runtimeItems[runtimeItemIndex];
                    var runtimeItemId = ItemId(runtimeItem, runtimeItemIndex);
                    sections.Add(new EditorInternalNavigationSection(
                        $"{runtimeCollection.Id}:{runtimeItemId}",
                        $"{runtimeCollection.ItemLabel} {runtimeItemIndex + 1}",
                        $"Runtime {runtimeCollection.ItemLabel.ToLowerInvariant()} {runtimeItemIndex + 1}",
                        EditorIcons.Component,
                        CreateEmbeddedRuntimeCollectionItemContent(
                            owner,
                            runtimeContract,
                            runtimeCollection,
                            runtimeItemIndex,
                            runtimeItem,
                            childCollections,
                            itemId,
                            PersistRuntimeContract)));
                }
                continue;
            }

            throw new InvalidOperationException(
                $"Runtime collection '{collection.Id}' sections presentation requires nested collection "
                + $"'{runtimeCollection.Id}' to declare uiPresentation 'itemSections'.");
        }

        return CreateSessionSubcardLayout(
            $"{owner.Node.Id}:{collection.Id}:{itemId}:runtime-contract",
            sections,
            EditorSubcardLayout.VerticalCards,
            RuntimeNavigationWidth);
    }

    private Control CreateEmbeddedRuntimeCollectionItemContent(
        RuntimeInputOwner owner,
        JsonObject runtimeContract,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        JsonObject item,
        IReadOnlyList<RuntimeInputCollectionDefinition> childCollections,
        string temporalOwnerId,
        Func<bool, Task> persistRuntimeContract)
    {
        var result = new StackPanel
        {
            Spacing = EditorUiDensity.Card(8),
            Margin = new Thickness(0, 0, 0, 36),
        };
        var parentItemId = ItemId(item, itemIndex);
        var editorHost = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        void ShowChildContent(Control content)
        {
            editorHost.Content = content;
            editorHost.InvalidateMeasure();
            result.InvalidateMeasure();
            Dispatcher.UIThread.Post(() =>
            {
                editorHost.InvalidateMeasure();
                result.InvalidateMeasure();
                foreach (var ancestor in result.GetVisualAncestors().OfType<Control>())
                {
                    ancestor.InvalidateMeasure();
                    if (ancestor is ScrollViewer) break;
                }
            }, DispatcherPriority.Background);
        }

        var selectedKey =
            $"{temporalOwnerId}:{collection.Id}:{parentItemId}:embedded-runtime-child";
        var selectedChildId = _sessionUiState.Selection(selectedKey);
        var childRows = new StackPanel { Spacing = 0 };
        foreach (var childCollection in childCollections)
        {
            var allChildItems = DesignPreviewTestValues.CollectionItems(
                runtimeContract,
                childCollection).ToList();
            var childItems = allChildItems
                .Where((candidate) =>
                    candidate[childCollection.UiParentItemIdJsonKey] is JsonValue parentValue
                    && parentValue.TryGetValue<string>(out var parentId)
                    && parentId.Equals(parentItemId, StringComparison.Ordinal))
                .ToList();
            foreach (var childItem in childItems)
            {
                var childItemId = ItemId(childItem, 0);
                var childItemIndex = allChildItems.FindIndex((candidate) =>
                    ItemId(candidate, 0).Equals(childItemId, StringComparison.Ordinal));
                if (childItemIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Runtime child item '{childItemId}' is not present in collection '{childCollection.Id}'.");
                }

                async Task PersistChildRuntimeContract(bool committed)
                {
                    allChildItems[childItemIndex] = CloneObject(childItem);
                    runtimeContract[childCollection.JsonKey] = new JsonArray(
                        allChildItems
                            .Select((candidate) => (JsonNode?)CloneObject(candidate))
                            .ToArray());
                    await persistRuntimeContract(committed);
                }

                Control ChildContent() => CreateEmbeddedChildRuntimeContent(
                    owner,
                    childCollection,
                    childItem,
                    temporalOwnerId,
                    PersistChildRuntimeContract);
                var button = new Button
                {
                    Content = "···",
                    Width = 40,
                    Height = 32,
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };
                EditorOverrideVisuals.ApplyActionButton(button);
                EditorAccessibility.Describe(
                    button,
                    $"Edit runtime values for {childCollection.Label}");
                button.Click += (_, args) =>
                {
                    args.Handled = true;
                    _sessionUiState.Select(selectedKey, childItemId);
                    ShowChildContent(ChildContent());
                };
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 8,
                    MinHeight = 48,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = childCollection.Label,
                            FontWeight = FontWeight.SemiBold,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                        button,
                    },
                };
                Grid.SetColumn(button, 1);
                childRows.Children.Add(new Border
                {
                    Padding = new Thickness(0, 7),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = EditorUiVisuals.ScrollbarSeparatorBrush(
                        Application.Current?.ActualThemeVariant != Avalonia.Styling.ThemeVariant.Light),
                    Child = row,
                });
                if (childItemId.Equals(selectedChildId, StringComparison.Ordinal))
                {
                    ShowChildContent(ChildContent());
                }
            }
        }
        result.Children.Add(childRows);
        result.Children.Add(editorHost);
        return result;
    }

    private Control CreateEmbeddedChildRuntimeContent(
        RuntimeInputOwner owner,
        RuntimeInputCollectionDefinition collection,
        JsonObject item,
        string temporalOwnerId,
        Func<bool, Task> persistRuntimeContract)
    {
        var runtimeContract = JsonPath.RequiredObject(
            item,
            RuntimeContractJsonKey(collection),
            $"Runtime collection '{collection.Id}' embedded item");
        var inputs = RuntimeInputDefinitionReader.ReadInputs(
                runtimeContract,
                new JsonObject())
            .Where((input) => IsVisibleRuntimeValue(owner, input))
            .ToList();
        var result = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 36),
        };
        foreach (var input in inputs)
        {
            result.Children.Add(CreateEmbeddedRuntimeInputControl(
                owner,
                runtimeContract,
                input,
                temporalOwnerId,
                persistRuntimeContract));
        }
        return result;
    }

    private Control CreateEmbeddedRuntimeInputControl(
        RuntimeInputOwner owner,
        JsonObject runtimeContract,
        ComponentInputDefinition input,
        string temporalOwnerId,
        Func<bool, Task> persistRuntimeContract)
    {
        var control = new DictionaryFieldControl(
            new FieldValue(
                RuntimeInputFieldDefinitionFactory.Create(ActiveInputOptions, owner.Node, input),
                DesignPreviewTestValues.Value(runtimeContract, input)),
            DictionaryServices(
                owner,
                (_) => "",
                openComponentVariantReference: (reference) =>
                {
                    _navigateToNode(reference);
                    return Task.CompletedTask;
                },
                openRuntimeComponentOverrides: _openEmbeddedContext));
        control.ValueChanged += async (_, next) =>
        {
            runtimeContract[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, next);
            await persistRuntimeContract(false);
        };
        control.ValueCommitted += async (_, next) =>
        {
            runtimeContract[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, next);
            await persistRuntimeContract(true);
        };
        return DecorateAnimationToggle(owner, input, temporalOwnerId, control);
    }

    private Control CreateDirectChildRuntimeContent(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        int itemIndex,
        JsonObject item)
    {
        var content = CreateTestValueCollectionItemContent(
            owner,
            preview,
            collection,
            actions,
            itemIndex,
            item,
            () => { },
            out var subcards);
        var result = new StackPanel
        {
            Spacing = EditorUiDensity.Card(8),
            Margin = new Thickness(0, 8, 0, 36),
        };
        if (content is Panel panel && panel.Children.Count > 0)
        {
            result.Children.Add(content);
        }
        foreach (var subcard in subcards)
        {
            result.Children.Add(EditorSubcardLayoutHost.ComposeSectionContent(subcard));
        }
        return result;
    }

    private StructuredCollectionActions CreateTestValueCollectionActions(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<JsonObject> items,
        Action<JsonObject, int> activate,
        Action changed)
    {
        return new StructuredCollectionActions(
            AddFirst: async () =>
            {
                var item = DefaultCollectionItem(owner, collection);
                activate(item, items.Count);
                if (owner.IsInstance)
                    await _instanceDocuments.AddCollectionItemAsync(
                        owner.Node.Id,
                        StorageCollectionKey(collection),
                        item);
                else
                    _setPreviewCollectionTestItems(owner.Node, collection.JsonKey, [item]);
                changed();
            },
            AddAfter: async (itemIndex) =>
            {
                var currentItem = items[itemIndex];
                var itemId = ItemId(currentItem, itemIndex);
                var next = DefaultCollectionItem(owner, collection);
                activate(next, items.Count);
                if (owner.IsInstance)
                {
                    await _instanceDocuments.InsertCollectionItemAfterAsync(
                        owner.Node.Id,
                        StorageCollectionKey(collection),
                        itemId,
                        next);
                }
                else
                {
                    var current = DesignPreviewTestValues.CollectionItems(preview, collection)
                        .Select(CloneObject)
                        .ToList();
                    current.Insert(Math.Min(itemIndex + 1, current.Count), next);
                    _setPreviewCollectionTestItems(owner.Node, collection.JsonKey, current);
                }
                changed();
            },
            Duplicate: async (itemIndex) =>
            {
                var item = items[itemIndex];
                var itemId = ItemId(item, itemIndex);
                var duplicateId = $"{collection.Id}_{Guid.NewGuid():N}";
                var copy = CloneObject(item);
                copy["id"] = duplicateId;
                RuntimeInputForwardingContract.RebaseIds(copy, itemId, duplicateId);
                var idMappings = StructuredCollectionItemIdentity.RebaseNestedItems(copy, collection)
                    .ToDictionary((entry) => entry.Key, (entry) => entry.Value, StringComparer.Ordinal);
                idMappings[itemId] = duplicateId;
                activate(copy, items.Count);
                if (owner.IsInstance)
                {
                    await _instanceDocuments.DuplicateCollectionItemAsync(
                        owner.Node.Id,
                        StorageCollectionKey(collection),
                        itemId,
                        copy,
                        idMappings);
                }
                else
                {
                    var current = DesignPreviewTestValues.CollectionItems(preview, collection)
                        .Select(CloneObject)
                        .ToList();
                    current.Insert(itemIndex + 1, copy);
                    _setPreviewCollectionTestItems(owner.Node, collection.JsonKey, current);
                }
                changed();
            },
            Move: async (itemIndex, delta) =>
            {
                var itemId = ItemId(items[itemIndex], itemIndex);
                if (owner.IsInstance)
                {
                    await _instanceDocuments.MoveCollectionItemAsync(
                        owner.Node.Id,
                        StorageCollectionKey(collection),
                        itemId,
                        delta);
                }
                else
                {
                    MoveTransientCollectionItem(owner, preview, collection, itemIndex, delta);
                    await Task.CompletedTask;
                }
                changed();
            },
            Delete: async (itemIndex) =>
            {
                var item = items[itemIndex];
                var itemId = ItemId(item, itemIndex);
                var label = RuntimeCollectionItemPresentation.Resolve(
                    collection,
                    item,
                    itemIndex,
                    $"{collection.ItemLabel} {itemIndex + 1}",
                    $"Payload item {itemIndex + 1}",
                    EditorIcons.Component).Title;
                if (!await _confirmCollectionItemDelete(label)) return;
                if (owner.IsInstance)
                {
                    await _instanceDocuments.DeleteCollectionItemAsync(
                        owner.Node.Id,
                        StorageCollectionKey(collection),
                        itemId);
                }
                else
                {
                    var current = DesignPreviewTestValues.CollectionItems(preview, collection)
                        .Select(CloneObject)
                        .ToList();
                    current.RemoveAt(itemIndex);
                    _setPreviewCollectionTestItems(owner.Node, collection.JsonKey, current);
                }
                changed();
            });
    }

    private Control CreateTestValueCollectionContent(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        IReadOnlyList<JsonObject> items,
        IReadOnlyList<RuntimeInputCollectionDefinition>? childCollections = null)
    {
        StructuredCollectionEditor? editor = null;
        void Changed()
        {
            _onChanged();
            _reloadAndSelect?.Invoke(owner.Node);
        }
        var collectionActions = CreateTestValueCollectionActions(
            owner,
            preview,
            collection,
            items,
            (item, fallbackIndex) => editor!.ActivateOnly(item, fallbackIndex),
            Changed);
        editor = new StructuredCollectionEditor(
            owner.IsInstance
                ? StructuredCollectionEditingContext.InstanceRuntime
                : StructuredCollectionEditingContext.RuntimeTestValues,
            $"{owner.Node.Id}:{collection.Id}",
            collection.ItemLabel,
            items,
            ItemId,
            (item, itemIndex) => RuntimeCollectionItemPresentation.Resolve(
                collection,
                item,
                itemIndex,
                $"{collection.ItemLabel} {itemIndex + 1}",
                $"Payload item {itemIndex + 1}",
                EditorIcons.Component),
            (item, itemIndex) =>
            {
                void OpenComponentOverrides() =>
                    OpenRuntimeComponentOverrides(owner, collection, itemIndex, item);
                var content = CreateTestValueCollectionItemContent(
                    owner,
                    preview,
                    collection,
                    actions,
                    itemIndex,
                    item,
                    OpenComponentOverrides,
                    out var itemSubcards);
                if (childCollections is { Count: > 0 })
                {
                    itemSubcards = itemSubcards
                        .Concat(CreateChildRuntimeCollectionSubcards(
                            owner, preview, item, actions, childCollections))
                        .ToList();
                }
                return new StructuredCollectionItemContent(content, itemSubcards);
            },
            collectionActions,
            _sessionUiState,
            canEditStructure: collection.CanEditStructure
                && string.IsNullOrWhiteSpace(collection.StorageCollectionJsonKey));
        var collectionEditor = editor.Create();
        if (!owner.IsInstance
            || _animationEditor is null
            || !collection.AnimationPresentation.Equals("collectionFooter", StringComparison.Ordinal)
            || !collection.Fields.Any((input) => input.Animation is not null))
        {
            return collectionEditor;
        }

        var animation = _animationEditor.CreateCollectionContent(owner.Node, collection);
        return new StackPanel
        {
            Spacing = EditorUiDensity.Card(8),
            Children =
            {
                collectionEditor,
                CreateSessionSubcardLayout(
                    $"{owner.Node.Id}:{collection.Id}:collection-footer",
                    [new EditorInternalNavigationSection(
                        "animation",
                        "Animation",
                        EditorUiText.Count(animation.ActiveTrackCount, "animated property"),
                        EditorIcons.Animation,
                        animation.Content)],
                    EditorSubcardLayout.FlatStack),
            },
        };
    }

    private IReadOnlyList<EditorInternalNavigationSection> CreateChildRuntimeCollectionSubcards(
        RuntimeInputOwner owner,
        JsonObject preview,
        JsonObject parentItem,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        IReadOnlyList<RuntimeInputCollectionDefinition> childCollections)
    {
        var result = new List<EditorInternalNavigationSection>();
        var parentId = ItemId(parentItem, 0);
        foreach (var childCollection in childCollections)
        {
            var childItems = DisplayItems(preview, childCollection)
                .Where((candidate) => candidate[childCollection.UiParentItemIdJsonKey]?.GetValue<string>() == parentId)
                .ToList();
            for (var index = 0; index < childItems.Count; index++)
            {
                var childItem = childItems[index];
                var childItemId = ItemId(childItem, index);
                var childContent = CreateTestValueCollectionItemContent(
                    owner,
                    preview,
                    childCollection,
                    actions,
                    DesignPreviewTestValues.CollectionItems(preview, childCollection).ToList().FindIndex(
                        (candidate) => ItemId(candidate, 0) == childItemId),
                    childItem,
                    () => { },
                    out var childSubcards);
                var presentation = RuntimeCollectionItemPresentation.Resolve(
                    childCollection,
                    childItem,
                    index,
                    $"{childCollection.ItemLabel} {index + 1}",
                    $"{childCollection.ItemLabel} {index + 1}",
                    EditorIcons.Component);
                result.Add(new EditorInternalNavigationSection(
                    childItemId,
                    presentation.Title,
                    presentation.Subtitle,
                    presentation.Icon,
                    childContent,
                    Subcards: childSubcards,
                    SubcardLayout: EditorSubcardLayout.FlatStack));
            }
        }
        return result;
    }

    private Control CreateTestValueCollectionItemContent(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        int itemIndex,
        JsonObject item,
        Action openComponentOverrides,
        out IReadOnlyList<EditorInternalNavigationSection> subcards)
    {
        var content = new StackPanel { Spacing = 8 };
        var itemId = item["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var id)
            ? id
            : "";
        var actionRow = CreateCollectionItemActionPanel(
            owner,
            preview,
            collection,
            actions,
            itemId,
            item,
            out var refreshActionVisibility);
        if (actionRow is not null)
        {
            content.Children.Add(actionRow);
        }
        var visibleCollectionFields = collection.Fields
            .Where((input) => IsVisibleRuntimeValue(owner, input))
            .ToList();
        foreach (var input in ComponentInputGrouping.OwnInputs(visibleCollectionFields))
        {
            content.Children.Add(CreateTestValueCollectionControl(
                owner,
                collection,
                itemIndex,
                item,
                input,
                refreshActionVisibility,
                openComponentOverrides));
        }

        var groups = ComponentInputGrouping.EmbeddedGroups(visibleCollectionFields);
        var topLevelGroupIds = ComponentInputGrouping.TopLevelGroupIds(groups).ToList();
        var groupSubcards = new List<EditorInternalNavigationSection>();
        foreach (var groupId in topLevelGroupIds)
        {
            groupSubcards.Add(CreateTestValueCollectionGroupSubcard(
                owner, preview, collection, itemIndex, item, groupId, groups, refreshActionVisibility));
        }
        var componentItemDefinition = collection.ComponentItems;
        var componentVariantField = componentItemDefinition is null
            ? null
            : collection.Fields.FirstOrDefault((input) => input.JsonKey == componentItemDefinition.VariantReferenceJsonKey);
        var componentVariantReference = componentVariantField is null
            ? ""
            : DesignPreviewTestValues.CollectionValue(item, componentVariantField);
        var itemRuntimeContractJsonKey = !string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey)
            ? collection.ItemRuntimeContractJsonKey
            : componentItemDefinition?.InputsJsonKey ?? "";
        var itemRuntimeContract = itemRuntimeContractJsonKey.Length > 0
            ? JsonPath.RequiredObject(
                item,
                itemRuntimeContractJsonKey,
                $"Runtime collection '{collection.Id}' item '{itemId}'")
            : null;
        var nestedInputs = new List<ComponentInputDefinition>();
        if (itemRuntimeContract is not null
            && (!string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey)
                || !string.IsNullOrWhiteSpace(componentVariantReference)))
        {
            var componentConfig = string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey)
                ? ComponentVariantConfig(componentVariantReference)
                : new JsonObject();
            nestedInputs = RuntimeInputDefinitionReader.ReadInputs(itemRuntimeContract, componentConfig).ToList();
            var nestedActions = actions.Where((action) =>
                    action.IsCollectionItemAction
                    && action.CollectionJsonKey == collection.JsonKey
                    && action.CollectionItemId == itemId
                    && action.TargetJsonPath == itemRuntimeContractJsonKey)
                .ToList();
            if (nestedInputs.Count > 0 || nestedActions.Count > 0)
            {
                var nestedPanel = new StackPanel { Spacing = 6 };
                var applicableNestedActions = nestedActions.Where((action) =>
                        ComponentPreviewActions.AppliesToItem(action, itemRuntimeContract))
                    .ToList();
                if (!owner.IsInstance && applicableNestedActions.Count > 0)
                {
                    var nestedActionPanel = CreateActionPanel();
                    foreach (var nestedAction in applicableNestedActions)
                    {
                        AddActionControl(nestedActionPanel, CreateActionControl(nestedAction, nestedInputs, itemRuntimeContract));
                    }
                    nestedPanel.Children.Add(nestedActionPanel);
                }
                foreach (var nestedInput in nestedInputs.Where((input) => IsVisibleRuntimeValue(owner, input)))
                {
                    nestedPanel.Children.Add(CreateNestedComponentInputControl(
                        owner, collection, itemIndex, item, itemRuntimeContract, nestedInput));
                }
                groupSubcards.Add(new EditorInternalNavigationSection(
                    "componentInputs",
                    string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey) ? "Component inputs" : "Runtime inputs",
                    $"{EditorUiText.Count(nestedInputs.Count, "runtime input")} · {EditorUiText.Count(nestedActions.Count, "action")}",
                    EditorIcons.Component,
                    nestedPanel));
            }
        }
        if (owner.IsInstance
            && _animationEditor is not null
            && ((!collection.AnimationPresentation.Equals("collectionFooter", StringComparison.Ordinal)
                    && collection.Fields.Any((input) => input.Animation is not null))
                || nestedInputs.Any((input) => input.Animation is not null)))
        {
            var animation = _animationEditor.CreateTargetContent(owner.Node, itemId);
            groupSubcards.Add(new EditorInternalNavigationSection(
                "animation",
                "Animation",
                EditorUiText.Count(animation.ActiveTrackCount, "animated property"),
                EditorIcons.Animation,
                animation.Content));
        }
        subcards = groupSubcards;

        return content;
    }

    private WrapPanel? CreateCollectionItemActionPanel(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        string itemId,
        JsonObject item,
        out Action refreshVisibility)
    {
        var itemActions = actions
            .Where((action) => action.IsCollectionItemAction
                && action.CollectionJsonKey == collection.JsonKey
                && action.CollectionItemId == itemId
                && string.IsNullOrWhiteSpace(action.TargetJsonPath))
            .ToList();
        if (owner.IsInstance || itemActions.Count == 0)
        {
            refreshVisibility = () => { };
            return null;
        }

        var actionRow = CreateActionPanel();
        var actionControls = new List<(
            ComponentPreviewActionDefinition Action,
            RuntimeTestActionControl Control)>();
        foreach (var action in itemActions)
        {
            var control = CreateActionControl(action, collection.Fields, item);
            actionControls.Add((action, control));
            AddActionControl(actionRow, control);
        }
        refreshVisibility = () =>
        {
            var currentItem = DesignPreviewTestValues.CollectionItems(preview, collection)
                .FirstOrDefault((candidate) => ItemId(candidate, 0) == itemId)
                ?? item;
            foreach (var (action, control) in actionControls)
            {
                control.IsVisible = ComponentPreviewActions.AppliesToItem(action, currentItem);
            }
            actionRow.IsVisible = actionControls.Any((entry) => entry.Control.IsVisible);
        };
        refreshVisibility();
        return actionRow;
    }

    private static string ItemId(JsonObject item, int index)
    {
        return JsonPath.RequiredString(item, "id", $"Runtime collection item at index {index}");
    }

    private static IReadOnlyList<JsonObject> DisplayItems(
        JsonObject preview,
        RuntimeInputCollectionDefinition collection)
    {
        var items = DesignPreviewTestValues.CollectionItems(preview, collection);
        if (collection.FixedItemCount > 0
            && items.Count != collection.FixedItemCount)
        {
            throw new InvalidOperationException(
                $"Runtime collection '{collection.JsonKey}' requires exactly "
                + $"{collection.FixedItemCount} items but contains {items.Count}.");
        }
        return items;
    }

    private static bool IsVisibleRuntimeValue(RuntimeInputOwner owner, ComponentInputDefinition input) =>
        !input.ActionOnly || (owner.IsInstance && input.Animation is not null);

    private JsonObject DefaultCollectionItem(RuntimeInputOwner owner, RuntimeInputCollectionDefinition collection)
    {
        var item = new JsonObject { ["id"] = $"{collection.Id}_{Guid.NewGuid():N}" };
        foreach (var field in collection.Fields)
        {
            var value = field.DefaultValue;
            if (field.ValueKind == ValueKind.ComponentVariant && string.IsNullOrWhiteSpace(value))
            {
                var options = RuntimeInputFieldDefinitionFactory.Create(ActiveInputOptions, owner.Node, field).Options ?? [];
                value = ComponentVariantOptionContract.SelectsComponentClass(field.ComponentType)
                    ? ""
                    : ComponentVariantOptionContract.RequireFixedBoundary(
                        options,
                        $"Runtime collection field '{field.Id}'").DefaultVariantReference;
            }
            item[field.JsonKey] = DesignPreviewTestValues.ValueNode(field, value);
        }
        var componentItems = collection.ComponentItems;
        var variant = componentItems is null
            ? null
            : collection.Fields.FirstOrDefault((field) => field.JsonKey == componentItems.VariantReferenceJsonKey);
        if (variant is not null && componentItems is not null)
        {
            var reference = item[variant.JsonKey]?.GetValue<string>() ?? "";
            item[componentItems.OverridesJsonKey] = new JsonObject();
            item[componentItems.InputsJsonKey] = string.IsNullOrWhiteSpace(reference)
                ? new JsonObject()
                : _ownerDocuments.ComponentVariantRuntimeInputs(reference);
        }
        if (!string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey))
        {
            var config = DesignPreviewTestValues.Parse(owner.ConfigJson);
            var reference = RuntimeCollectionItemContractOwner
                .ResolveItemVariantReference(
                item,
                collection,
                config,
                ComponentVariantConfig);
            item[collection.ItemRuntimeContractJsonKey] =
                _ownerDocuments.ComponentVariantRuntimeInputs(reference);
        }
        StructuredCollectionItemIdentity.RebaseNestedItems(item, collection);
        return item;
    }

    private void OpenRuntimeComponentOverrides(
        RuntimeInputOwner owner,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        JsonObject item)
    {
        var componentItems = collection.ComponentItems
            ?? throw new InvalidOperationException($"Collection '{collection.Id}' has no component item contract.");
        var variantField = collection.Fields.Single((field) => field.JsonKey == componentItems.VariantReferenceJsonKey);
        var variantReference = DesignPreviewTestValues.CollectionValue(item, variantField);
        if (string.IsNullOrWhiteSpace(variantReference)) return;
        var overrides = RuntimeComponentCollectionItemDocumentContract.RequireOverrides(
            item,
            componentItems.DocumentKeys,
            $"Runtime collection '{collection.Id}' item '{ItemId(item, itemIndex)}'");
        var selected = _ownerDocuments.ComponentVariantSelection(variantReference);
        async Task ApplyOverrides(JsonObject nextOverrides)
        {
            var itemId = ItemId(
                item,
                itemIndex);
            if (owner.IsInstance)
            {
                await _instanceDocuments.UpdateCollectionValueAsync(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    componentItems.OverridesJsonKey,
                    nextOverrides);
            }
            item[componentItems.OverridesJsonKey] =
                nextOverrides.DeepClone();
            _setPreviewCollectionItemValues(
                collection.JsonKey,
                itemId,
                new Dictionary<string, JsonNode?>
                {
                    [componentItems.OverridesJsonKey] =
                        nextOverrides,
                });
            if (owner.IsInstance)
            {
                _onChanged();
            }
            _testValuesChanged();
        }
        _openEmbeddedContext(new EditorEmbeddedContext(
            owner.Node,
            [],
            new RuntimeComponentOverrideSource(
                selected.ProjectId,
                variantReference,
                selected.ComponentType,
                selected.RecordClassId,
                selected.ConfigJson,
                overrides,
                ApplyOverrides)));
    }

    private static JsonObject CloneObject(JsonObject source) =>
        source.DeepClone().AsObject();

    private void MoveTransientCollectionItem(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        int delta)
    {
        var current = DesignPreviewTestValues.CollectionItems(preview, collection).Select(CloneObject).ToList();
        var target = itemIndex + delta;
        if (itemIndex < 0 || target < 0 || target >= current.Count) return;
        var item = current[itemIndex];
        current.RemoveAt(itemIndex);
        current.Insert(target, item);
        _setPreviewCollectionTestItems(owner.Node, collection.JsonKey, current);
    }

    private Control CreateTestValueCollectionControl(
        RuntimeInputOwner owner,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        JsonObject item,
        ComponentInputDefinition input,
        Action? afterCommit = null,
        Action? openComponentOverrides = null)
    {
        var componentItems = collection.ComponentItems;
        var selectsComponent = componentItems is not null
            && input.JsonKey.Equals(componentItems.VariantReferenceJsonKey, StringComparison.Ordinal);
        var hasComponentOverrides = selectsComponent
            && componentItems is not null
            && item[componentItems.OverridesJsonKey] is JsonObject currentOverrides
            && ComponentOverrideCount(currentOverrides) > 0;
        var services = DictionaryServices(owner, (fieldId) =>
        {
            var source = collection.Fields.FirstOrDefault((candidate) => candidate.Id == fieldId);
            return source is null ? "" : DesignPreviewTestValues.CollectionValue(item, source);
        },
        openComponentVariantReference: (reference) =>
        {
            _navigateToNode(reference);
            return Task.CompletedTask;
        },
        openEmbeddedComponent: selectsComponent && openComponentOverrides is not null
            ? (_) =>
            {
                openComponentOverrides();
                return Task.CompletedTask;
            }
            : null,
        openRuntimeComponentOverrides: _openEmbeddedContext) with
        {
            DecorateStructuredCollectionField = owner.IsInstance
                ? (nestedInput, targetId, nestedControl) => DecorateAnimationToggle(owner, nestedInput, targetId, nestedControl)
                : null,
            RemoveStructuredCollectionAnimationTargets = owner.IsInstance
                ? async (targetIds) =>
                {
                    var document = new ModuleInstanceAnimationDocument(
                        PreparedAnimationJson(owner));
                    foreach (var targetId in targetIds) document.RemoveTarget(targetId);
                    _preparedAnimationJson =
                        await _instanceDocuments.SaveAnimationJsonAsync(
                            owner.Node.Id,
                            document.ToJson());
                    _onChanged();
                }
                : null,
            DuplicateStructuredCollectionAnimationTargets = owner.IsInstance
                ? async (targetIds) =>
                {
                    var document = new ModuleInstanceAnimationDocument(
                        PreparedAnimationJson(owner));
                    document.DuplicateTargets(targetIds);
                    _preparedAnimationJson =
                        await _instanceDocuments.SaveAnimationJsonAsync(
                            owner.Node.Id,
                            document.ToJson());
                    _onChanged();
                }
                : null,
        };
        var definition = RuntimeInputFieldDefinitionFactory.Create(
            ActiveInputOptions,
            owner.Node,
            input,
            CollectionFieldAvailability.AllowsEmpty(item, input));
        if (!string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey))
        {
            definition = definition with { Options = RuntimeInputDynamicOptions.Resolve(ActiveInputOptions, input, item) };
        }
        var control = new DictionaryFieldControl(
            new FieldValue(
                definition,
                DesignPreviewTestValues.CollectionValue(item, input),
                IsHighlighted: hasComponentOverrides),
            services);
        var fieldIsActive = CollectionFieldAvailability.IsEnabled(item, input, itemIndex);
        control.IsEnabled = fieldIsActive;
        control.IsVisible = fieldIsActive;
        control.ValueCommitted += async (_, next) =>
        {
            var itemId = item["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var id)
                ? id
                : "";
            var nextNode = DesignPreviewTestValues.ValueNode(input, next);
            item[input.JsonKey] = nextNode?.DeepClone();
            var updates = new Dictionary<string, JsonNode?>
            {
                [input.JsonKey] = nextNode,
            };
            var transitioned = ApplyCollectionTransition(collection, item, input, next, updates);
            if (selectsComponent && componentItems is not null)
            {
                item[componentItems.OverridesJsonKey] = new JsonObject();
                item[componentItems.InputsJsonKey] = string.IsNullOrWhiteSpace(next)
                    ? new JsonObject()
                    : _ownerDocuments.ComponentVariantRuntimeInputs(next);
                updates[componentItems.OverridesJsonKey] = item[componentItems.OverridesJsonKey];
                updates[componentItems.InputsJsonKey] = item[componentItems.InputsJsonKey];
            }
            if (owner.IsInstance)
            {
                await _instanceDocuments.UpdateCollectionValuesAsync(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    updates);
                _onChanged();
            }
            else
            {
                _setPreviewCollectionItemValues(
                    collection.JsonKey,
                    itemId,
                    updates);
            }
            _testValuesChanged();
            afterCommit?.Invoke();
            if (selectsComponent
                || collection.Fields.Any((candidate) =>
                    candidate.EnabledWhenItemJsonKey.Equals(input.JsonKey, StringComparison.Ordinal)
                    || candidate.BehaviorTiming?.SourceFieldId.Equals(input.Id, StringComparison.Ordinal) == true))
            {
                _reloadAndSelect?.Invoke(owner.Node);
            }
        };
        var targetId = item["id"]?.GetValue<string>() ?? "";
        return DecorateAnimationToggle(owner, input, targetId, control, collection.Fields);
    }

    private static bool ApplyCollectionTransition(
        RuntimeInputCollectionDefinition collection,
        JsonObject item,
        ComponentInputDefinition input,
        string next,
        IDictionary<string, JsonNode?> updates)
    {
        var transition = input.Transition;
        if (transition is null
            || transition.ForwardedTargetOnly
            || !transition.TriggerValues.Contains(next, StringComparer.Ordinal))
        {
            return false;
        }
        var target = collection.Fields.FirstOrDefault((candidate) =>
            candidate.Id.Equals(transition.TargetInputId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Collection input transition target '{transition.TargetInputId}' was not declared.");
        var current = DesignPreviewTestValues.CollectionValue(item, target);
        if (!string.IsNullOrWhiteSpace(transition.TargetValuePattern)
            && Regex.IsMatch(current, transition.TargetValuePattern, RegexOptions.CultureInvariant))
        {
            return false;
        }
        var replacement = DesignPreviewTestValues.ValueNode(target, transition.ReplacementValue);
        item[target.JsonKey] = replacement?.DeepClone();
        updates[target.JsonKey] = replacement;
        return true;
    }

    private Control CreateNestedComponentInputControl(
        RuntimeInputOwner owner,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        JsonObject item,
        JsonObject componentInputs,
        ComponentInputDefinition input)
    {
        var control = new DictionaryFieldControl(
            new FieldValue(
                RuntimeInputFieldDefinitionFactory.Create(ActiveInputOptions, owner.Node, input),
                DesignPreviewTestValues.Value(componentInputs, input)),
            DictionaryServices(
                owner,
                (_) => "",
                openComponentVariantReference: (reference) =>
                {
                    _navigateToNode(reference);
                    return Task.CompletedTask;
                },
                openRuntimeComponentOverrides: _openEmbeddedContext));
        void ApplyTransientValue(string next)
        {
            componentInputs[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, next);
            var inputsJsonKey = RuntimeContractJsonKey(collection);
            item[inputsJsonKey] = componentInputs.DeepClone();
            _setPreviewCollectionItemValues(
                collection.JsonKey,
                ItemId(item, itemIndex),
                new Dictionary<string, JsonNode?>
                {
                    [inputsJsonKey] = componentInputs,
                });
            _testValuesChanged();
        }
        control.ValueChanged += (_, next) => ApplyTransientValue(next);
        control.ValueCommitted += async (_, next) =>
        {
            componentInputs[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, next);
            var inputsJsonKey = RuntimeContractJsonKey(collection);
            item[inputsJsonKey] = componentInputs.DeepClone();
            var itemId = ItemId(item, itemIndex);
            if (owner.IsInstance)
            {
                await _instanceDocuments.UpdateCollectionValueAsync(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    inputsJsonKey,
                    componentInputs);
                _onChanged();
            }
        };
        return DecorateAnimationToggle(owner, input, ItemId(item, itemIndex), control);
    }

    private static string RuntimeContractJsonKey(RuntimeInputCollectionDefinition collection)
    {
        if (!string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey))
            return collection.ItemRuntimeContractJsonKey;
        return collection.ComponentItems?.InputsJsonKey
            ?? throw new InvalidOperationException($"Collection '{collection.Id}' has no item runtime contract.");
    }

    private static int ComponentOverrideCount(JsonObject overrides)
    {
        var count = 0;
        void Visit(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject objectValue:
                    foreach (var value in objectValue.Select((entry) => entry.Value)) Visit(value);
                    break;
                case JsonArray arrayValue:
                    foreach (var value in arrayValue) Visit(value);
                    break;
                case not null:
                    count++;
                    break;
            }
        }
        Visit(overrides);
        return count;
    }

    private Control DecorateAnimationToggle(
        RuntimeInputOwner owner,
        ComponentInputDefinition input,
        string targetId,
        DictionaryFieldControl control,
        IReadOnlyList<ComponentInputDefinition>? ownerInputs = null)
    {
        if (!owner.IsInstance) return control;
        var document = new ModuleInstanceAnimationDocument(
            PreparedAnimationJson(owner));
        var timingOwnedByTrack = ownerInputs?.Any((candidate) =>
            candidate.Animation is { } animation
            && animation.BaseDurationFieldId.Equals(input.Id, StringComparison.Ordinal)
            && document.HasTrack(candidate.Id, targetId)) == true;
        if (timingOwnedByTrack)
        {
            control.IsEnabled = false;
            ToolTip.SetTip(control, "Duration is owned by the active animation track.");
        }
        if (input.Animation is null) return control;
        var active = document.HasTrack(input.Id, targetId);
        var baseValue = control.Value;
        var toggle = new Button
        {
            Content = EditorTimelineTransport.CreateAnimationActivationGlyph(
                filled: active,
                extendsOwnerDuration: input.Animation.ExtendsOwnerDuration,
                size: 16,
                brush: active
                    ? EditorAnimationVisuals.ActiveTrackBrush
                    : EditorAnimationVisuals.InactiveTrackBrush),
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = active
                ? EditorAnimationVisuals.ActiveTrackBrush
                : EditorAnimationVisuals.InactiveTrackBrush,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 4, 0),
        };
        EditorAccessibility.Describe(toggle, active
            ? $"Disable animation for {input.Label}"
            : $"Enable animation for {input.Label}");
        toggle.Click += async (_, args) =>
        {
            args.Handled = true;
            if (active)
            {
                if (!await _confirmAnimationDisable(input.Label)) return;
                document.RemoveTrack(input.Id, targetId);
            }
            else if (_animationEditor is not null)
            {
                _animationEditor.AddInitialTrack(
                    document,
                    owner.Node,
                    input,
                    targetId,
                    control.Value);
            }
            else document.AddTrack(
                input.Id,
                targetId,
                DesignPreviewTestValues.ValueNode(input, control.Value) ?? JsonValue.Create(control.Value)!,
                input.Animation.Interpolations.First());
            _preparedAnimationJson =
                await _instanceDocuments.SaveAnimationJsonAsync(
                    owner.Node.Id,
                    document.ToJson());
            if (_reloadAndSelect is not null) _reloadAndSelect(owner.Node);
            else _onChanged();
        };
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 2,
        };
        row.Children.Add(toggle);
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        if (active && _animationEditor is not null)
        {
            control.IsEnabled = false;
            void RefreshResolvedValue() => control.SetPresentedValue(
                _animationEditor.ResolveRuntimeValue(owner.Node, input, targetId, baseValue));
            PreviewPlaybackStateBinding.Attach(row, _playbackState, RefreshResolvedValue);
        }
        return row;
    }

    private string PreparedAnimationJson(
        RuntimeInputOwner owner)
    {
        if (!owner.IsInstance)
        {
            throw new InvalidOperationException(
                $"Definition '{owner.Node.Id}' has no persisted animation document.");
        }
        return _preparedAnimationJson
            ?? throw new InvalidOperationException(
                $"Production Screen '{owner.Node.Id}' requires its prepared animation document.");
    }

    private EditorInternalNavigationSection CreateTestValueCollectionGroupSubcard(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        JsonObject item,
        string groupId,
        IReadOnlyDictionary<string, List<ComponentInputDefinition>> groups,
        Action? afterCommit = null)
    {
        var groupInputs = groups[groupId];
        var content = new StackPanel { Spacing = 8 };
        var sectionLabel = "";
        foreach (var input in groupInputs)
        {
            if (!string.IsNullOrWhiteSpace(input.UiSectionLabel)
                && !string.Equals(sectionLabel, input.UiSectionLabel, StringComparison.Ordinal))
            {
                content.Children.Add(EditorGroupBlock.CreateInlineSection(input.UiSectionLabel));
                sectionLabel = input.UiSectionLabel;
            }
            content.Children.Add(CreateTestValueCollectionControl(
                owner,
                collection,
                itemIndex,
                item,
                input,
                afterCommit));
        }

        var childSubcards = new List<EditorInternalNavigationSection>();
        foreach (var childId in ComponentInputGrouping.ChildGroupIds(groupId, groups))
        {
            childSubcards.Add(CreateTestValueCollectionGroupSubcard(
                owner, preview, collection, itemIndex, item, childId, groups, afterCommit));
        }
        return new EditorInternalNavigationSection(
            groupId,
            ComponentInputGrouping.GroupLabel(groupInputs),
            "Runtime inputs",
            EditorIcons.Component,
            content,
            Subcards: childSubcards,
            SubcardLayout: EditorSubcardLayout.FlatStack);
    }

    private EditorInternalNavigationSection CreateTestValueGroupSubcard(
        RuntimeInputOwner owner,
        JsonObject preview,
        string groupId,
        IReadOnlyDictionary<string, List<ComponentInputDefinition>> groups)
    {
        var groupInputs = groups[groupId];
        var content = CreateSeparatedInputContent(owner, preview, groupInputs);
        var childSubcards = new List<EditorInternalNavigationSection>();
        foreach (var childId in ComponentInputGrouping.ChildGroupIds(groupId, groups))
        {
            childSubcards.Add(CreateTestValueGroupSubcard(owner, preview, childId, groups));
        }
        return new EditorInternalNavigationSection(
            groupId,
            ComponentInputGrouping.GroupLabel(groupInputs),
            "Runtime inputs",
            EditorIcons.Component,
            content,
            Subcards: childSubcards,
            SubcardLayout: EditorSubcardLayout.FlatStack);
    }

    private Control CreateSessionSubcardLayout(
        string stateKey,
        IReadOnlyList<EditorInternalNavigationSection> sections,
        EditorSubcardLayout layout,
        double defaultNavigationWidth = EditorInternalNavigation.DefaultNavigationWidth)
    {
        var selectedId = _sessionUiState.Selection(stateKey);
        var navigationWidth = _sessionUiState.NavigationWidth(
            stateKey,
            defaultNavigationWidth);
        return new EditorSubcardLayoutHost(
            sections,
            layout,
            selectedId,
            (next) => _sessionUiState.Select(stateKey, next),
            navigationWidth,
            (next) => _sessionUiState.SetNavigationWidth(stateKey, next));
    }

    private RuntimeTestActionControl CreateActionControl(
        ComponentPreviewActionDefinition action,
        IReadOnlyList<ComponentInputDefinition> inputs,
        JsonObject values)
    {
        var targetInput = string.IsNullOrWhiteSpace(action.TargetInputId)
            ? null
            : inputs.FirstOrDefault((input) => input.JsonKey == action.TargetInputId);
        var targetOptions = action.TargetMode == ComponentPreviewActionTargetMode.Option
            ? action.TargetOptions.Count > 0 ? action.TargetOptions : RuntimeInputDynamicOptions.Resolve(ActiveInputOptions, targetInput, values)
            : null;
        var currentTargetValue = targetInput is null
            ? ""
            : DesignPreviewTestValues.Value(values, targetInput);
        return new RuntimeTestActionControl(
            action.Label,
            (targetValue) => _triggerAction(action.Id, targetValue),
            () => _restoreAction(action.Id),
            () => _canRestoreAction(action.Id),
            (targetValue, delta) => _stepAction(action.Id, delta, targetValue),
            (delta) => _canStepAction(action.Id, delta),
            (targetValue, frame) => _setActionFrame(action.Id, frame, targetValue),
            () => _currentActionFrame(action.Id),
            () => _maximumActionFrame(action.Id),
            _playbackState,
            targetOptions,
            currentTargetValue);
    }

    private static WrapPanel CreateActionPanel() => new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private static void AddActionControl(WrapPanel panel, RuntimeTestActionControl control)
    {
        control.Margin = new Thickness(0, 0, 6, 6);
        panel.Children.Add(control);
    }

    private RuntimeInputOwner ResolveOwner(ProjectTreeNode node)
    {
        var source = _ownerDocuments.Load(node);
        return new RuntimeInputOwner(
            node,
            source.ConfigJson,
            source.RuntimePreviewJson,
            source.IsInstance
                ? (_) => Task.CompletedTask
                : (json) => _ownerDocuments.SaveDesignPreviewJsonAsync(source, json),
            source.IsInstance);
    }

    private JsonObject ComponentVariantConfig(
        string variantReference)
    {
        if (_preparedDictionaryContext is not null
            && _preparedDictionaryContext.TryVariantSelection(
                variantReference,
                out var selection))
        {
            return DesignPreviewTestValues.Parse(
                selection.ConfigJson);
        }
        return _previewInputData.ComponentVariantConfig(
            variantReference);
    }

    private DictionaryFieldServices DictionaryServices(
        RuntimeInputOwner owner,
        Func<string, string> getFieldValue,
        Func<string, Task>? openComponentVariantReference = null,
        Func<string, Task>? openEmbeddedComponent = null,
        Func<FieldDefinition, ComponentInputBindingDefinition, Task>?
            openComponentInputBinding = null,
        Action<EditorEmbeddedContext>?
            openRuntimeComponentOverrides = null)
    {
        return _preparedDictionaryContext is null
            ? _dictionaryServices.ForNode(
                owner.Node,
                getFieldValue,
                openComponentVariantReference,
                openEmbeddedComponent,
                openComponentInputBinding,
                openRuntimeComponentOverrides)
            : _dictionaryServices.ForPreparedNode(
                owner.Node,
                _preparedDictionaryContext,
                getFieldValue,
                openComponentVariantReference,
                openEmbeddedComponent,
                openComponentInputBinding,
                openRuntimeComponentOverrides);
    }

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent ?? throw new InvalidOperationException($"{node.Kind} has no project ancestor.");
        }

        return current;
    }

    private static string StorageCollectionKey(RuntimeInputCollectionDefinition collection) =>
        !string.IsNullOrWhiteSpace(collection.StorageCollectionJsonKey)
            ? collection.StorageCollectionJsonKey
            : string.IsNullOrWhiteSpace(collection.SourceCollectionJsonKey)
                ? collection.JsonKey
                : collection.SourceCollectionJsonKey;

}
