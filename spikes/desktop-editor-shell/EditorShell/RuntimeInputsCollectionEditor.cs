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
    private readonly Action<string> _triggerAction;
    private readonly Action<string, string> _setPreviewTestValue;
    private readonly Action<string, string, ComponentInputDefinition, string> _setPreviewCollectionTestValue;
    private readonly Func<JsonObject, JsonObject> _applyTransientTestValues;
    private readonly Func<bool> _resetTestValues;
    private readonly Func<string, IReadOnlyList<string>, Task<bool>> _confirmSaveDefaults;
    private readonly Func<string, Task<bool>> _confirmCollectionItemDelete;
    private readonly Func<string, Task<bool>> _confirmAnimationDisable;
    private readonly PreviewPlaybackState _playbackState;
    private readonly Action<ProjectTreeNode>? _reloadAndSelect;
    private readonly EditorSessionUiState _sessionUiState;
    private readonly ModuleInstanceAnimationEditor? _animationEditor;
    private Action _testValuesChanged = () => { };

    public RuntimeInputsCollectionEditor(
        SpikeDatabase database,
        EditorDictionaryFieldServices dictionaryServices,
        Action onChanged,
        Action<string> triggerAction,
        Action<string, string> setPreviewTestValue,
        Action<string, string, ComponentInputDefinition, string> setPreviewCollectionTestValue,
        Func<JsonObject, JsonObject> applyTransientTestValues,
        Func<bool> resetTestValues,
        Func<string, IReadOnlyList<string>, Task<bool>> confirmSaveDefaults,
        Func<string, Task<bool>> confirmCollectionItemDelete,
        Func<string, Task<bool>> confirmAnimationDisable,
        PreviewPlaybackState playbackState,
        EditorSessionUiState sessionUiState,
        ModuleInstanceAnimationEditor? animationEditor = null,
        Action<ProjectTreeNode>? reloadAndSelect = null)
    {
        _database = database;
        _dictionaryServices = dictionaryServices;
        _onChanged = onChanged;
        _triggerAction = triggerAction;
        _setPreviewTestValue = setPreviewTestValue;
        _setPreviewCollectionTestValue = setPreviewCollectionTestValue;
        _applyTransientTestValues = applyTransientTestValues;
        _resetTestValues = resetTestValues;
        _confirmSaveDefaults = confirmSaveDefaults;
        _confirmCollectionItemDelete = confirmCollectionItemDelete;
        _confirmAnimationDisable = confirmAnimationDisable;
        _playbackState = playbackState;
        _sessionUiState = sessionUiState;
        _animationEditor = animationEditor;
        _reloadAndSelect = reloadAndSelect;
    }

    public InstantEditorCard Create(ProjectTreeNode node)
    {
        var owner = ResolveOwner(node);
        var persistedPreview = DesignPreviewTestValues.Parse(owner.DesignPreviewJson);
        var preview = _applyTransientTestValues(persistedPreview);
        var config = DesignPreviewTestValues.Parse(owner.ConfigJson);
        var inputs = ComponentPreviewInputSession.ReadRuntimeInputs(preview, config);
        var collections = ComponentPreviewInputSession.ReadRuntimeCollections(preview, config);
        var actions = ComponentPreviewActions.Read(preview);
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
                if (!_resetTestValues()) return;
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
                var current = _applyTransientTestValues(DesignPreviewTestValues.Parse(owner.DesignPreviewJson));
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
                var current = _applyTransientTestValues(DesignPreviewTestValues.Parse(owner.DesignPreviewJson));
                var differences = DesignPreviewTestValues.Differences(current, DesignPreviewTestValues.Parse(owner.DesignPreviewJson), inputs, collections);
                if (differences.Count == 0 || !await _confirmSaveDefaults(owner.Node.Name, differences.Select((difference) => difference.Label).ToList())) return;
                DesignPreviewTestValues.PromoteToDefaults(current, inputs, collections);
                owner.Save(current.ToJsonString());
                _resetTestValues();
                _onChanged();
            };
            buttons.Children.Add(saveDefaults);
            RefreshSaveState();
        }
        foreach (var action in actions.Where((candidate) => !candidate.IsCollectionItemAction))
        {
            var button = new Button
            {
                MinWidth = 92,
                Content = CreateActionContent(action),
            };
            ToolTip.SetTip(button, action.Label);
            button.Click += (_, args) =>
            {
                args.Handled = true;
                _triggerAction(action.Id);
            };
            buttons.Children.Add(button);
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
        };
        return DecorateAnimationToggle(owner, input, "", control);
    }

    private Control CreateTestValueCollectionContent(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        IReadOnlyList<JsonObject> items)
    {
        var footer = new StackPanel { Spacing = 8 };
        if (items.Count == 0)
        {
            footer.Children.Add(new TextBlock { Text = "No active instances in this design.", Opacity = 0.68 });
        }

        var subcards = new List<EditorInternalNavigationSection>();
        for (var index = 0; index < items.Count; index++)
        {
            var itemId = ItemId(items[index], index);
            var expansionKey = $"{owner.Node.Id}:{collection.Id}:{itemId}:expanded";
            var navigationKey = $"{owner.Node.Id}:{collection.Id}:{itemId}:vertical-card";
            var isExpanded = _sessionUiState.IsExpanded(expansionKey);
            var selectedSubcardId = _sessionUiState.Selection(navigationKey);
            var itemContent = CreateTestValueCollectionItemContent(
                owner,
                preview,
                collection,
                actions,
                index,
                items[index],
                out var trailing,
                out var itemSubcards);
            var presentation = RuntimeCollectionItemPresentation.Resolve(
                collection,
                items[index],
                $"Payload item {index + 1}",
                EditorIcons.Component);
            subcards.Add(new EditorInternalNavigationSection(
                itemId,
                $"{collection.ItemLabel} {index + 1}",
                presentation.Subtitle,
                presentation.Icon,
                itemContent,
                trailing,
                itemSubcards,
                EditorSubcardLayout.VerticalCards,
                isExpanded,
                (next) => _sessionUiState.SetExpanded(expansionKey, next),
                selectedSubcardId,
                (next) => _sessionUiState.Select(navigationKey, next)));
        }

        if (owner.IsInstance && items.Count == 0)
        {
            var add = EditorCollectionItemControls.CreateAddButton($"Add {collection.ItemLabel.ToLowerInvariant()}");
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Click += (_, _) =>
            {
                var item = new JsonObject { ["id"] = $"{collection.Id}_{Guid.NewGuid():N}" };
                foreach (var field in collection.Fields)
                {
                    item[field.JsonKey] = DesignPreviewTestValues.ValueNode(field, field.DefaultValue);
                }
                _database.AddModuleInstanceRuntimeCollectionItem(owner.Node.Id, StorageCollectionKey(collection), item);
                _onChanged();
                _reloadAndSelect?.Invoke(owner.Node);
            };
            footer.Children.Add(add);
        }

        var content = new StackPanel { Spacing = EditorUiDensity.Card(8) };
        content.Children.Add(new EditorSubcardLayoutHost(subcards, EditorSubcardLayout.FlatStack));
        if (footer.Children.Count > 0)
        {
            content.Children.Add(footer);
        }
        return content;
    }

    private Control CreateTestValueCollectionItemContent(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        int itemIndex,
        JsonObject item,
        out Control? trailing,
        out IReadOnlyList<EditorInternalNavigationSection> subcards)
    {
        var content = new StackPanel { Spacing = 8 };
        var itemId = item["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var id)
            ? id
            : "";
        var itemActions = actions
            .Where((action) => action.IsCollectionItemAction
                && action.CollectionJsonKey == collection.JsonKey
                && action.CollectionItemId == itemId)
            .ToList();
        trailing = null;
        if (owner.IsInstance)
        {
            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
            };
            var add = EditorCollectionItemControls.CreateAddButton($"Add {collection.ItemLabel.ToLowerInvariant()} after this item");
            add.Click += (_, args) =>
            {
                args.Handled = true;
                var next = new JsonObject { ["id"] = $"{collection.Id}_{Guid.NewGuid():N}" };
                foreach (var field in collection.Fields)
                {
                    next[field.JsonKey] = DesignPreviewTestValues.ValueNode(field, field.DefaultValue);
                }
                _database.InsertModuleInstanceRuntimeCollectionItemAfter(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    next);
                _onChanged();
                _reloadAndSelect?.Invoke(owner.Node);
            };
            controls.Children.Add(add);
            var duplicate = EditorCollectionItemControls.CreateDuplicateButton($"Duplicate {collection.ItemLabel.ToLowerInvariant()}");
            duplicate.Click += (_, args) =>
            {
                args.Handled = true;
                _database.DuplicateModuleInstanceRuntimeCollectionItem(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    $"{collection.Id}_{Guid.NewGuid():N}");
                _onChanged();
                _reloadAndSelect?.Invoke(owner.Node);
            };
            controls.Children.Add(duplicate);
            var moveUp = EditorCollectionItemControls.CreateMoveButton(up: true, enabled: itemIndex > 0);
            moveUp.Click += (_, args) =>
            {
                args.Handled = true;
                _database.MoveModuleInstanceRuntimeCollectionItem(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    -1);
                _onChanged();
                _reloadAndSelect?.Invoke(owner.Node);
            };
            controls.Children.Add(moveUp);
            var moveDown = EditorCollectionItemControls.CreateMoveButton(
                up: false,
                enabled: itemIndex < DesignPreviewTestValues.CollectionItems(preview, collection).Count - 1);
            moveDown.Click += (_, args) =>
            {
                args.Handled = true;
                _database.MoveModuleInstanceRuntimeCollectionItem(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    1);
                _onChanged();
                _reloadAndSelect?.Invoke(owner.Node);
            };
            controls.Children.Add(moveDown);
            var delete = EditorCollectionItemControls.CreateDeleteButton();
            delete.Click += async (_, args) =>
            {
                args.Handled = true;
                var label = $"{collection.ItemLabel} {itemIndex + 1}";
                if (!await _confirmCollectionItemDelete(label)) return;
                _database.DeleteModuleInstanceRuntimeCollectionItem(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId);
                _onChanged();
                _reloadAndSelect?.Invoke(owner.Node);
            };
            controls.Children.Add(delete);
            trailing = controls;
        }
        StackPanel? actionRow = null;
        var actionButtons = new List<(ComponentPreviewActionDefinition Action, Button Button)>();
        void RefreshActionVisibility()
        {
            var currentItem = DesignPreviewTestValues.CollectionItems(preview, collection)
                .ElementAtOrDefault(itemIndex) ?? item;
            foreach (var (action, button) in actionButtons)
            {
                button.IsVisible = ComponentPreviewActions.AppliesToItem(action, currentItem);
            }
            if (actionRow is not null)
            {
                actionRow.IsVisible = actionButtons.Any((entry) => entry.Button.IsVisible);
            }
        }
        if (itemActions.Count > 0)
        {
            actionRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
            };
            foreach (var action in itemActions)
            {
                var button = new Button
                {
                    MinWidth = 128,
                    Height = 34,
                    FontWeight = FontWeight.SemiBold,
                    Background = EditorSukiWindowTheme.AccentBrush(0x24),
                    BorderBrush = EditorSukiWindowTheme.AccentBrush(0x80),
                    BorderThickness = new Thickness(1),
                    Content = CreateActionContent(action),
                };
                ToolTip.SetTip(button, action.Label);
                button.Click += (_, args) =>
                {
                    args.Handled = true;
                    _triggerAction(action.Id);
                };
                actionButtons.Add((action, button));
                actionRow.Children.Add(button);
            }
            RefreshActionVisibility();
            content.Children.Add(actionRow);
        }
        foreach (var input in ComponentInputGrouping.OwnInputs(collection.Fields))
        {
            content.Children.Add(CreateTestValueCollectionControl(owner, preview, collection, itemIndex, item, input, RefreshActionVisibility));
        }

        var groups = ComponentInputGrouping.EmbeddedGroups(collection.Fields);
        var topLevelGroupIds = ComponentInputGrouping.TopLevelGroupIds(groups).ToList();
        var groupSubcards = new List<EditorInternalNavigationSection>();
        foreach (var groupId in topLevelGroupIds)
        {
            groupSubcards.Add(CreateTestValueCollectionGroupSubcard(
                owner, preview, collection, itemIndex, item, groupId, groups, RefreshActionVisibility));
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

    private Control CreateTestValueCollectionControl(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        JsonObject item,
        ComponentInputDefinition input,
        Action? afterCommit = null)
    {
        var control = new DictionaryFieldControl(
            new FieldValue(RuntimeInputFieldDefinitionFactory.Create(_database, owner.Node, input), DesignPreviewTestValues.CollectionValue(item, input)),
            _dictionaryServices.ForNode(owner.Node, (fieldId) =>
            {
                var source = collection.Fields.FirstOrDefault((candidate) => candidate.Id == fieldId);
                return source is null ? "" : DesignPreviewTestValues.CollectionValue(item, source);
            }));
        control.IsEnabled = CollectionFieldIsEnabled(item, input);
        control.ValueCommitted += (_, next) =>
        {
            var itemId = item["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var id)
                ? id
                : "";
            var nextNode = DesignPreviewTestValues.ValueNode(input, next);
            item[input.JsonKey] = nextNode?.DeepClone();
            if (owner.IsInstance)
            {
                _database.UpdateModuleInstanceRuntimeCollectionValue(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId,
                    input.JsonKey,
                    nextNode);
                _onChanged();
            }
            else
            {
                DesignPreviewTestValues.SetCollectionValue(preview, collection, itemIndex, input, next);
            }
            _setPreviewCollectionTestValue(collection.JsonKey, itemId, input, next);
            _testValuesChanged();
            afterCommit?.Invoke();
            if (collection.Fields.Any((candidate) =>
                    candidate.EnabledWhenItemJsonKey.Equals(input.JsonKey, StringComparison.Ordinal)
                    || candidate.BehaviorTiming?.SourceFieldId.Equals(input.Id, StringComparison.Ordinal) == true))
            {
                _reloadAndSelect?.Invoke(owner.Node);
            }
        };
        var targetId = item["id"]?.GetValue<string>() ?? "";
        return DecorateAnimationToggle(owner, input, targetId, control);
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

    private static bool CollectionFieldIsEnabled(JsonObject item, ComponentInputDefinition input)
    {
        if (string.IsNullOrWhiteSpace(input.EnabledWhenItemJsonKey)
            || input.EnabledWhenItemValues is not { Count: > 0 })
        {
            return true;
        }

        var current = item[input.EnabledWhenItemJsonKey] is JsonValue value
            && value.TryGetValue<string>(out var text)
            ? text
            : "";
        return input.EnabledWhenItemValues.Contains(current, StringComparer.Ordinal);
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
        return new EditorSubcardLayoutHost(
            sections,
            layout,
            selectedId,
            (next) => _sessionUiState.Select(stateKey, next));
    }

    private static Control CreateActionContent(ComponentPreviewActionDefinition action)
    {
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
        };
        content.Children.Add(EditorIcons.CreateSemantic(action.Label, EditorIcons.Play, 12));
        var label = new TextBlock
        {
            Text = action.Label,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);
        content.Children.Add(label);
        return content;
    }

    private RuntimeInputOwner ResolveOwner(ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.Module)
        {
            var settings = _database.GetModuleSettings(node.Id);
            return new RuntimeInputOwner(node, settings.ConfigJson, settings.DesignPreviewJson,
                (json) => _database.UpdateModuleDesignPreviewJson(node.Id, json), false);
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
            var module = _database.GetModuleSettings(instance.ModuleId);
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
