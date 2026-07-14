using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Action _refreshPreview;
    private readonly Dictionary<string, string> _groupNavigationSelections = new(StringComparer.Ordinal);

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
        _refreshPreview = refreshPreview;
    }

    public InstantEditorCard Create(ProjectTreeNode node, EditorLayoutCard layoutCard)
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
                var field = _fieldValues.Create(node, layoutField.Id);
                var supportsEmbeddedOverrides = node.Kind is ProjectTreeNodeKind.ComponentClass or ProjectTreeNodeKind.ComponentPreset or ProjectTreeNodeKind.Module;
                var services = _dictionaryFieldServices.ForNode(
                    node,
                    (fieldId) => _activeFieldControls.ValueOrStored(fieldId, (id) => _fieldValues.CurrentStoredValue(node, id)),
                    _openComponentPresetReference,
                    supportsEmbeddedOverrides ? (fieldId) => _openEmbeddedComponentEditor(node, fieldId) : null,
                    supportsEmbeddedOverrides ? (definition, input) => _openEmbeddedComponentSlotEditor(node, ComponentInputSlot(definition, input)) : null);
                var control = new DictionaryFieldControl(
                    field,
                    services);
                controls.Add(control);
                groupControls.Add(control);
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
                groupPanel.Children.Add(control);
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

    public InstantEditorCard CreateEmbedded(EditorEmbeddedContext context, EditorLayoutCard layoutCard)
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
                var field = _componentClassFieldValues.CreateEmbeddedFieldValue(context, layoutField.Id);
                var services = _dictionaryFieldServices.ForNode(
                    context.OwnerNode,
                    (fieldId) => _activeFieldControls.ValueOrStored(fieldId, (id) =>
                        _componentClassFieldValues.CreateEmbeddedFieldValue(context, id).Value),
                    _openComponentPresetReference,
                    (fieldId) => _openNestedEmbeddedComponentEditor(context, fieldId),
                    (definition, input) => _openNestedEmbeddedComponentSlotEditor(context, ComponentInputSlot(definition, input)));
                var control = new DictionaryFieldControl(field, services);
                controls.Add(control);
                groupControls.Add(control);
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
                groupPanel.Children.Add(control);
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

    public static bool EmbeddedCardHasFields(EditorLayoutCard layoutCard)
    {
        return layoutCard.VisibleGroups
            .SelectMany((group) => group.VisibleFields)
            .Any((field) => field.Id.StartsWith("component.", StringComparison.Ordinal)
                && !field.Id.Equals("component.type", StringComparison.Ordinal));
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
        return new EditorSubcardLayoutHost(
            sections,
            layout,
            selectedId,
            (nextId) => _groupNavigationSelections[stateKey] = nextId);
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
