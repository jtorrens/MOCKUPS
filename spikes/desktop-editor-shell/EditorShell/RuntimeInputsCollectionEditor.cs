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
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RuntimeInputsCollectionEditor
{
    private readonly ComponentPreviewInputDataSource _previewInputData;
    private readonly RuntimeInputOwnerDocumentStore _ownerDocuments;
    private readonly RuntimeInputInstanceDocumentStore _instanceDocuments;
    private readonly RuntimeInputOptionsDataSource _runtimeInputOptions;
    private readonly EditorDictionaryFieldServices _dictionaryServices;
    private readonly Action _onChanged;
    private readonly Action<string, string?> _triggerAction;
    private readonly Action<string> _restoreAction;
    private readonly Func<string, bool> _canRestoreAction;
    private readonly Action<string, string> _setPreviewTestValue;
    private readonly Action<string, string, ComponentInputDefinition, string> _setPreviewCollectionTestValue;
    private readonly Action<string, IReadOnlyList<JsonObject>> _setPreviewCollectionTestItems;
    private readonly Func<ProjectTreeNode, JsonObject, JsonObject> _applyTransientTestValues;
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

    public RuntimeInputsCollectionEditor(
        SpikeDatabase database,
        EditorDictionaryFieldServices dictionaryServices,
        Action onChanged,
        Action<string, string?> triggerAction,
        Action<string> restoreAction,
        Func<string, bool> canRestoreAction,
        Action<string, string> setPreviewTestValue,
        Action<string, string, ComponentInputDefinition, string> setPreviewCollectionTestValue,
        Action<string, IReadOnlyList<JsonObject>> setPreviewCollectionTestItems,
        Func<ProjectTreeNode, JsonObject, JsonObject> applyTransientTestValues,
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
        _previewInputData = new ComponentPreviewInputDataSource(database);
        _ownerDocuments = new RuntimeInputOwnerDocumentStore(database);
        _instanceDocuments = new RuntimeInputInstanceDocumentStore(database);
        _runtimeInputOptions = new RuntimeInputOptionsDataSource(database);
        _dictionaryServices = dictionaryServices;
        _onChanged = onChanged;
        _triggerAction = triggerAction;
        _restoreAction = restoreAction;
        _canRestoreAction = canRestoreAction;
        _setPreviewTestValue = setPreviewTestValue;
        _setPreviewCollectionTestValue = setPreviewCollectionTestValue;
        _setPreviewCollectionTestItems = setPreviewCollectionTestItems;
        _applyTransientTestValues = applyTransientTestValues;
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

    public InstantEditorCard Create(ProjectTreeNode node)
    {
        var surface = LoadSurface(node);
        if (surface.Owner.IsInstance)
        {
            throw new InvalidOperationException(
                "Production Screen Payload belongs to the Preview authoring surface.");
        }

        return CreateRuntimeContractCard(surface);
    }

    public Control CreateProductionScreenPayloadSurface(ProjectTreeNode node)
    {
        var surface = LoadSurface(node);
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

    public Control? CreateDesignTestValuesSurface(ProjectTreeNode node)
    {
        var surface = LoadSurface(node);
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

    private InstantEditorCard CreateRuntimeContractCard(RuntimeInputSurface surface)
    {
        var card = new InstantEditorCard(
            EditorCardHeader.Create(
                "Runtime Contract",
                $"{EditorUiText.Count(surface.Inputs.Count, "input")} · {EditorUiText.Count(surface.Collections.Count, "collection")}",
                EditorIcons.CreateSemantic("Runtime Contract", EditorIcons.Design, 18)),
            new Border
            {
                Padding = new Thickness(10),
                Child = CreateApiTab(surface.Owner, surface.Inputs, surface.Collections),
            },
            isExpanded: false)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SessionStateId = "collection:runtime-contract",
        };
        EditorGroupBlock.ApplyContentSeparator(card);
        return card;
    }

    private RuntimeInputSurface LoadSurface(ProjectTreeNode node)
    {
        var owner = ResolveOwner(node);
        var persistedPreview = DesignPreviewTestValues.Parse(owner.DesignPreviewJson);
        var config = DesignPreviewTestValues.Parse(owner.ConfigJson);
        var preview = RuntimeInputForwardingContract.EffectivePreview(
            _applyTransientTestValues(owner.Node, persistedPreview),
            config);
        var inputs = ComponentPreviewInputSession.ReadRuntimeInputs(preview, config);
        var collections = ComponentPreviewInputSession.ReadRuntimeCollections(preview, config);
        var actions = ComponentPreviewActions.ReadWithEmbedded(
            preview,
            _previewInputData.ComponentVariantRuntimeContract);
        return new RuntimeInputSurface(owner, preview, inputs, collections, actions);
    }

    private Control CreateApiTab(
        RuntimeInputOwner owner,
        IReadOnlyList<ComponentInputDefinition> inputs,
        IReadOnlyList<RuntimeInputCollectionDefinition> collections)
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        if (inputs.Count == 0 && collections.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "This definition exposes no runtime inputs.", Opacity = 0.68 });
            return panel;
        }

        var visibleInputs = inputs.Where((input) => !input.ActionOnly).ToList();
        var groups = ComponentInputGrouping.EmbeddedGroups(visibleInputs);
        var sections = new List<EditorInternalNavigationSection>();
        var topLevelGroupIds = ComponentInputGrouping.TopLevelGroupIds(groups).ToList();
        var ownInputs = ComponentInputGrouping.OwnInputs(visibleInputs).ToList();
        if (ownInputs.Count > 0)
        {
            var general = new StackPanel { Spacing = 6 };
            foreach (var input in ownInputs) general.Children.Add(CreateApiInputRow(input));
            sections.Add(new EditorInternalNavigationSection(
                "general",
                "General",
                "API fields",
                EditorIcons.General,
                general));
        }

        foreach (var groupId in topLevelGroupIds)
        {
            sections.Add(CreateApiGroupSubcard(groupId, groups));
        }
        foreach (var collection in collections)
        {
            sections.Add(new EditorInternalNavigationSection(
                collection.Id,
                collection.Label,
                "Collection API",
                EditorIcons.Component,
                CreateApiCollectionContent(collection)));
        }
        panel.Children.Add(CreateSessionSubcardLayout(
            $"{owner.Node.Id}:runtime-api",
            sections,
            EditorSubcardLayout.VerticalCards));

        return panel;
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
                var current = _applyTransientTestValues(owner.Node, DesignPreviewTestValues.Parse(owner.DesignPreviewJson));
                var baseline = DesignPreviewTestValues.Parse(owner.DesignPreviewJson);
                var currentInputs = ComponentPreviewInputSession.ReadRuntimeInputs(current, config: DesignPreviewTestValues.Parse(owner.ConfigJson));
                var currentCollections = ComponentPreviewInputSession.ReadRuntimeCollections(current, DesignPreviewTestValues.Parse(owner.ConfigJson));
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
                var current = _applyTransientTestValues(owner.Node, DesignPreviewTestValues.Parse(owner.DesignPreviewJson));
                var differences = DesignPreviewTestValues.Differences(current, DesignPreviewTestValues.Parse(owner.DesignPreviewJson), inputs, collections);
                if (differences.Count == 0 || !await _confirmSaveDefaults(owner.Node.Name, differences.Select((difference) => difference.Label).ToList())) return;
                DesignPreviewTestValues.PromoteToDefaults(current, inputs, collections);
                owner.Save(current.ToJsonString());
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
                        childCollections));
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
                EditorSubcardLayout.VerticalCards));
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
        void OnPlaybackStateChanged() => UpdatePlaybackState();
        _playbackState.Changed += OnPlaybackStateChanged;
        surface.DetachedFromVisualTree += (_, _) => _playbackState.Changed -= OnPlaybackStateChanged;
        UpdatePlaybackState();
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
            content.Children.Add(CreateTestValueControl(owner, preview, input));
        }
        return content;
    }

    private Control CreateApiInputRow(ComponentInputDefinition input)
    {
        return new Border
        {
            Padding = new Thickness(8, 6),
            Child = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = input.Label, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                    new TextBlock { Text = $"{input.ValueKind} · Payload key: {input.JsonKey}", FontSize = 11, Opacity = 0.7 },
                },
            },
        };
    }

    private Control CreateApiCollectionContent(RuntimeInputCollectionDefinition collection)
    {
        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = $"Runtime array · Collection key: {collection.JsonKey}",
            FontSize = 11,
            Opacity = 0.7,
        });
        foreach (var field in collection.Fields)
        {
            content.Children.Add(CreateApiInputRow(field));
        }
        return content;
    }

    private EditorInternalNavigationSection CreateApiGroupSubcard(
        string groupId,
        IReadOnlyDictionary<string, List<ComponentInputDefinition>> groups)
    {
        var groupInputs = groups[groupId];
        var content = new StackPanel { Spacing = 6 };
        foreach (var input in groupInputs)
        {
            content.Children.Add(CreateApiInputRow(input));
        }
        var childSubcards = new List<EditorInternalNavigationSection>();
        foreach (var childId in ComponentInputGrouping.ChildGroupIds(groupId, groups))
        {
            childSubcards.Add(CreateApiGroupSubcard(childId, groups));
        }
        return new EditorInternalNavigationSection(
            groupId,
            ComponentInputGrouping.GroupLabel(groupInputs),
            "API fields",
            EditorIcons.Component,
            content,
            Subcards: childSubcards,
            SubcardLayout: EditorSubcardLayout.FlatStack);
    }

    private Control CreateTestValueControl(
        RuntimeInputOwner owner,
        JsonObject preview,
        ComponentInputDefinition input)
    {
        var value = DesignPreviewTestValues.Value(preview, input);
        var definition = RuntimeInputFieldDefinitionFactory.Create(
            _runtimeInputOptions,
            owner.Node,
            input);
        if (!string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey))
        {
            definition = definition with
            {
                Options = RuntimeInputDynamicOptions.Resolve(_runtimeInputOptions, input, preview),
            };
        }
        var control = new DictionaryFieldControl(
            new FieldValue(definition, value),
            _dictionaryServices.ForNode(
                owner.Node,
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
            _setPreviewTestValue(input.JsonKey, next);
            _testValuesChanged();
        };
        control.ValueCommitted += (_, next) =>
        {
            if (owner.IsInstance)
            {
                _instanceDocuments.UpdateRuntimeValue(owner.Node.Id, input.JsonKey, DesignPreviewTestValues.ValueNode(input, next));
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
        return DecorateAnimationToggle(owner, input, "", control);
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
        IReadOnlyList<RuntimeInputCollectionDefinition> childCollections)
    {
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
                    childCollections)));
        }
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

    private Control CreatePromotedRuntimeContractContent(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        JsonObject item)
    {
        var runtimeContractJsonKey = RuntimeContractJsonKey(collection);
        var itemId = ItemId(item, itemIndex);
        var runtimeContract = JsonPath.RequiredObject(
            item,
            runtimeContractJsonKey,
            $"Runtime collection '{collection.Id}' item '{itemId}'");
        var runtimeInputs = ComponentPreviewInputSession.ReadRuntimeInputs(
            runtimeContract,
            new JsonObject());
        var runtimeCollections = ComponentPreviewInputSession.ReadRuntimeCollections(
            runtimeContract,
            new JsonObject());
        var hiddenInputIds = (collection.ItemRuntimeHiddenInputIds ?? [])
            .ToHashSet(StringComparer.Ordinal);

        void PersistRuntimeContract(bool committed)
        {
            item[runtimeContractJsonKey] = runtimeContract.DeepClone();
            if (owner.IsInstance && committed)
            {
                _instanceDocuments.UpdateCollectionValue(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    runtimeContractJsonKey,
                    runtimeContract);
                _onChanged();
                return;
            }

            var current = DesignPreviewTestValues.CollectionItems(preview, collection)
                .Select(CloneObject)
                .ToList();
            if (itemIndex < 0 || itemIndex >= current.Count)
            {
                throw new InvalidOperationException(
                    $"Runtime collection '{collection.Id}' item index {itemIndex} is outside the current collection.");
            }
            current[itemIndex] = CloneObject(item);
            _setPreviewCollectionTestItems(collection.JsonKey, current);
            _testValuesChanged();
        }

        var sections = new List<EditorInternalNavigationSection>();
        var general = new StackPanel { Spacing = 8 };
        foreach (var input in ComponentInputGrouping.OwnInputs(
                     collection.Fields
                         .Where((candidate) => IsVisibleRuntimeValue(owner, candidate))
                         .ToList()))
        {
            general.Children.Add(CreateTestValueCollectionControl(
                owner,
                preview,
                collection,
                itemIndex,
                item,
                input,
                () => { },
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
            EditorSubcardLayout.VerticalCards);
    }

    private Control CreateEmbeddedRuntimeCollectionItemContent(
        RuntimeInputOwner owner,
        JsonObject runtimeContract,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        JsonObject item,
        IReadOnlyList<RuntimeInputCollectionDefinition> childCollections,
        string temporalOwnerId,
        Action<bool> persistRuntimeContract)
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
                Control ChildContent() => CreateEmbeddedChildRuntimeContent(
                    owner,
                    childCollection,
                    childItem,
                    temporalOwnerId,
                    persistRuntimeContract);
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
        Action<bool> persistRuntimeContract)
    {
        var runtimeContract = JsonPath.RequiredObject(
            item,
            RuntimeContractJsonKey(collection),
            $"Runtime collection '{collection.Id}' embedded item");
        var inputs = ComponentPreviewInputSession.ReadRuntimeInputs(
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
        Action<bool> persistRuntimeContract)
    {
        var control = new DictionaryFieldControl(
            new FieldValue(
                RuntimeInputFieldDefinitionFactory.Create(_runtimeInputOptions, owner.Node, input),
                DesignPreviewTestValues.Value(runtimeContract, input)),
            _dictionaryServices.ForNode(
                owner.Node,
                (_) => "",
                openComponentVariantReference: (reference) =>
                {
                    _navigateToNode(reference);
                    return Task.CompletedTask;
                },
                openRuntimeComponentOverrides: _openEmbeddedContext));
        control.ValueChanged += (_, next) =>
        {
            runtimeContract[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, next);
            persistRuntimeContract(false);
        };
        control.ValueCommitted += (_, next) =>
        {
            runtimeContract[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, next);
            persistRuntimeContract(true);
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
        var collectionActions = new StructuredCollectionActions(
            AddFirst: () =>
            {
                var item = DefaultCollectionItem(owner, collection);
                editor!.ActivateOnly(item, items.Count);
                if (owner.IsInstance)
                    _instanceDocuments.AddCollectionItem(owner.Node.Id, StorageCollectionKey(collection), item);
                else
                    _setPreviewCollectionTestItems(collection.JsonKey, [item]);
                Changed();
            },
            AddAfter: (itemIndex) =>
            {
                var currentItem = items[itemIndex];
                var itemId = ItemId(currentItem, itemIndex);
                var next = DefaultCollectionItem(owner, collection);
                editor!.ActivateOnly(next, items.Count);
                if (owner.IsInstance)
                    _instanceDocuments.InsertCollectionItemAfter(owner.Node.Id, StorageCollectionKey(collection), itemId, next);
                else
                {
                    var current = DesignPreviewTestValues.CollectionItems(preview, collection).Select(CloneObject).ToList();
                    current.Insert(Math.Min(itemIndex + 1, current.Count), next);
                    _setPreviewCollectionTestItems(collection.JsonKey, current);
                }
                Changed();
            },
            Duplicate: (itemIndex) =>
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
                editor!.ActivateOnly(copy, items.Count);
                if (owner.IsInstance)
                {
                    _instanceDocuments.DuplicateCollectionItem(
                        owner.Node.Id,
                        StorageCollectionKey(collection),
                        itemId,
                        copy,
                        idMappings);
                }
                else
                {
                    var current = DesignPreviewTestValues.CollectionItems(preview, collection).Select(CloneObject).ToList();
                    current.Insert(itemIndex + 1, copy);
                    _setPreviewCollectionTestItems(collection.JsonKey, current);
                }
                Changed();
            },
            Move: (itemIndex, delta) =>
            {
                var itemId = ItemId(items[itemIndex], itemIndex);
                if (owner.IsInstance)
                    _instanceDocuments.MoveCollectionItem(owner.Node.Id, StorageCollectionKey(collection), itemId, delta);
                else
                    MoveTransientCollectionItem(preview, collection, itemIndex, delta);
                Changed();
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
                    _instanceDocuments.DeleteCollectionItem(owner.Node.Id, StorageCollectionKey(collection), itemId);
                else
                {
                    var current = DesignPreviewTestValues.CollectionItems(preview, collection).Select(CloneObject).ToList();
                    current.RemoveAt(itemIndex);
                    _setPreviewCollectionTestItems(collection.JsonKey, current);
                }
                Changed();
            });
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
                    OpenRuntimeComponentOverrides(owner, preview, collection, itemIndex, item);
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
        var itemActions = actions
            .Where((action) => action.IsCollectionItemAction
                && action.CollectionJsonKey == collection.JsonKey
                && action.CollectionItemId == itemId
                && string.IsNullOrWhiteSpace(action.TargetJsonPath))
            .ToList();
        WrapPanel? actionRow = null;
        var actionControls = new List<(ComponentPreviewActionDefinition Action, RuntimeTestActionControl Control)>();
        void RefreshActionVisibility()
        {
            var currentItem = DesignPreviewTestValues.CollectionItems(preview, collection)
                .ElementAtOrDefault(itemIndex) ?? item;
            foreach (var (action, control) in actionControls)
            {
                control.IsVisible = ComponentPreviewActions.AppliesToItem(action, currentItem);
            }
            if (actionRow is not null)
            {
                actionRow.IsVisible = actionControls.Any((entry) => entry.Control.IsVisible);
            }
        }
        if (!owner.IsInstance && itemActions.Count > 0)
        {
            actionRow = CreateActionPanel();
            foreach (var action in itemActions)
            {
                var control = CreateActionControl(action, collection.Fields, item);
                actionControls.Add((action, control));
                AddActionControl(actionRow, control);
            }
            RefreshActionVisibility();
            content.Children.Add(actionRow);
        }
        var visibleCollectionFields = collection.Fields
            .Where((input) => IsVisibleRuntimeValue(owner, input))
            .ToList();
        foreach (var input in ComponentInputGrouping.OwnInputs(visibleCollectionFields))
        {
            content.Children.Add(CreateTestValueCollectionControl(
                owner,
                preview,
                collection,
                itemIndex,
                item,
                input,
                RefreshActionVisibility,
                openComponentOverrides));
        }

        var groups = ComponentInputGrouping.EmbeddedGroups(visibleCollectionFields);
        var topLevelGroupIds = ComponentInputGrouping.TopLevelGroupIds(groups).ToList();
        var groupSubcards = new List<EditorInternalNavigationSection>();
        foreach (var groupId in topLevelGroupIds)
        {
            groupSubcards.Add(CreateTestValueCollectionGroupSubcard(
                owner, preview, collection, itemIndex, item, groupId, groups, RefreshActionVisibility));
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
                ? _previewInputData.ComponentVariantConfig(componentVariantReference)
                : new JsonObject();
            nestedInputs = ComponentPreviewInputSession.ReadRuntimeInputs(itemRuntimeContract, componentConfig).ToList();
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
                        owner, preview, collection, itemIndex, item, itemRuntimeContract, nestedInput));
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

    private static string ItemId(JsonObject item, int index)
    {
        return JsonPath.RequiredString(item, "id", $"Runtime collection item at index {index}");
    }

    private static IReadOnlyList<JsonObject> DisplayItems(
        JsonObject preview,
        RuntimeInputCollectionDefinition collection)
    {
        var items = DesignPreviewTestValues.CollectionItems(preview, collection);
        return collection.FixedItemCount > 0
            ? items.Take(collection.FixedItemCount).ToList()
            : items;
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
                var options = RuntimeInputFieldDefinitionFactory.Create(_runtimeInputOptions, owner.Node, field).Options ?? [];
                value = ComponentVariantOptionContract.SelectsComponentClass(field.ComponentType)
                    ? ""
                    : ComponentVariantOptionContract.RequireFixedBoundary(
                        options,
                        $"Runtime collection field '{field.Id}'").DefaultVariantReference;
            }
            item[field.JsonKey] = DesignPreviewTestValues.ValueNode(field, value);
        }
        StructuredCollectionItemIdentity.RebaseNestedItems(item, collection);
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
        return item;
    }

    private void OpenRuntimeComponentOverrides(
        RuntimeInputOwner owner,
        JsonObject preview,
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
        void ApplyOverrides(JsonObject nextOverrides)
        {
            item[componentItems.OverridesJsonKey] = nextOverrides.DeepClone();
            var current = DesignPreviewTestValues.CollectionItems(preview, collection).Select(CloneObject).ToList();
            if (itemIndex >= 0 && itemIndex < current.Count)
            {
                current[itemIndex] = CloneObject(item);
                _setPreviewCollectionTestItems(collection.JsonKey, current);
            }
            if (owner.IsInstance)
            {
                _instanceDocuments.UpdateCollectionValue(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    ItemId(item, itemIndex),
                    componentItems.OverridesJsonKey,
                    nextOverrides);
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
        _setPreviewCollectionTestItems(collection.JsonKey, current);
    }

    private Control CreateTestValueCollectionControl(
        RuntimeInputOwner owner,
        JsonObject preview,
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
        var services = _dictionaryServices.ForNode(owner.Node, (fieldId) =>
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
                ? (targetIds) =>
                {
                    var document = new ModuleInstanceAnimationDocument(
                        _instanceDocuments.AnimationJson(owner.Node.Id));
                    foreach (var targetId in targetIds) document.RemoveTarget(targetId);
                    _instanceDocuments.SaveAnimationJson(owner.Node.Id, document.ToJson());
                    _onChanged();
                }
                : null,
            DuplicateStructuredCollectionAnimationTargets = owner.IsInstance
                ? (targetIds) =>
                {
                    var document = new ModuleInstanceAnimationDocument(
                        _instanceDocuments.AnimationJson(owner.Node.Id));
                    document.DuplicateTargets(targetIds);
                    _instanceDocuments.SaveAnimationJson(owner.Node.Id, document.ToJson());
                    _onChanged();
                }
                : null,
        };
        var definition = RuntimeInputFieldDefinitionFactory.Create(
            _runtimeInputOptions,
            owner.Node,
            input,
            CollectionFieldAvailability.AllowsEmpty(item, input));
        if (!string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey))
        {
            definition = definition with { Options = RuntimeInputDynamicOptions.Resolve(_runtimeInputOptions, input, item) };
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
        control.ValueCommitted += (_, next) =>
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
                _instanceDocuments.UpdateCollectionValues(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    updates);
                _onChanged();
            }
            else
            {
                DesignPreviewTestValues.SetCollectionValue(preview, collection, itemIndex, input, next);
            }
            if (selectsComponent || transitioned)
            {
                var current = DesignPreviewTestValues.CollectionItems(preview, collection).Select(CloneObject).ToList();
                if (itemIndex >= 0 && itemIndex < current.Count)
                {
                    current[itemIndex] = CloneObject(item);
                    _setPreviewCollectionTestItems(collection.JsonKey, current);
                }
            }
            else
            {
                _setPreviewCollectionTestValue(collection.JsonKey, itemId, input, next);
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
        return DecorateAnimationToggle(owner, input, targetId, control);
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
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        JsonObject item,
        JsonObject componentInputs,
        ComponentInputDefinition input)
    {
        var control = new DictionaryFieldControl(
            new FieldValue(
                RuntimeInputFieldDefinitionFactory.Create(_runtimeInputOptions, owner.Node, input),
                DesignPreviewTestValues.Value(componentInputs, input)),
            _dictionaryServices.ForNode(
                owner.Node,
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
            var current = DesignPreviewTestValues.CollectionItems(preview, collection).Select(CloneObject).ToList();
            if (itemIndex >= 0 && itemIndex < current.Count)
            {
                current[itemIndex] = CloneObject(item);
                _setPreviewCollectionTestItems(collection.JsonKey, current);
            }
            _testValuesChanged();
        }
        control.ValueChanged += (_, next) => ApplyTransientValue(next);
        control.ValueCommitted += (_, next) =>
        {
            componentInputs[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, next);
            var inputsJsonKey = RuntimeContractJsonKey(collection);
            item[inputsJsonKey] = componentInputs.DeepClone();
            var itemId = ItemId(item, itemIndex);
            if (owner.IsInstance)
            {
                _instanceDocuments.UpdateCollectionValue(
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
        DictionaryFieldControl control)
    {
        if (!owner.IsInstance || input.Animation is null) return control;
        var document = new ModuleInstanceAnimationDocument(
            _instanceDocuments.AnimationJson(owner.Node.Id));
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
            else document.AddTrack(
                input.Id,
                targetId,
                DesignPreviewTestValues.ValueNode(input, control.Value) ?? JsonValue.Create(control.Value)!,
                input.Animation.Interpolations.First());
            _instanceDocuments.SaveAnimationJson(owner.Node.Id, document.ToJson());
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
            _playbackState.Changed += RefreshResolvedValue;
            row.DetachedFromVisualTree += (_, _) => _playbackState.Changed -= RefreshResolvedValue;
            RefreshResolvedValue();
        }
        return row;
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
            content.Children.Add(CreateTestValueCollectionControl(owner, preview, collection, itemIndex, item, input, afterCommit));
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
        EditorSubcardLayout layout)
    {
        var selectedId = _sessionUiState.Selection(stateKey);
        var navigationWidth = _sessionUiState.NavigationWidth(
            stateKey,
            EditorInternalNavigation.DefaultNavigationWidth);
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
            ? action.TargetOptions.Count > 0 ? action.TargetOptions : RuntimeInputDynamicOptions.Resolve(_runtimeInputOptions, targetInput, values)
            : null;
        var currentTargetValue = targetInput is null
            ? ""
            : DesignPreviewTestValues.Value(values, targetInput);
        return new RuntimeTestActionControl(
            action.Label,
            (targetValue) => _triggerAction(action.Id, targetValue),
            () => _restoreAction(action.Id),
            () => _canRestoreAction(action.Id),
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
                ? (_) => { }
                : (json) => _ownerDocuments.SaveDesignPreviewJson(source, json),
            source.IsInstance);
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

    private sealed record RuntimeInputOwner(
        ProjectTreeNode Node,
        string ConfigJson,
        string DesignPreviewJson,
        Action<string> Save,
        bool IsInstance);

    private sealed record RuntimeInputSurface(
        RuntimeInputOwner Owner,
        JsonObject Preview,
        IReadOnlyList<ComponentInputDefinition> Inputs,
        IReadOnlyList<RuntimeInputCollectionDefinition> Collections,
        IReadOnlyList<ComponentPreviewActionDefinition> Actions);
}
