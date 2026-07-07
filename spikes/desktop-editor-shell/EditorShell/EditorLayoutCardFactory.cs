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
    private readonly ActorAvatarPreviewController _actorAvatarPreviews;
    private readonly EditorDictionaryFieldServices _dictionaryFieldServices;
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator;
    private readonly EditorActiveFieldControls _activeFieldControls;
    private readonly IEditorShellMessageSink _messages;
    private readonly Func<ProjectTreeNode, string, Task> _openEmbeddedComponentEditor;
    private readonly Func<ProjectTreeNode, EmbeddedComponentSlotDefinition, Task> _openEmbeddedComponentSlotEditor;
    private readonly Func<EditorEmbeddedContext, string, Task> _openNestedEmbeddedComponentEditor;
    private readonly Func<EditorEmbeddedContext, EmbeddedComponentSlotDefinition, Task> _openNestedEmbeddedComponentSlotEditor;
    private readonly Func<ProjectTreeNode, Task> _toggleVariantLock;
    private readonly Action _refreshPreview;

    public EditorLayoutCardFactory(
        EditorFieldValueRouter fieldValues,
        ComponentClassFieldValueService componentClassFieldValues,
        ActorAvatarPreviewController actorAvatarPreviews,
        EditorDictionaryFieldServices dictionaryFieldServices,
        EditorFieldCommitCoordinator fieldCommitCoordinator,
        EditorActiveFieldControls activeFieldControls,
        IEditorShellMessageSink messages,
        Func<ProjectTreeNode, string, Task> openEmbeddedComponentEditor,
        Func<ProjectTreeNode, EmbeddedComponentSlotDefinition, Task> openEmbeddedComponentSlotEditor,
        Func<EditorEmbeddedContext, string, Task> openNestedEmbeddedComponentEditor,
        Func<EditorEmbeddedContext, EmbeddedComponentSlotDefinition, Task> openNestedEmbeddedComponentSlotEditor,
        Func<ProjectTreeNode, Task> toggleVariantLock,
        Action refreshPreview)
    {
        _fieldValues = fieldValues;
        _componentClassFieldValues = componentClassFieldValues;
        _actorAvatarPreviews = actorAvatarPreviews;
        _dictionaryFieldServices = dictionaryFieldServices;
        _fieldCommitCoordinator = fieldCommitCoordinator;
        _activeFieldControls = activeFieldControls;
        _messages = messages;
        _openEmbeddedComponentEditor = openEmbeddedComponentEditor;
        _openEmbeddedComponentSlotEditor = openEmbeddedComponentSlotEditor;
        _openNestedEmbeddedComponentEditor = openNestedEmbeddedComponentEditor;
        _openNestedEmbeddedComponentSlotEditor = openNestedEmbeddedComponentSlotEditor;
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
        var headerIcon = EditorIcons.Create(layoutCard.Icon, 18);
        var visibleGroups = layoutCard.VisibleGroups.ToList();
        var useSectionChrome = visibleGroups.Count > 1;
        var exclusiveGroupCards = new List<InstantEditorCard>();

        foreach (var group in visibleGroups)
        {
            var groupPanel = new StackPanel
            {
                Spacing = EditorUiDensity.Card(12),
            };

            _actorAvatarPreviews.AddIfNeeded(node, layoutCard, groupPanel);

            foreach (var layoutField in group.VisibleFields)
            {
                var field = _fieldValues.Create(node, layoutField.Id);
                var services = _dictionaryFieldServices.ForNode(
                    node,
                    (fieldId) => _activeFieldControls.ValueOrStored(fieldId, (id) => _fieldValues.CurrentStoredValue(node, id)),
                    (fieldId) => _openEmbeddedComponentEditor(node, fieldId),
                    (definition, input) => _openEmbeddedComponentSlotEditor(node, ComponentInputSlot(definition, input)));
                var control = new DictionaryFieldControl(
                    field,
                    services);
                controls.Add(control);
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
                        _actorAvatarPreviews.Refresh(node, _activeFieldControls.ControlsByFieldId);
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
                body.Children.Add(GroupControl(group, groupPanel, useSectionChrome, exclusiveGroupCards));
            }
        }

        WireExclusiveGroups(exclusiveGroupCards);

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
        EditorCardHeader.SetOverrideState(headerIcon, controls);
        foreach (var control in controls)
        {
            control.ValueChanged += (_, _) =>
            {
                EditorCardHeader.SetOverrideState(headerIcon, controls);
                _actorAvatarPreviews.Refresh(node, _activeFieldControls.ControlsByFieldId);
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
        var headerIcon = EditorIcons.Create(layoutCard.Icon, 18);
        var visibleGroups = layoutCard.VisibleGroups.ToList();
        var useSectionChrome = visibleGroups.Count > 1;
        var exclusiveGroupCards = new List<InstantEditorCard>();

        foreach (var group in visibleGroups)
        {
            var groupPanel = new StackPanel
            {
                Spacing = EditorUiDensity.Card(12),
            };

            foreach (var layoutField in group.VisibleFields
                         .Where((field) => field.Id.StartsWith("component.", StringComparison.Ordinal)))
            {
                var field = _componentClassFieldValues.CreateEmbeddedFieldValue(
                    context.OwnerNode,
                    context.Slots,
                    layoutField.Id);
                var services = _dictionaryFieldServices.ForNode(
                    context.OwnerNode,
                    (fieldId) => _activeFieldControls.ValueOrStored(fieldId, (id) =>
                        _componentClassFieldValues.CreateEmbeddedFieldValue(
                            context.OwnerNode,
                            context.Slots,
                            id).Value),
                    (fieldId) => _openNestedEmbeddedComponentEditor(context, fieldId),
                    (definition, input) => _openNestedEmbeddedComponentSlotEditor(context, ComponentInputSlot(definition, input)));
                var control = new DictionaryFieldControl(field, services);
                controls.Add(control);
                _activeFieldControls.Register(control);
                control.ValueCommitted += (_, value) =>
                {
                    try
                    {
                        if (value == field.Definition.InheritedStorageValue)
                        {
                            _componentClassFieldValues.CommitEmbeddedFieldValue(
                                context.OwnerNode,
                                context.Slots,
                                field.Definition.Id,
                                value);
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
                                var current = _componentClassFieldValues.CreateEmbeddedFieldValue(
                                    context.OwnerNode,
                                    context.Slots,
                                    field.Definition.Id);
                                return current.IsInherited
                                    ? current.Definition.InheritedStorageValue
                                    : current.Value;
                            },
                            (storedValue) => _componentClassFieldValues.CommitEmbeddedFieldValue(
                                context.OwnerNode,
                                context.Slots,
                                field.Definition.Id,
                                storedValue));
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
                body.Children.Add(GroupControl(group, groupPanel, useSectionChrome, exclusiveGroupCards));
            }
        }

        WireExclusiveGroups(exclusiveGroupCards);

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
            .Any((field) => field.Id.StartsWith("component.", StringComparison.Ordinal));
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

    private static void WireExclusiveGroups(IReadOnlyList<InstantEditorCard> cards)
    {
        foreach (var card in cards)
        {
            card.Expanded += (_, _) =>
            {
                foreach (var other in cards)
                {
                    if (!ReferenceEquals(other, card))
                    {
                        other.IsExpanded = false;
                    }
                }
            };
        }

        var openCards = cards.Where((card) => card.IsExpanded).ToList();
        foreach (var extraOpenCard in openCards.Skip(1))
        {
            extraOpenCard.IsExpanded = false;
        }
    }
}
