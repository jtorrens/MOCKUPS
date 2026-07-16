using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorLayoutCardFactory
{
    private readonly EditorFieldValueRouter _fieldValues;
    private readonly ComponentClassFieldValueService _componentClassFieldValues;
    private readonly IEditorInlinePreviewController _inlinePreviews;
    private readonly EditorDictionaryFieldServices _dictionaryFieldServices;
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator;
    private readonly EditorActiveFieldControls _activeFieldControls;
    private readonly IEditorShellMessageSink _messages;
    private readonly Func<ProjectTreeNode, string, Task> _openEmbeddedComponentEditor;
    private readonly Func<ProjectTreeNode, EmbeddedComponentSlotDefinition, Task> _openEmbeddedComponentSlotEditor;
    private readonly Func<EditorEmbeddedContext, string, Task> _openNestedEmbeddedComponentEditor;
    private readonly Func<EditorEmbeddedContext, EmbeddedComponentSlotDefinition, Task> _openNestedEmbeddedComponentSlotEditor;
    private readonly Func<string, Task> _openComponentPresetReference;
    private readonly Func<ProjectTreeNode, Task> _toggleVariantLock;
    private readonly Action<EditorEmbeddedContext> _openRuntimeComponentOverrides;
    private readonly Action<ProjectTreeNode> _scheduleActiveEditorReload;
    private readonly Action _refreshPreview;
    private readonly Dictionary<string, string> _groupNavigationSelections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _groupNavigationWidths = new(StringComparer.Ordinal);

    public EditorLayoutCardFactory(
        EditorFieldValueRouter fieldValues,
        ComponentClassFieldValueService componentClassFieldValues,
        IEditorInlinePreviewController inlinePreviews,
        EditorDictionaryFieldServices dictionaryFieldServices,
        EditorFieldCommitCoordinator fieldCommitCoordinator,
        EditorActiveFieldControls activeFieldControls,
        IEditorShellMessageSink messages,
        Func<ProjectTreeNode, string, Task> openEmbeddedComponentEditor,
        Func<ProjectTreeNode, EmbeddedComponentSlotDefinition, Task> openEmbeddedComponentSlotEditor,
        Func<EditorEmbeddedContext, string, Task> openNestedEmbeddedComponentEditor,
        Func<EditorEmbeddedContext, EmbeddedComponentSlotDefinition, Task> openNestedEmbeddedComponentSlotEditor,
        Func<string, Task> openComponentPresetReference,
        Func<ProjectTreeNode, Task> toggleVariantLock,
        Action<EditorEmbeddedContext> openRuntimeComponentOverrides,
        Action<ProjectTreeNode> scheduleActiveEditorReload,
        Action refreshPreview)
    {
        _fieldValues = fieldValues;
        _componentClassFieldValues = componentClassFieldValues;
        _inlinePreviews = inlinePreviews;
        _dictionaryFieldServices = dictionaryFieldServices;
        _fieldCommitCoordinator = fieldCommitCoordinator;
        _activeFieldControls = activeFieldControls;
        _messages = messages;
        _openEmbeddedComponentEditor = openEmbeddedComponentEditor;
        _openEmbeddedComponentSlotEditor = openEmbeddedComponentSlotEditor;
        _openNestedEmbeddedComponentEditor = openNestedEmbeddedComponentEditor;
        _openNestedEmbeddedComponentSlotEditor = openNestedEmbeddedComponentSlotEditor;
        _openComponentPresetReference = openComponentPresetReference;
        _toggleVariantLock = toggleVariantLock;
        _openRuntimeComponentOverrides = openRuntimeComponentOverrides;
        _scheduleActiveEditorReload = scheduleActiveEditorReload;
        _refreshPreview = refreshPreview;
    }

    public InstantEditorCard Create(
        ProjectTreeNode node,
        EditorLayoutCard layoutCard,
        EditorSimplifiedProjectionState? simplifiedProjection = null)
    {
        var body = new StackPanel
        {
            Spacing = EditorUiDensity.Card(12),
        };
        var controls = new List<DictionaryFieldControl>();
        var headerIcon = EditorIcons.CreateSemantic(layoutCard.Label, layoutCard.Icon, 18);
        var visibleGroups = layoutCard.VisibleGroups.ToList();
        var groupLayout = ParseGroupLayout(layoutCard.GroupLayout);
        var useSectionChrome = visibleGroups.Count > 1;
        var exclusiveGroupCards = new List<InstantEditorCard>();
        var organizedGroups = new List<(EditorLayoutGroup Group, Control Content, EditorSubcardLayout Layout)>();

        foreach (var group in visibleGroups)
        {
            var groupControls = new List<DictionaryFieldControl>();
            var groupPanel = new StackPanel
            {
                Spacing = EditorUiDensity.Card(12),
            };

            _inlinePreviews.AddIfNeeded(node, layoutCard, groupPanel);

            foreach (var layoutField in group.VisibleFields)
            {
                var control = CreateDirectFieldControl(node, layoutField.Id, simplifiedProjection);
                controls.Add(control);
                groupControls.Add(control);
                groupPanel.Children.Add(EditorSimplifiedPromotionControl.Wrap(
                    control,
                    simplifiedProjection,
                    EditorSimplifiedFieldReference.Direct(control.FieldId)));
            }

            if (groupPanel.Children.Count > 0)
            {
                var groupContent = GroupContent(group, groupPanel, groupControls);
                organizedGroups.Add((group, groupContent, EffectiveGroupLayout(group, groupLayout)));
            }
        }

        ComposeOrganizedGroups(
            body,
            layoutCard,
            $"{node.RecordClassId}:{layoutCard.Id}",
            organizedGroups,
            useSectionChrome,
            exclusiveGroupCards);

        if (body.Children.Count == 0)
        {
            body.Children.Add(new TextBlock
            {
                Text = "No fields in this card yet.",
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var card = new InstantEditorCard(
            EditorCardHeader.Create(layoutCard.Label, EditorCardHeader.Subtitle(layoutCard), headerIcon),
            new Border
            {
                Padding = EditorUiDensity.CardThickness(10),
                Child = body,
            },
            layoutCard.DefaultOpen,
            VariantLockButton(node, layoutCard))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        if (organizedGroups.Any((item) => item.Layout == EditorSubcardLayout.VerticalCards))
        {
            EditorGroupBlock.ApplyContentSeparator(card);
        }
        EditorCardHeader.SetOverrideState(headerIcon, controls);
        foreach (var control in controls)
        {
            control.ValueChanged += (_, _) =>
            {
                EditorCardHeader.SetOverrideState(headerIcon, controls);
                _inlinePreviews.Refresh(node, _activeFieldControls.ControlsByFieldId);
            };
        }

        return card;
    }

    public InstantEditorCard CreateEmbedded(
        EditorEmbeddedContext context,
        EditorLayoutCard layoutCard,
        EditorSimplifiedProjectionState? simplifiedProjection = null)
    {
        var body = new StackPanel
        {
            Spacing = EditorUiDensity.Card(12),
        };
        var controls = new List<DictionaryFieldControl>();
        var headerIcon = EditorIcons.CreateSemantic(layoutCard.Label, layoutCard.Icon, 18);
        var visibleGroups = layoutCard.VisibleGroups.ToList();
        var groupLayout = ParseGroupLayout(layoutCard.GroupLayout);
        var useSectionChrome = visibleGroups.Count > 1;
        var exclusiveGroupCards = new List<InstantEditorCard>();
        var organizedGroups = new List<(EditorLayoutGroup Group, Control Content, EditorSubcardLayout Layout)>();

        foreach (var group in visibleGroups)
        {
            var groupControls = new List<DictionaryFieldControl>();
            var groupPanel = new StackPanel
            {
                Spacing = EditorUiDensity.Card(12),
            };

            foreach (var layoutField in group.VisibleFields
                         .Where((field) => field.Id.StartsWith("component.", StringComparison.Ordinal)
                             && !field.Id.Equals("component.type", StringComparison.Ordinal)))
            {
                var control = CreateEmbeddedFieldControl(context, layoutField.Id, simplifiedProjection);
                controls.Add(control);
                groupControls.Add(control);
                var groupPath = context.Slots
                    .TakeLast(2)
                    .Select((slot) => new EditorSimplifiedGroupIdentity(
                        slot.FieldId,
                        slot.Label,
                        EditorIcons.Component))
                    .ToList();
                groupPanel.Children.Add(EditorSimplifiedPromotionControl.Wrap(
                    control,
                    simplifiedProjection,
                    EditorSimplifiedFieldReference.Embedded(
                        context.Slots.Select((slot) => slot.FieldId).ToList(),
                        control.FieldId,
                        groupPath)));
            }

            if (groupPanel.Children.Count > 0)
            {
                var groupContent = GroupContent(group, groupPanel, groupControls);
                organizedGroups.Add((group, groupContent, EffectiveGroupLayout(group, groupLayout)));
            }
        }

        ComposeOrganizedGroups(
            body,
            layoutCard,
            $"{context.OwnerNode.RecordClassId}:{layoutCard.Id}:embedded",
            organizedGroups,
            useSectionChrome,
            exclusiveGroupCards);

        var embeddedBody = new Border
        {
            Padding = EditorUiDensity.CardThickness(10),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 214, 166, 56)),
            Child = body,
        };
        var card = new InstantEditorCard(
            EditorCardHeader.Create(layoutCard.Label, $"Embedded override · {context.OwnerNode.Name}", headerIcon),
            embeddedBody,
            layoutCard.DefaultOpen)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        if (organizedGroups.Any((item) => item.Layout == EditorSubcardLayout.VerticalCards))
        {
            EditorGroupBlock.ApplyContentSeparator(card);
        }
        EditorCardHeader.SetOverrideState(headerIcon, controls);
        foreach (var control in controls)
        {
            control.ValueChanged += (_, _) =>
            {
                EditorCardHeader.SetOverrideState(headerIcon, controls);
            };
        }

        return card;
    }

    public InstantEditorCard CreateSimplified(
        ProjectTreeNode node,
        EditorSimplifiedProjectionState projection,
        bool isExpanded)
    {
        var groups = projection.Layout?.Groups
            .OrderBy((group) => group.Order)
            .ThenBy((group) => group.Label)
            .Where(HasEnabledEntries)
            .ToList() ?? [];
        Control content;
        if (groups.Count == 0)
        {
            content = new TextBlock
            {
                Text = "No controls selected. Switch to Complete to choose them.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72,
            };
        }
        else
        {
            var sections = groups
                .Select((group) => SimplifiedSection(node, projection, group, 0, group.Id))
                .ToList();
            content = CreateGroupLayoutHost(
                $"{node.Id}:simplified:root",
                sections,
                EditorSubcardLayout.VerticalCards);
        }

        var icon = EditorIcons.CreateSemantic("Simplified", EditorIcons.General, 18);
        var card = new InstantEditorCard(
            EditorCardHeader.Create("General", "Selected controls", icon),
            new Border
            {
                Padding = EditorUiDensity.CardThickness(10),
                Child = content,
            },
            isExpanded)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        EditorGroupBlock.ApplyContentSeparator(card);
        return card;
    }

    private EditorInternalNavigationSection SimplifiedSection(
        ProjectTreeNode node,
        EditorSimplifiedProjectionState projection,
        EditorSimplifiedGroup group,
        int depth,
        string statePath)
    {
        var stack = new StackPanel { Spacing = EditorUiDensity.Card(12) };
        foreach (var entry in group.Entries
                     .Where((entry) => entry.Enabled)
                     .OrderBy((entry) => entry.Order)
                     .ThenBy((entry) => entry.Id))
        {
            var control = CreateSimplifiedEntryControl(node, projection, entry);
            if (control is not null)
            {
                stack.Children.Add(entry.Captured
                    ? EditorSimplifiedPromotionControl.WrapCaptured(control)
                    : control);
            }
        }

        var children = group.Groups
            .OrderBy((child) => child.Order)
            .ThenBy((child) => child.Label)
            .Where(HasEnabledEntries)
            .ToList();
        if (children.Count > 0)
        {
            var childSections = children
                .Select((child) => SimplifiedSection(
                    node,
                    projection,
                    child,
                    depth + 1,
                    $"{statePath}:{child.Id}"))
                .ToList();
            stack.Children.Add(CreateGroupLayoutHost(
                $"{node.Id}:simplified:{statePath}:children",
                childSections,
                depth == 0 ? EditorSubcardLayout.VerticalCards : EditorSubcardLayout.SeparatedSections));
        }

        return new EditorInternalNavigationSection(
            group.Id,
            group.Label,
            "Simplified controls",
            string.IsNullOrWhiteSpace(group.Icon) ? EditorIcons.Component : group.Icon,
            stack,
            ShowLabel: false);
    }

    private Control? CreateSimplifiedEntryControl(
        ProjectTreeNode node,
        EditorSimplifiedProjectionState projection,
        EditorSimplifiedEntry entry)
    {
        return entry.Kind switch
        {
            "field" => CreateDirectFieldControl(node, entry.FieldId, projection),
            "embeddedField" => CreateEmbeddedFieldControl(
                new EditorEmbeddedContext(
                    node,
                    entry.SlotFieldIds.Select(EmbeddedComponentSlotCatalog.Get).ToList()),
                entry.FieldId,
                projection),
            "collectionField" => CreateCollectionItemFieldControl(node, entry, projection),
            _ => null,
        };
    }

    private DictionaryFieldControl? CreateCollectionItemFieldControl(
        ProjectTreeNode node,
        EditorSimplifiedEntry entry,
        EditorSimplifiedProjectionState projection)
    {
        var slots = entry.SlotFieldIds.Select(EmbeddedComponentSlotCatalog.Get).ToList();
        var embeddedContext = slots.Count == 0 ? null : new EditorEmbeddedContext(node, slots);
        var collectionValue = embeddedContext is null
            ? _fieldValues.Create(node, entry.CollectionFieldId)
            : _componentClassFieldValues.CreateEmbeddedFieldValue(embeddedContext, entry.CollectionFieldId);
        var collection = collectionValue.Definition.StructuredCollection;
        if (collection is null) return null;
        var items = JsonNode.Parse(collectionValue.Value) as JsonArray ?? new JsonArray();
        var item = items.OfType<JsonObject>().FirstOrDefault((candidate) =>
            candidate["id"]?.GetValue<string>().Equals(entry.ItemId, StringComparison.Ordinal) == true);
        var input = collection.Fields.FirstOrDefault((candidate) =>
            candidate.Id.Equals(entry.ItemFieldId, StringComparison.Ordinal));
        if (item is null || input is null) return null;

        var enabled = CollectionFieldAvailability.IsEnabled(item, input, 0);
        var definition = new FieldDefinition(
            $"{entry.CollectionFieldId}.{entry.ItemId}.{input.Id}",
            input.Label,
            input.ValueKind,
            IsEditable: enabled && collectionValue.Definition.IsEditable,
            DefaultValue: input.DefaultValue,
            Options: input.Options,
            PairLabels: input.PairLabels,
            Number: input.ValueKind is ValueKind.Integer or ValueKind.Decimal or ValueKind.Alpha
                ? new NumberDefinition(input.Minimum, input.Maximum, input.Increment, input.ValueKind == ValueKind.Integer ? 0 : 2)
                : null);
        var services = _dictionaryFieldServices.ForNode(
            node,
            (id) => _activeFieldControls.ValueOrStored(id, (storedId) => _fieldValues.CurrentStoredValue(node, storedId)),
            _openComponentPresetReference,
            null,
            null,
            _openRuntimeComponentOverrides) with
        {
            SimplifiedProjection = projection,
        };
        var control = new DictionaryFieldControl(
            new FieldValue(definition, DesignPreviewTestValues.CollectionValue(item, input)),
            services);
        _activeFieldControls.Register(control);
        control.ValueCommitted += (_, next) =>
        {
            try
            {
                var previous = DesignPreviewTestValues.CollectionValue(item, input);
                item[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, next);
                var nextCollection = items.ToJsonString();
                _fieldCommitCoordinator.Commit(
                    control,
                    next,
                    (draftValue) => draftValue,
                    () => previous,
                    (_) =>
                    {
                        if (embeddedContext is null)
                        {
                            _fieldValues.Commit(node, entry.CollectionFieldId, nextCollection);
                        }
                        else
                        {
                            _componentClassFieldValues.CommitEmbeddedFieldValue(
                                embeddedContext,
                                entry.CollectionFieldId,
                                nextCollection);
                        }
                    });
                _activeFieldControls.RefreshPreviews();
                _refreshPreview();
                if (collection.Fields.Any((candidate) =>
                        candidate.EnabledWhenItemJsonKey.Equals(input.JsonKey, StringComparison.Ordinal)))
                {
                    _scheduleActiveEditorReload(node);
                }
            }
            catch (Exception exception)
            {
                _messages.Error($"Simplified collection field {definition.Id}", exception);
            }
        };
        return control;
    }

    private static bool HasEnabledEntries(EditorSimplifiedGroup group) =>
        group.Entries.Any((entry) => entry.Enabled)
        || group.Groups.Any(HasEnabledEntries);

    public static bool EmbeddedCardHasFields(EditorLayoutCard layoutCard)
    {
        return layoutCard.VisibleGroups
            .SelectMany((group) => group.VisibleFields)
            .Any((field) => field.Id.StartsWith("component.", StringComparison.Ordinal)
                && !field.Id.Equals("component.type", StringComparison.Ordinal));
    }

    internal DictionaryFieldControl CreateDirectFieldControl(
        ProjectTreeNode node,
        string fieldId,
        EditorSimplifiedProjectionState? simplifiedProjection = null)
    {
        var field = _fieldValues.Create(node, fieldId);
        var supportsEmbeddedOverrides = node.Kind is ProjectTreeNodeKind.ComponentClass
            or ProjectTreeNodeKind.ComponentPreset
            or ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.ModuleVariant;
        var hasEmbeddedSlot = EmbeddedComponentSlotCatalog.TryGet(field.Definition.Id, out _);
        var services = _dictionaryFieldServices.ForNode(
            node,
            (id) => _activeFieldControls.ValueOrStored(id, (storedId) => _fieldValues.CurrentStoredValue(node, storedId)),
            _openComponentPresetReference,
            supportsEmbeddedOverrides && hasEmbeddedSlot ? (id) => _openEmbeddedComponentEditor(node, id) : null,
            supportsEmbeddedOverrides ? (definition, input) => _openEmbeddedComponentSlotEditor(node, ComponentInputSlot(definition, input)) : null,
            _openRuntimeComponentOverrides) with
        {
            SimplifiedProjection = simplifiedProjection,
        };
        var control = new DictionaryFieldControl(field, services);
        _activeFieldControls.Register(control);
        control.ValueCommitted += (_, value) =>
        {
            try
            {
                _fieldCommitCoordinator.Commit(
                    control,
                    value,
                    (draftValue) => _fieldValues.ToStorageValue(node, field.Definition.Id, draftValue),
                    () => _fieldValues.CurrentStoredValue(node, field.Definition.Id),
                    (storedValue) => _fieldValues.Commit(node, field.Definition.Id, storedValue));
                _inlinePreviews.Refresh(node, _activeFieldControls.ControlsByFieldId);
                _activeFieldControls.RefreshPreviews();
                _refreshPreview();
            }
            catch (Exception exception)
            {
                _messages.Error($"Editor field {field.Definition.Id}", exception);
            }
        };
        control.RuntimeContractChanged += (_, _) => _scheduleActiveEditorReload(node);
        return control;
    }

    internal DictionaryFieldControl CreateEmbeddedFieldControl(
        EditorEmbeddedContext context,
        string fieldId,
        EditorSimplifiedProjectionState? simplifiedProjection = null)
    {
        var field = _componentClassFieldValues.CreateEmbeddedFieldValue(context, fieldId);
        var services = _dictionaryFieldServices.ForNode(
            context.OwnerNode,
            (id) => _activeFieldControls.ValueOrStored(id, (storedId) =>
                _componentClassFieldValues.CreateEmbeddedFieldValue(context, storedId).Value),
            _openComponentPresetReference,
            (id) => _openNestedEmbeddedComponentEditor(context, id),
            (definition, input) => _openNestedEmbeddedComponentSlotEditor(context, ComponentInputSlot(definition, input)),
            _openRuntimeComponentOverrides) with
        {
            SimplifiedProjection = simplifiedProjection,
            SimplifiedSlotFieldIds = context.Slots.Select((slot) => slot.FieldId).ToList(),
        };
        var control = new DictionaryFieldControl(field, services);
        _activeFieldControls.Register(control);
        control.ValueCommitted += (_, value) =>
        {
            try
            {
                if (value == field.Definition.InheritedStorageValue)
                {
                    _componentClassFieldValues.CommitEmbeddedFieldValue(context, field.Definition.Id, value);
                    control.AcceptInheritedValueAsDefault();
                    _activeFieldControls.RefreshPreviews();
                    _refreshPreview();
                    return;
                }

                _fieldCommitCoordinator.Commit(
                    control,
                    value,
                    (draftValue) => draftValue,
                    () =>
                    {
                        var current = _componentClassFieldValues.CreateEmbeddedFieldValue(context, field.Definition.Id);
                        return current.IsInherited
                            ? current.Definition.InheritedStorageValue
                            : current.Value;
                    },
                    (storedValue) => _componentClassFieldValues.CommitEmbeddedFieldValue(context, field.Definition.Id, storedValue));
                _activeFieldControls.RefreshPreviews();
                _refreshPreview();
            }
            catch (Exception exception)
            {
                _messages.Error($"Embedded field {field.Definition.Id}", exception);
            }
        };
        return control;
    }

    private static EmbeddedComponentSlotDefinition ComponentInputSlot(
        FieldDefinition definition,
        ComponentInputBindingDefinition input)
    {
        if (string.IsNullOrWhiteSpace(input.ComponentType))
        {
            throw new InvalidOperationException($"Component input '{input.Id}' has no component type.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(definition.Id);
        return new EmbeddedComponentSlotDefinition(
            $"{definition.Id}.{input.Id}",
            input.ComponentType,
            input.Label,
            $"component.{input.ComponentType}",
            [.. descriptor.JsonPath, input.JsonKey]);
    }

    private Button? VariantLockButton(ProjectTreeNode node, EditorLayoutCard layoutCard)
    {
        if (node.Kind != ProjectTreeNodeKind.ComponentPreset
            || !layoutCard.Id.Equals("general", StringComparison.Ordinal))
        {
            return null;
        }

        var icon = EditorIcons.Create(node.IsLocked ? EditorIcons.Lock : EditorIcons.Unlock, 15);
        EditorIcons.ApplyBrush(icon, EditorNavigationVisuals.VariantLockBrush(node.IsLocked));
        var button = new Button
        {
            Content = icon,
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, node.IsLocked ? "Unlock variant editing" : "Lock variant editing");
        button.Click += async (_, e) =>
        {
            e.Handled = true;
            await _toggleVariantLock(node);
        };
        return button;
    }

    private Control CreateGroupLayoutHost(
        string stateKey,
        IReadOnlyList<EditorInternalNavigationSection> sections,
        EditorSubcardLayout layout)
    {
        _groupNavigationSelections.TryGetValue(stateKey, out var selectedId);
        var navigationWidth = _groupNavigationWidths.GetValueOrDefault(
            stateKey,
            EditorInternalNavigation.DefaultNavigationWidth);
        return new EditorSubcardLayoutHost(
            sections,
            layout,
            selectedId,
            (nextId) => _groupNavigationSelections[stateKey] = nextId,
            navigationWidth,
            (next) => _groupNavigationWidths[stateKey] = next);
    }

    private void ComposeOrganizedGroups(
        StackPanel body,
        EditorLayoutCard layoutCard,
        string stateKey,
        IReadOnlyList<(EditorLayoutGroup Group, Control Content, EditorSubcardLayout Layout)> groups,
        bool useSectionChrome,
        List<InstantEditorCard> exclusiveGroupCards)
    {
        var blockIndex = 0;
        EditorSubcardLayout? previousLayout = null;
        for (var index = 0; index < groups.Count;)
        {
            var layout = groups[index].Layout;
            if (previousLayout is not null && previousLayout != layout)
            {
                body.Children.Add(EditorGroupBlock.CreateSeparator());
            }
            previousLayout = layout;
            if (layout == EditorSubcardLayout.Stacked)
            {
                body.Children.Add(GroupControl(groups[index].Group, groups[index].Content, useSectionChrome, exclusiveGroupCards));
                index++;
                continue;
            }

            var sections = new List<EditorInternalNavigationSection>();
            while (index < groups.Count && groups[index].Layout == layout)
            {
                var item = groups[index];
                sections.Add(CreateGroupSection(layoutCard, item.Group, item.Content, index));
                index++;
            }
            body.Children.Add(CreateGroupLayoutHost($"{stateKey}:block:{blockIndex++}", sections, layout));
        }
        WireExclusiveGroups(exclusiveGroupCards);
    }

    private static EditorSubcardLayout EffectiveGroupLayout(
        EditorLayoutGroup group,
        EditorSubcardLayout cardLayout)
    {
        return string.IsNullOrWhiteSpace(group.Presentation)
            ? cardLayout
            : ParseGroupLayout(group.Presentation);
    }

    private static EditorInternalNavigationSection CreateGroupSection(
        EditorLayoutCard layoutCard,
        EditorLayoutGroup group,
        Control content,
        int index)
    {
        return new EditorInternalNavigationSection(
            group.Id,
            group.Label,
            "Editor fields",
            string.IsNullOrWhiteSpace(group.Icon) ? EditorIcons.Component : group.Icon,
            content,
            ShowLabel: !(index == 0
                && group.Label.Equals(layoutCard.Label, StringComparison.OrdinalIgnoreCase)));
    }

    private static EditorSubcardLayout ParseGroupLayout(string value)
    {
        return value switch
        {
            "flatStack" => EditorSubcardLayout.FlatStack,
            "verticalCards" => EditorSubcardLayout.VerticalCards,
            "separatedSections" => EditorSubcardLayout.SeparatedSections,
            _ => EditorSubcardLayout.Stacked,
        };
    }

    private static Control GroupControl(
        EditorLayoutGroup group,
        Control groupPanel,
        bool useSectionChrome,
        List<InstantEditorCard> exclusiveGroupCards)
    {
        if (group.Collapsible)
        {
            var control = EditorGroupBlock.CreateCollapsible(group, groupPanel, out var card);
            if (group.Exclusive)
            {
                exclusiveGroupCards.Add(card);
            }

            return control;
        }

        return useSectionChrome
            ? EditorGroupBlock.Create(group, groupPanel)
            : EditorGroupBlock.CreatePlain(group, groupPanel);
    }

    private static Control GroupContent(
        EditorLayoutGroup group,
        StackPanel groupPanel,
        IReadOnlyList<DictionaryFieldControl> controls)
    {
        Control content = groupPanel;
        if (group.PairLayout.Equals("sharedHeader", StringComparison.Ordinal))
        {
            PairFieldLabels? labels = null;
            foreach (var control in controls)
            {
                var controlLabels = control.UseSharedPairHeader();
                labels ??= controlLabels;
            }
            if (labels is not null)
            {
                var compactGroup = new StackPanel { Spacing = EditorUiDensity.Card(8) };
                compactGroup.Children.Add(EditorGroupBlock.CreatePairColumnHeader(labels));
                compactGroup.Children.Add(groupPanel);
                content = compactGroup;
            }
        }

        return LocalHorizontalViewport(content, controls);
    }

    private static Control LocalHorizontalViewport(
        Control content,
        IReadOnlyList<DictionaryFieldControl> controls)
    {
        if (!controls.Any((control) => control.RequiresLocalHorizontalViewport))
        {
            return content;
        }

        return new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    private static void WireExclusiveGroups(IReadOnlyList<InstantEditorCard> cards)
    {
        EditorGroupBlock.WireExclusiveCards(cards);
    }
}
