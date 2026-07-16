using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RuntimeInputsCollectionEditor
{
    private readonly SpikeDatabase _database;
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
        _database = database;
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
        var owner = ResolveOwner(node);
        var persistedPreview = DesignPreviewTestValues.Parse(owner.DesignPreviewJson);
        var config = DesignPreviewTestValues.Parse(owner.ConfigJson);
        var preview = RuntimeInputForwardingContract.EffectivePreview(
            _applyTransientTestValues(owner.Node, persistedPreview),
            config);
        var inputs = ComponentPreviewInputSession.ReadRuntimeInputs(preview, config);
        var collections = ComponentPreviewInputSession.ReadRuntimeCollections(preview, config);
        var actions = ComponentPreviewActions.ReadWithEmbedded(_database, preview);
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = owner.IsInstance ? "Runtime Values" : "Test Values", Content = CreateTestValuesTab(owner, preview, persistedPreview, inputs, collections, actions) },
                new TabItem { Header = "Runtime API", Content = CreateApiTab(owner, inputs, collections) },
            },
        };

        var card = new InstantEditorCard(
            EditorCardHeader.Create(
                "Runtime Inputs",
                $"{EditorUiText.Count(inputs.Count, "input")} · {EditorUiText.Count(collections.Count, "collection")}",
                EditorIcons.CreateSemantic("Runtime Inputs", EditorIcons.Design, 18)),
            new Border { Padding = new Thickness(10), Child = tabs },
            isExpanded: false)
        { HorizontalAlignment = HorizontalAlignment.Stretch };
        EditorGroupBlock.ApplyContentSeparator(card);
        return card;
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

        var groups = ComponentInputGrouping.EmbeddedGroups(inputs);
        var sections = new List<EditorInternalNavigationSection>();
        var topLevelGroupIds = ComponentInputGrouping.TopLevelGroupIds(groups).ToList();
        var ownInputs = ComponentInputGrouping.OwnInputs(inputs).ToList();
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
        JsonObject persistedPreview,
        IReadOnlyList<ComponentInputDefinition> inputs,
        IReadOnlyList<RuntimeInputCollectionDefinition> collections,
        IReadOnlyList<ComponentPreviewActionDefinition> actions)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(new TextBlock
        {
            Text = owner.IsInstance ? "Runtime Values" : "Test Values",
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
        panel.Children.Add(header);
        if (!owner.IsInstance)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "These values affect only the current Preview until you choose to save them as defaults.",
                FontSize = 11,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        var rootActions = actions.Where((candidate) => !candidate.IsCollectionItemAction).ToList();
        if (rootActions.Count > 0)
        {
            var actionPanel = CreateActionPanel();
            foreach (var action in rootActions)
            {
                AddActionControl(actionPanel, CreateActionControl(action, inputs, preview));
            }
            panel.Children.Add(actionPanel);
        }
        if (inputs.Count == 0 && collections.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "No test values are required.", Opacity = 0.68 });
            return panel;
        }

        var groups = ComponentInputGrouping.EmbeddedGroups(inputs);
        var sections = new List<EditorInternalNavigationSection>();
        var topLevelGroupIds = ComponentInputGrouping.TopLevelGroupIds(groups).ToList();
        var ownInputs = ComponentInputGrouping.OwnInputs(inputs).ToList();
        if (ownInputs.Count > 0)
        {
            var generalSubcards = new List<EditorInternalNavigationSection>();
            if (owner.IsInstance
                && _animationEditor is not null
                && ownInputs.Any((input) => input.Animation is not null))
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
        foreach (var collection in collections)
        {
            var items = DesignPreviewTestValues.CollectionItems(preview, collection);
            sections.Add(new EditorInternalNavigationSection(
                collection.Id,
                collection.Label,
                $"{items.Count} active {EditorUiText.Noun(items.Count, "instance")}",
                EditorIcons.Component,
                CreateTestValueCollectionContent(owner, preview, collection, actions, items)));
        }
        panel.Children.Add(EditorGroupBlock.CreateSeparator());
        panel.Children.Add(CreateSessionSubcardLayout(
            $"{owner.Node.Id}:test-values",
            sections,
            EditorSubcardLayout.VerticalCards));

        void UpdatePlaybackState()
        {
            panel.IsEnabled = !_playbackState.IsBusy;
        }
        void OnPlaybackStateChanged() => UpdatePlaybackState();
        _playbackState.Changed += OnPlaybackStateChanged;
        panel.DetachedFromVisualTree += (_, _) => _playbackState.Changed -= OnPlaybackStateChanged;
        UpdatePlaybackState();
        return panel;
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
        var control = new DictionaryFieldControl(
            new FieldValue(RuntimeInputFieldDefinitionFactory.Create(_database, owner.Node, input), value),
            _dictionaryServices.ForNode(owner.Node, (_) => ""));
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
                _database.UpdateModuleInstanceRuntimeValue(owner.Node.Id, input.JsonKey, DesignPreviewTestValues.ValueNode(input, next));
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

    private Control CreateTestValueCollectionContent(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        IReadOnlyList<JsonObject> items)
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
                    _database.AddModuleInstanceRuntimeCollectionItem(owner.Node.Id, StorageCollectionKey(collection), item);
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
                    _database.InsertModuleInstanceRuntimeCollectionItemAfter(owner.Node.Id, StorageCollectionKey(collection), itemId, next);
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
                    _database.DuplicateModuleInstanceRuntimeCollectionItem(
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
                    _database.MoveModuleInstanceRuntimeCollectionItem(owner.Node.Id, StorageCollectionKey(collection), itemId, delta);
                else
                    MoveTransientCollectionItem(preview, collection, itemIndex, delta);
                Changed();
            },
            Delete: async (itemIndex) =>
            {
                var item = items[itemIndex];
                var itemId = ItemId(item, itemIndex);
                var label = $"{collection.ItemLabel} {itemIndex + 1}";
                if (!await _confirmCollectionItemDelete(label)) return;
                if (owner.IsInstance)
                    _database.DeleteModuleInstanceRuntimeCollectionItem(owner.Node.Id, StorageCollectionKey(collection), itemId);
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
                return new StructuredCollectionItemContent(content, itemSubcards);
            },
            collectionActions,
            _sessionUiState);
        return editor.Create();
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
        if (itemActions.Count > 0)
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
        foreach (var input in ComponentInputGrouping.OwnInputs(collection.Fields).Where((input) => !input.ActionOnly))
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

        var groups = ComponentInputGrouping.EmbeddedGroups(collection.Fields.Where((input) => !input.ActionOnly).ToList());
        var topLevelGroupIds = ComponentInputGrouping.TopLevelGroupIds(groups).ToList();
        var groupSubcards = new List<EditorInternalNavigationSection>();
        foreach (var groupId in topLevelGroupIds)
        {
            groupSubcards.Add(CreateTestValueCollectionGroupSubcard(
                owner, preview, collection, itemIndex, item, groupId, groups, RefreshActionVisibility));
        }
        var componentItemDefinition = collection.ComponentItems;
        var componentPresetField = componentItemDefinition is null
            ? null
            : collection.Fields.FirstOrDefault((input) => input.JsonKey == componentItemDefinition.PresetJsonKey);
        var componentPresetReference = componentPresetField is null
            ? ""
            : DesignPreviewTestValues.CollectionValue(item, componentPresetField);
        if (!string.IsNullOrWhiteSpace(componentPresetReference)
            && componentItemDefinition is not null
            && item[componentItemDefinition.InputsJsonKey] is JsonObject componentInputs)
        {
            var componentConfig = _database.GetComponentPresetConfig(componentPresetReference);
            var nestedInputs = ComponentPreviewInputSession.ReadRuntimeInputs(componentInputs, componentConfig);
            var nestedActions = actions.Where((action) =>
                    action.IsCollectionItemAction
                    && action.CollectionJsonKey == collection.JsonKey
                    && action.CollectionItemId == itemId
                    && action.TargetJsonPath == componentItemDefinition.InputsJsonKey)
                .ToList();
            if (nestedInputs.Count > 0 || nestedActions.Count > 0)
            {
                var nestedPanel = new StackPanel { Spacing = 6 };
                var applicableNestedActions = nestedActions.Where((action) =>
                        ComponentPreviewActions.AppliesToItem(action, componentInputs))
                    .ToList();
                if (applicableNestedActions.Count > 0)
                {
                    var nestedActionPanel = CreateActionPanel();
                    foreach (var nestedAction in applicableNestedActions)
                    {
                        AddActionControl(nestedActionPanel, CreateActionControl(nestedAction, nestedInputs, componentInputs));
                    }
                    nestedPanel.Children.Add(nestedActionPanel);
                }
                foreach (var nestedInput in nestedInputs)
                {
                    nestedPanel.Children.Add(CreateNestedComponentInputControl(
                        owner, preview, collection, itemIndex, item, componentInputs, nestedInput));
                }
                groupSubcards.Add(new EditorInternalNavigationSection(
                    "componentInputs",
                    "Component inputs",
                    $"{EditorUiText.Count(nestedInputs.Count, "runtime input")} · {EditorUiText.Count(nestedActions.Count, "action")}",
                    EditorIcons.Component,
                    nestedPanel));
            }
        }
        if (owner.IsInstance
            && _animationEditor is not null
            && collection.Fields.Any((input) => input.Animation is not null))
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
        return item["id"] is JsonValue value && value.TryGetValue<string>(out var id)
            ? id
            : $"item-{index}";
    }

    private JsonObject DefaultCollectionItem(RuntimeInputOwner owner, RuntimeInputCollectionDefinition collection)
    {
        var item = new JsonObject { ["id"] = $"{collection.Id}_{Guid.NewGuid():N}" };
        foreach (var field in collection.Fields)
        {
            var value = field.DefaultValue;
            if (field.ValueKind == ValueKind.ComponentPreset && string.IsNullOrWhiteSpace(value))
            {
                var options = RuntimeInputFieldDefinitionFactory.Create(_database, owner.Node, field).Options ?? [];
                var componentId = options.FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.GroupValue))?.GroupValue ?? "";
                value = string.IsNullOrWhiteSpace(componentId)
                    ? ""
                    : options.SingleOrDefault((option) =>
                        option.GroupValue.Equals(componentId, StringComparison.Ordinal)
                        && option.Value.Equals($"{componentId}::preset::default", StringComparison.Ordinal))?.Value
                      ?? throw new InvalidOperationException($"Component '{componentId}' has no explicit default Variant.");
            }
            item[field.JsonKey] = DesignPreviewTestValues.ValueNode(field, value);
        }
        StructuredCollectionItemIdentity.RebaseNestedItems(item, collection);
        var componentItems = collection.ComponentItems;
        var preset = componentItems is null
            ? null
            : collection.Fields.FirstOrDefault((field) => field.JsonKey == componentItems.PresetJsonKey);
        if (preset is not null && componentItems is not null)
        {
            var reference = item[preset.JsonKey]?.GetValue<string>() ?? "";
            item[componentItems.OverridesJsonKey] = new JsonObject();
            item[componentItems.InputsJsonKey] = string.IsNullOrWhiteSpace(reference)
                ? new JsonObject()
                : _database.GetComponentPresetRuntimeInputs(reference);
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
        var presetField = collection.Fields.Single((field) => field.JsonKey == componentItems.PresetJsonKey);
        var presetReference = DesignPreviewTestValues.CollectionValue(item, presetField);
        if (string.IsNullOrWhiteSpace(presetReference)) return;
        var overrides = item[componentItems.OverridesJsonKey] as JsonObject;
        if (overrides is null)
        {
            overrides = new JsonObject();
            item[componentItems.OverridesJsonKey] = overrides;
        }
        var selected = _database.GetComponentPresetSelectionSettings(presetReference);
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
                _database.UpdateModuleInstanceRuntimeCollectionValue(
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
                presetReference,
                selected.ComponentType,
                selected.RecordClassId,
                selected.ConfigJson,
                overrides,
                ApplyOverrides)));
    }

    private static JsonObject CloneObject(JsonObject source) =>
        source.DeepClone() as JsonObject ?? new JsonObject();

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
            && input.JsonKey.Equals(componentItems.PresetJsonKey, StringComparison.Ordinal);
        var hasComponentOverrides = selectsComponent
            && componentItems is not null
            && item[componentItems.OverridesJsonKey] is JsonObject currentOverrides
            && ComponentOverrideCount(currentOverrides) > 0;
        var services = _dictionaryServices.ForNode(owner.Node, (fieldId) =>
        {
            var source = collection.Fields.FirstOrDefault((candidate) => candidate.Id == fieldId);
            return source is null ? "" : DesignPreviewTestValues.CollectionValue(item, source);
        },
        openComponentPresetReference: (reference) =>
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
                        _database.GetModuleInstanceSettings(owner.Node.Id).AnimationJson);
                    foreach (var targetId in targetIds) document.RemoveTarget(targetId);
                    _database.UpdateModuleInstanceAnimationJson(owner.Node.Id, document.ToJson());
                    _onChanged();
                }
                : null,
            DuplicateStructuredCollectionAnimationTargets = owner.IsInstance
                ? (targetIds) =>
                {
                    var document = new ModuleInstanceAnimationDocument(
                        _database.GetModuleInstanceSettings(owner.Node.Id).AnimationJson);
                    document.DuplicateTargets(targetIds);
                    _database.UpdateModuleInstanceAnimationJson(owner.Node.Id, document.ToJson());
                    _onChanged();
                }
                : null,
        };
        var control = new DictionaryFieldControl(
            new FieldValue(
                RuntimeInputFieldDefinitionFactory.Create(_database, owner.Node, input),
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
            if (selectsComponent && componentItems is not null)
            {
                item[componentItems.OverridesJsonKey] = new JsonObject();
                item[componentItems.InputsJsonKey] = string.IsNullOrWhiteSpace(next)
                    ? new JsonObject()
                    : _database.GetComponentPresetRuntimeInputs(next);
            }
            if (owner.IsInstance)
            {
                _database.UpdateModuleInstanceRuntimeCollectionValue(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    input.JsonKey,
                    nextNode);
                if (selectsComponent && componentItems is not null)
                {
                    _database.UpdateModuleInstanceRuntimeCollectionValue(
                        owner.Node.Id,
                        StorageCollectionKey(collection),
                        itemId,
                        componentItems.OverridesJsonKey,
                        item[componentItems.OverridesJsonKey]);
                    _database.UpdateModuleInstanceRuntimeCollectionValue(
                        owner.Node.Id,
                        StorageCollectionKey(collection),
                        itemId,
                        componentItems.InputsJsonKey,
                        item[componentItems.InputsJsonKey]);
                }
                _onChanged();
            }
            else
            {
                DesignPreviewTestValues.SetCollectionValue(preview, collection, itemIndex, input, next);
            }
            if (selectsComponent)
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
                RuntimeInputFieldDefinitionFactory.Create(_database, owner.Node, input),
                DesignPreviewTestValues.Value(componentInputs, input)),
            _dictionaryServices.ForNode(owner.Node, (_) => ""));
        void ApplyTransientValue(string next)
        {
            componentInputs[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, next);
            var inputsJsonKey = collection.ComponentItems?.InputsJsonKey
                ?? throw new InvalidOperationException($"Collection '{collection.Id}' has no component item contract.");
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
            var inputsJsonKey = collection.ComponentItems?.InputsJsonKey
                ?? throw new InvalidOperationException($"Collection '{collection.Id}' has no component item contract.");
            item[inputsJsonKey] = componentInputs.DeepClone();
            var itemId = ItemId(item, itemIndex);
            if (owner.IsInstance)
            {
                _database.UpdateModuleInstanceRuntimeCollectionValue(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    inputsJsonKey,
                    componentInputs);
                _onChanged();
            }
        };
        return control;
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
            _database.GetModuleInstanceSettings(owner.Node.Id).AnimationJson);
        var active = document.HasTrack(input.Id, targetId);
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
            _database.UpdateModuleInstanceAnimationJson(owner.Node.Id, document.ToJson());
            _onChanged();
            _reloadAndSelect?.Invoke(owner.Node);
        };
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 2,
        };
        row.Children.Add(toggle);
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
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
            ? action.TargetOptions.Count > 0 ? action.TargetOptions : DynamicOptions(targetInput, values)
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

    private IReadOnlyList<FieldOption>? DynamicOptions(ComponentInputDefinition? input, JsonObject values)
    {
        if (input is null) return null;
        if (string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey)) return input.Options;
        if (values[input.OptionsSourceCollectionJsonKey] is not JsonArray items) return [];
        return items.OfType<JsonObject>().Select((item, index) =>
        {
            var value = item[input.OptionsSourceValueJsonKey]?.GetValue<string>() ?? "";
            var rawLabel = string.IsNullOrWhiteSpace(input.OptionsSourceLabelJsonKey)
                ? ""
                : item[input.OptionsSourceLabelJsonKey]?.GetValue<string>() ?? "";
            var label = rawLabel;
            if (input.OptionsSourceLabelJsonKey.Equals("presetId", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(rawLabel))
            {
                try { label = _database.GetRuntimeComponentPresetName(rawLabel, new JsonObject(), []); }
                catch { label = rawLabel; }
            }
            if (string.IsNullOrWhiteSpace(label)) label = $"State {index + 1}";
            return new FieldOption(value, label);
        }).Where((option) => !string.IsNullOrWhiteSpace(option.Value)).ToList();
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
        if (node.Kind == ProjectTreeNodeKind.Module)
        {
            var settings = _database.GetModuleSettings(node.Id);
            return new RuntimeInputOwner(node, settings.ConfigJson, settings.DesignPreviewJson,
                (json) => _database.UpdateModuleDesignPreviewJson(node.Id, json), false);
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleVariant)
        {
            var settings = _database.GetModuleVariantSettings(node);
            return new RuntimeInputOwner(node, settings.ConfigJson, settings.DesignPreviewJson,
                (json) => _database.UpdateModuleDesignPreviewJson(node.Parent?.Id
                    ?? throw new InvalidOperationException("Module variant has no parent module."), json), false);
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentPreset && node.Parent is not null)
        {
            var settings = _database.GetComponentPresetSettings(node);
            return new RuntimeInputOwner(node, settings.ConfigJson, settings.DesignPreviewJson,
                (json) => _database.UpdateComponentClassDesignPreviewJson(node.Parent.Id, json), false);
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleInstance)
        {
            var instance = _database.GetModuleInstanceSettings(node.Id);
            var module = _database.GetModuleInstanceVariantSettings(node.Id);
            return new RuntimeInputOwner(
                node,
                module.ConfigJson,
                _database.GetModuleInstanceRuntimePreviewJson(node.Id),
                (_) => { },
                true);
        }

        throw new InvalidOperationException($"Runtime inputs are not supported by '{node.Kind}'.");
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
        string.IsNullOrWhiteSpace(collection.SourceCollectionJsonKey)
            ? collection.JsonKey
            : collection.SourceCollectionJsonKey;

    private sealed record RuntimeInputOwner(
        ProjectTreeNode Node,
        string ConfigJson,
        string DesignPreviewJson,
        Action<string> Save,
        bool IsInstance);
}
