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
    private readonly PreviewPlaybackState _playbackState;
    private readonly Action<ProjectTreeNode>? _reloadAndSelect;
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
        PreviewPlaybackState playbackState,
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
        _playbackState = playbackState;
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
                new TabItem { Header = "Runtime API", Content = CreateApiTab(inputs, collections) },
            },
        };

        return new InstantEditorCard(
            EditorCardHeader.Create(
                "Runtime Inputs",
                $"{EditorUiText.Count(inputs.Count, "input")} · {EditorUiText.Count(collections.Count, "collection")}",
                EditorIcons.CreateSemantic("Runtime Inputs", EditorIcons.Design, 18)),
            new Border { Padding = new Thickness(10), Child = tabs },
            isExpanded: false)
        { HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    private Control CreateApiTab(
        IReadOnlyList<ComponentInputDefinition> inputs,
        IReadOnlyList<RuntimeInputCollectionDefinition> collections)
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        if (inputs.Count == 0 && collections.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "This definition exposes no runtime inputs.", Opacity = 0.68 });
            return panel;
        }

        foreach (var input in ComponentInputGrouping.OwnInputs(inputs))
        {
            panel.Children.Add(CreateApiInputRow(input));
        }

        var groups = ComponentInputGrouping.EmbeddedGroups(inputs);
        var groupCards = new List<InstantEditorCard>();
        foreach (var groupId in ComponentInputGrouping.TopLevelGroupIds(groups))
        {
            panel.Children.Add(CreateApiGroupCard(groupId, groups, groupCards));
        }
        foreach (var collection in collections)
        {
            panel.Children.Add(CreateApiCollectionCard(collection, out var card));
            groupCards.Add(card);
        }
        EditorGroupBlock.WireExclusiveCards(groupCards);

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
            ToolTip.SetTip(reset, "Descarta los cambios temporales de este Preview.");
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

        foreach (var input in ComponentInputGrouping.OwnInputs(inputs))
        {
            panel.Children.Add(CreateTestValueControl(owner, preview, input));
        }

        var groups = ComponentInputGrouping.EmbeddedGroups(inputs);
        var groupCards = new List<InstantEditorCard>();
        foreach (var groupId in ComponentInputGrouping.TopLevelGroupIds(groups))
        {
            panel.Children.Add(CreateTestValueGroupCard(owner, preview, groupId, groups, groupCards));
        }
        foreach (var collection in collections)
        {
            panel.Children.Add(CreateTestValueCollectionCard(owner, preview, collection, actions, out var card));
            groupCards.Add(card);
        }
        EditorGroupBlock.WireExclusiveCards(groupCards);

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

    private Control CreateApiCollectionCard(
        RuntimeInputCollectionDefinition collection,
        out InstantEditorCard card)
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
        return EditorGroupBlock.CreateNestedCard(
            EditorCardHeader.Create(collection.Label, $"{collection.ItemLabel} contract", EditorIcons.CreateSemantic(collection.Label, EditorIcons.Component, 16)),
            content,
            out card,
            isExpanded: false);
    }

    private Control CreateApiGroupCard(
        string groupId,
        IReadOnlyDictionary<string, List<ComponentInputDefinition>> groups,
        List<InstantEditorCard> siblingCards)
    {
        var groupInputs = groups[groupId];
        var content = new StackPanel { Spacing = 6 };
        foreach (var input in groupInputs)
        {
            content.Children.Add(CreateApiInputRow(input));
        }
        var childCards = new List<InstantEditorCard>();
        foreach (var childId in ComponentInputGrouping.ChildGroupIds(groupId, groups))
        {
            content.Children.Add(CreateApiGroupCard(childId, groups, childCards));
        }
        EditorGroupBlock.WireExclusiveCards(childCards);
        return CreateEmbeddedCard(ComponentInputGrouping.GroupLabel(groupInputs), content, siblingCards);
    }

    private Control CreateTestValueControl(
        RuntimeInputOwner owner,
        JsonObject preview,
        ComponentInputDefinition input)
    {
        var value = DesignPreviewTestValues.Value(preview, input);
        var control = new DictionaryFieldControl(
            new FieldValue(CreateDefinition(owner.Node, input), value),
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
        return control;
    }

    private Control CreateTestValueCollectionCard(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        out InstantEditorCard card)
    {
        var content = new StackPanel { Spacing = 8 };
        var items = DesignPreviewTestValues.CollectionItems(preview, collection);
        if (items.Count == 0)
        {
            content.Children.Add(new TextBlock { Text = "No active instances in this design.", Opacity = 0.68 });
        }

        var itemCards = new List<InstantEditorCard>();
        for (var index = 0; index < items.Count; index++)
        {
            content.Children.Add(CreateTestValueCollectionItemCard(owner, preview, collection, actions, index, items[index], out var itemCard));
            itemCards.Add(itemCard);
        }
        EditorGroupBlock.WireExclusiveCards(itemCards);

        if (owner.IsInstance)
        {
            var add = new Button { Content = $"Add {collection.ItemLabel.ToLowerInvariant()}", HorizontalAlignment = HorizontalAlignment.Left };
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
            content.Children.Add(add);
        }

        return EditorGroupBlock.CreateNestedCard(
            EditorCardHeader.Create(
                collection.Label,
                $"{items.Count} active {collection.ItemLabel.ToLowerInvariant()} {EditorUiText.Noun(items.Count, "instance")}",
                EditorIcons.CreateSemantic(collection.Label, EditorIcons.Component, 16)),
            content,
            out card,
            isExpanded: true);
    }

    private Control CreateTestValueCollectionItemCard(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<ComponentPreviewActionDefinition> actions,
        int itemIndex,
        JsonObject item,
        out InstantEditorCard card)
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
        Button? delete = null;
        if (owner.IsInstance)
        {
            delete = new Button
            {
                Content = EditorIcons.Create(EditorIcons.Delete, 14),
                Width = 30,
                Height = 28,
                Padding = new Thickness(0),
            };
            delete.Click += (_, _) =>
            {
                _database.DeleteModuleInstanceRuntimeCollectionItem(
                    owner.Node.Id,
                    StorageCollectionKey(collection),
                    itemId);
                _onChanged();
                _reloadAndSelect?.Invoke(owner.Node);
            };
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
        var groupCards = new List<InstantEditorCard>();
        foreach (var groupId in ComponentInputGrouping.TopLevelGroupIds(groups))
        {
            content.Children.Add(CreateTestValueCollectionGroupCard(
                owner, preview, collection, itemIndex, item, groupId, groups, groupCards, RefreshActionVisibility));
        }
        EditorGroupBlock.WireExclusiveCards(groupCards);

        return EditorGroupBlock.CreateNestedCard(
            EditorCardHeader.Create($"{collection.ItemLabel} {itemIndex + 1}", $"Payload item {itemIndex + 1}", EditorIcons.CreateSemantic(collection.ItemLabel, EditorIcons.Component, 14)),
            content,
            out card,
            isExpanded: true,
            headerTrailing: delete);
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
            new FieldValue(CreateDefinition(owner.Node, input), DesignPreviewTestValues.CollectionValue(item, input)),
            _dictionaryServices.ForNode(owner.Node, (_) => ""));
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
                    candidate.EnabledWhenItemJsonKey.Equals(input.JsonKey, StringComparison.Ordinal)))
            {
                _reloadAndSelect?.Invoke(owner.Node);
            }
        };
        return control;
    }

    private Control CreateTestValueCollectionGroupCard(
        RuntimeInputOwner owner,
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        JsonObject item,
        string groupId,
        IReadOnlyDictionary<string, List<ComponentInputDefinition>> groups,
        List<InstantEditorCard> siblingCards,
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

        var childCards = new List<InstantEditorCard>();
        foreach (var childId in ComponentInputGrouping.ChildGroupIds(groupId, groups))
        {
            content.Children.Add(CreateTestValueCollectionGroupCard(
                owner, preview, collection, itemIndex, item, childId, groups, childCards, afterCommit));
        }
        EditorGroupBlock.WireExclusiveCards(childCards);
        var cardSurface = EditorGroupBlock.CreateCollapsible(
            EditorCardHeader.Create(ComponentInputGrouping.GroupLabel(groupInputs), "Runtime inputs", EditorIcons.CreateSemantic(ComponentInputGrouping.GroupLabel(groupInputs), EditorIcons.Component, 14)),
            content,
            out var card,
            isExpanded: false);
        siblingCards.Add(card);
        return cardSurface;
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

    private Control CreateTestValueGroupCard(
        RuntimeInputOwner owner,
        JsonObject preview,
        string groupId,
        IReadOnlyDictionary<string, List<ComponentInputDefinition>> groups,
        List<InstantEditorCard> siblingCards)
    {
        var groupInputs = groups[groupId];
        var content = new StackPanel { Spacing = 8 };
        foreach (var input in groupInputs)
        {
            content.Children.Add(CreateTestValueControl(owner, preview, input));
        }
        var childCards = new List<InstantEditorCard>();
        foreach (var childId in ComponentInputGrouping.ChildGroupIds(groupId, groups))
        {
            content.Children.Add(CreateTestValueGroupCard(owner, preview, childId, groups, childCards));
        }
        EditorGroupBlock.WireExclusiveCards(childCards);
        return CreateEmbeddedCard(ComponentInputGrouping.GroupLabel(groupInputs), content, siblingCards);
    }

    private static Control CreateEmbeddedCard(
        string label,
        Control content,
        List<InstantEditorCard> siblingCards)
    {
        var card = EditorGroupBlock.CreateCollapsible(
            EditorCardHeader.Create(label, "Embedded component inputs", EditorIcons.CreateSemantic(label, EditorIcons.Component, 16)),
            content,
            out var groupCard);
        siblingCards.Add(groupCard);
        return card;
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

    private FieldDefinition CreateDefinition(ProjectTreeNode node, ComponentInputDefinition input)
    {
        var projectId = ProjectAncestor(node).Id;
        var options = input.ValueKind switch
        {
            ValueKind.RecordReference when input.TableId == "actors" => _database.GetActorOptions(projectId),
            ValueKind.ComponentPreset when !string.IsNullOrWhiteSpace(input.ComponentType) =>
                _database.GetComponentPresetReferenceOptionsByType(projectId, input.ComponentType),
            ValueKind.PaletteColorToken => _database.GetPaletteColorOptions(projectId),
            _ => input.Options,
        };
        return new FieldDefinition(
            input.Id,
            input.Label,
            input.ValueKind,
            DefaultValue: input.DefaultValue,
            Options: options,
            PairLabels: input.ValueKind == ValueKind.IntegerPair ? input.PairLabels : null,
            Number: input.ValueKind is ValueKind.Decimal or ValueKind.Integer or ValueKind.Alpha
                ? new NumberDefinition(input.Minimum, input.Maximum, input.Increment, input.ValueKind == ValueKind.Integer ? 0 : 2)
                : null,
            RecordReference: input.ValueKind == ValueKind.RecordReference
                ? new RecordReferenceDefinition(input.TableId)
                : null,
            Unit: input.Unit);
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
