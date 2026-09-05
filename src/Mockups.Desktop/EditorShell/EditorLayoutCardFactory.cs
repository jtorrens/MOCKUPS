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
    private readonly Func<string, Task> _openComponentVariantReference;
    private readonly Func<ProjectTreeNode, Task> _toggleVariantLock;
    private readonly Action<EditorEmbeddedContext> _openRuntimeComponentOverrides;
    private readonly Func<ProjectTreeNode, FieldDefinition, string, Task>
        _openRecordReferenceOverrides;
    private readonly Action<ProjectTreeNode> _scheduleActiveEditorReload;
    private readonly Action _refreshPreview;
    private readonly EditorSessionUiState _sessionUiState;

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
        Func<string, Task> openComponentVariantReference,
        Func<ProjectTreeNode, Task> toggleVariantLock,
        Action<EditorEmbeddedContext> openRuntimeComponentOverrides,
        Func<ProjectTreeNode, FieldDefinition, string, Task>
            openRecordReferenceOverrides,
        Action<ProjectTreeNode> scheduleActiveEditorReload,
        Action refreshPreview,
        EditorSessionUiState sessionUiState)
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
        _openComponentVariantReference = openComponentVariantReference;
        _toggleVariantLock = toggleVariantLock;
        _openRuntimeComponentOverrides = openRuntimeComponentOverrides;
        _openRecordReferenceOverrides =
            openRecordReferenceOverrides;
        _scheduleActiveEditorReload = scheduleActiveEditorReload;
        _refreshPreview = refreshPreview;
        _sessionUiState = sessionUiState;
    }

    public InstantEditorCard Create(
        ProjectTreeNode node,
        EditorLayoutCard layoutCard,
        string editorStateKey,
        EditorDictionaryContextSnapshot dictionaryContext,
        IReadOnlyDictionary<string, FieldValue> preparedFields)
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

            foreach (var layoutField in group.VisibleFieldsFor(
                         preparedFields))
            {
                var control = CreateDirectFieldControl(
                    node,
                    preparedFields[layoutField.Id],
                    dictionaryContext,
                    preparedFields);
                controls.Add(control);
                groupControls.Add(control);
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
            $"{editorStateKey}:{layoutCard.Id}",
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
            SessionStateId = $"layout:{layoutCard.Id}",
        };
        if (organizedGroups.Any((item) => item.Layout == EditorSubcardLayout.VerticalCards))
        {
            EditorGroupBlock.ApplyContentSeparator(card);
        }
        EditorCardHeader.SetOverrideState(headerIcon, controls);
        foreach (var control in controls)
        {
            control.OverrideStateChanged += (_, _) =>
                EditorCardHeader.SetOverrideState(headerIcon, controls);
            control.ValueChanged += (_, _) =>
            {
                _inlinePreviews.Refresh(node, _activeFieldControls.ControlsByFieldId);
            };
        }

        return card;
    }

    public InstantEditorCard CreateEmbedded(
        EditorEmbeddedContext context,
        EditorLayoutCard layoutCard,
        EditorDictionaryContextSnapshot dictionaryContext,
        IReadOnlyDictionary<string, FieldValue> preparedFields)
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

            foreach (var layoutField in group.VisibleFieldsFor(
                             preparedFields)
                         .Where((field) => field.Id.StartsWith("component.", StringComparison.Ordinal)
                             && !field.Id.Equals("component.type", StringComparison.Ordinal)))
            {
                var control = CreateEmbeddedFieldControl(
                    context,
                    preparedFields[layoutField.Id],
                    dictionaryContext,
                    preparedFields);
                controls.Add(control);
                groupControls.Add(control);
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
            $"{context.RecordClassId}:{layoutCard.Id}:embedded",
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
            SessionStateId = $"embedded:{layoutCard.Id}",
        };
        if (organizedGroups.Any((item) => item.Layout == EditorSubcardLayout.VerticalCards))
        {
            EditorGroupBlock.ApplyContentSeparator(card);
        }
        EditorCardHeader.SetOverrideState(headerIcon, controls);
        foreach (var control in controls)
        {
            control.OverrideStateChanged += (_, _) =>
                EditorCardHeader.SetOverrideState(headerIcon, controls);
        }

        return card;
    }

    public static bool EmbeddedCardHasFields(
        EditorLayoutCard layoutCard,
        IReadOnlyDictionary<string, FieldValue> preparedFields)
    {
        return layoutCard.VisibleGroups
            .SelectMany((group) => group.VisibleFieldsFor(
                preparedFields))
            .Any((field) => field.Id.StartsWith("component.", StringComparison.Ordinal)
                && !field.Id.Equals("component.type", StringComparison.Ordinal));
    }

    public InstantEditorCard CreateRecordReferenceOverrides(
        EditorEmbeddedContext context,
        EditorLayoutCard layoutCard,
        EditorDictionaryContextSnapshot dictionaryContext,
        IReadOnlyDictionary<string, FieldValue> preparedFields,
        EditorActiveFieldControls activeFieldControls)
    {
        var body = new StackPanel
        {
            Spacing = EditorUiDensity.Card(12),
        };
        var controls = new List<DictionaryFieldControl>();
        var headerIcon = EditorIcons.CreateSemantic(
            layoutCard.Label,
            layoutCard.Icon,
            18);
        var visibleGroups = layoutCard.VisibleGroups.ToList();
        var groupLayout = ParseGroupLayout(
            layoutCard.GroupLayout);
        var useSectionChrome = visibleGroups.Count > 1;
        var exclusiveGroupCards =
            new List<InstantEditorCard>();
        var organizedGroups =
            new List<(EditorLayoutGroup Group, Control Content,
                EditorSubcardLayout Layout)>();

        foreach (var group in visibleGroups)
        {
            var groupControls =
                new List<DictionaryFieldControl>();
            var groupPanel = new StackPanel
            {
                Spacing = EditorUiDensity.Card(12),
            };
            foreach (var layoutField in group.VisibleFieldsFor(
                         preparedFields))
            {
                if (!preparedFields.TryGetValue(
                        layoutField.Id,
                        out var field))
                {
                    continue;
                }
                var services = _dictionaryFieldServices
                    .ForPreparedNode(
                        context.OwnerNode,
                        dictionaryContext,
                        (fieldId) => activeFieldControls
                            .ValueOrStored(
                                fieldId,
                                (storedId) => PreparedStoredValue(
                                    preparedFields,
                                    storedId)));
                var control = new DictionaryFieldControl(
                    field,
                    services);
                activeFieldControls.Register(control);
                control.ValueCommitted += async (_, value) =>
                {
                    try
                    {
                        await _fieldCommitCoordinator.CommitAsync(
                            control,
                            value,
                            (draft) => draft,
                            () => _fieldValues
                                .CurrentRecordReferenceOverrideStoredValue(
                                    context,
                                    field.Definition.Id),
                            (stored) => _fieldValues
                                .PersistRecordReferenceOverride(
                                context,
                                field.Definition.Id,
                                stored));
                        activeFieldControls.RefreshPreviews();
                        _scheduleActiveEditorReload(
                            context.OwnerNode);
                    }
                    catch (Exception exception)
                    {
                        _messages.Error(
                            $"Record override {field.Definition.Id}",
                            exception);
                    }
                };
                controls.Add(control);
                groupControls.Add(control);
                groupPanel.Children.Add(control);
            }
            if (groupPanel.Children.Count > 0)
            {
                organizedGroups.Add((
                    group,
                    GroupContent(
                        group,
                        groupPanel,
                        groupControls),
                    EffectiveGroupLayout(
                        group,
                        groupLayout)));
            }
        }

        ComposeOrganizedGroups(
            body,
            layoutCard,
            $"record-overrides:{context.OwnerNode.Id}:{layoutCard.Id}",
            organizedGroups,
            useSectionChrome,
            exclusiveGroupCards);
        var card = new InstantEditorCard(
            EditorCardHeader.Create(
                layoutCard.Label,
                EditorCardHeader.Subtitle(layoutCard),
                headerIcon),
            new Border
            {
                Padding = EditorUiDensity.CardThickness(10),
                Child = body,
            },
            layoutCard.DefaultOpen)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SessionStateId =
                $"record-overrides:{layoutCard.Id}",
        };
        if (organizedGroups.Any((item) =>
                item.Layout
                == EditorSubcardLayout.VerticalCards))
        {
            EditorGroupBlock.ApplyContentSeparator(card);
        }
        EditorCardHeader.SetOverrideState(
            headerIcon,
            controls);
        foreach (var control in controls)
        {
            control.OverrideStateChanged += (_, _) =>
                EditorCardHeader.SetOverrideState(
                    headerIcon,
                    controls);
        }
        return card;
    }

    internal DictionaryFieldControl CreateDirectFieldControl(
        ProjectTreeNode node,
        FieldValue field,
        EditorDictionaryContextSnapshot dictionaryContext,
        IReadOnlyDictionary<string, FieldValue> preparedFields)
    {
        var supportsEmbeddedOverrides = node.Kind is ProjectTreeNodeKind.ComponentClass
            or ProjectTreeNodeKind.ComponentVariant
            or ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.ModuleVariant;
        var hasEmbeddedSlot = EmbeddedComponentSlotCatalog.TryGet(field.Definition.Id, out _);
        var services = _dictionaryFieldServices.ForPreparedNode(
            node,
            dictionaryContext,
            (id) => _activeFieldControls.ValueOrStored(
                id,
                (storedId) => PreparedStoredValue(
                    preparedFields,
                    storedId)),
            _openComponentVariantReference,
            supportsEmbeddedOverrides && hasEmbeddedSlot ? (id) => _openEmbeddedComponentEditor(node, id) : null,
            supportsEmbeddedOverrides ? (definition, input) => _openEmbeddedComponentSlotEditor(node, ComponentInputSlot(definition, input)) : null,
            _openRuntimeComponentOverrides,
            (definition, referenceId) =>
                _openRecordReferenceOverrides(
                    node,
                    definition,
                    referenceId),
            restoreEmbeddedComponentOverrides:
                supportsEmbeddedOverrides && hasEmbeddedSlot
                    ? async (_) =>
                    {
                        await _fieldCommitCoordinator.ExecuteAsync(
                            () => _componentClassFieldValues
                                .ClearEmbeddedComponentOverrides(
                                    node,
                                    [EmbeddedComponentSlotCatalog.Get(
                                        field.Definition.Id)]));
                        _scheduleActiveEditorReload(node);
                        _refreshPreview();
                    }
                    : null,
            restoreRecordReferenceOverrides:
                async (definition, _) =>
                {
                    await _fieldCommitCoordinator.ExecuteAsync(
                        () => _fieldValues
                            .ClearRecordReferenceOverrides(
                                node,
                                definition));
                    _scheduleActiveEditorReload(node);
                    _refreshPreview();
                });
        var control = new DictionaryFieldControl(field, services);
        _activeFieldControls.Register(control);
        control.ValueCommitted += async (_, value) =>
        {
            try
            {
                await _fieldCommitCoordinator.CommitAsync(
                    control,
                    value,
                    (draftValue) => _fieldValues.ToStorageValue(node, field.Definition.Id, draftValue),
                    () => _fieldValues.CurrentStoredValue(node, field.Definition.Id),
                    (storedValue) => _fieldValues.Persist(node, field.Definition.Id, storedValue));
                await _fieldValues.ApplyPostCommitEffectsAsync(
                    node,
                    field.Definition.Id,
                    control.Value);
                _activeFieldControls.RefreshReadOnlyValues(
                    (fieldId) => _fieldValues.Create(node, fieldId));
                _inlinePreviews.Refresh(node, _activeFieldControls.ControlsByFieldId);
                _activeFieldControls.RefreshPreviews();
                _scheduleActiveEditorReload(node);
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
        FieldValue field,
        EditorDictionaryContextSnapshot dictionaryContext,
        IReadOnlyDictionary<string, FieldValue> preparedFields)
        => CreateEmbeddedFieldControlCore(
            context,
            field,
            dictionaryContext,
            preparedFields,
            null,
            _activeFieldControls,
            null);

    public Control CreateFlatOverrideContent(
        ProjectTreeNode node,
        EditorPreparedOverrideProjection projection)
    {
        var body = new StackPanel
        {
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        if (projection.Count == 0)
        {
            body.Children.Add(new Border
            {
                Padding = EditorUiDensity.CardThickness(12),
                Child = new TextBlock
                {
                    Text = "No local inherited Overrides.",
                    Opacity = 0.72,
                    TextWrapping = TextWrapping.Wrap,
                },
            });
            return body;
        }

        foreach (var group in projection.Groups)
        {
            var scopedControls = new EditorActiveFieldControls();
            var groupFields = new StackPanel
            {
                Spacing = EditorUiDensity.Card(12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            foreach (var fieldId in group.OverrideFieldIds)
            {
                groupFields.Children.Add(
                    CreateEmbeddedFieldControlCore(
                    group.Context,
                    group.Fields[fieldId],
                    projection.DictionaryContext,
                    group.Fields,
                    projection.RootFields,
                    scopedControls,
                    () => _scheduleActiveEditorReload(node),
                    compact: true));
            }
            body.Children.Add(new Border
            {
                Padding = EditorUiDensity.CardThickness(
                    0,
                    10,
                    0,
                    16),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(
                    Color.FromArgb(46, 128, 142, 164)),
                Child = new StackPanel
                {
                    Spacing = EditorUiDensity.Card(12),
                    HorizontalAlignment =
                        HorizontalAlignment.Stretch,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = group.PathLabel,
                            FontSize = 11,
                            FontWeight = FontWeight.SemiBold,
                            Opacity = 0.72,
                            TextTrimming =
                                TextTrimming.CharacterEllipsis,
                        },
                        groupFields,
                    },
                },
            });
        }
        return body;
    }

    private DictionaryFieldControl CreateEmbeddedFieldControlCore(
        EditorEmbeddedContext context,
        FieldValue field,
        EditorDictionaryContextSnapshot dictionaryContext,
        IReadOnlyDictionary<string, FieldValue> preparedFields,
        IReadOnlyDictionary<string, FieldValue>? dependencyFields,
        EditorActiveFieldControls activeFieldControls,
        Action? restored,
        bool compact = false)
    {
        var services = _dictionaryFieldServices.ForPreparedNode(
            context.OwnerNode,
            dictionaryContext,
            (id) => activeFieldControls.ValueOrStored(
                id,
                (storedId) => PreparedStoredValue(
                    preparedFields,
                    storedId,
                    dependencyFields)),
            _openComponentVariantReference,
            (id) => _openNestedEmbeddedComponentEditor(context, id),
            (definition, input) => _openNestedEmbeddedComponentSlotEditor(context, ComponentInputSlot(definition, input)),
            _openRuntimeComponentOverrides,
            restoreEmbeddedComponentOverrides:
                async (fieldId) =>
                {
                    var nested = context.Nested(
                        EmbeddedComponentSlotCatalog.Get(fieldId));
                    await _componentClassFieldValues
                        .ClearEmbeddedComponentOverridesAsync(nested);
                    activeFieldControls.RefreshPreviews();
                    _refreshPreview();
                    restored?.Invoke();
                });
        var control = new DictionaryFieldControl(
            field,
            services,
            compact);
        activeFieldControls.Register(control);
        control.ValueCommitted += async (_, value) =>
        {
            try
            {
                if (context.RuntimeSource is not null)
                {
                    await _componentClassFieldValues
                        .CommitEmbeddedFieldValueAsync(
                            context,
                            field.Definition.Id,
                            value);
                    if (value
                        == field.Definition
                            .InheritedStorageValue)
                    {
                        control
                            .AcceptInheritedValueAsDefault();
                        restored?.Invoke();
                    }
                    else
                    {
                        control
                            .MarkCurrentValueCommitted();
                    }
                    activeFieldControls.RefreshPreviews();
                    _refreshPreview();
                    return;
                }

                if (value == field.Definition.InheritedStorageValue)
                {
                    await _fieldCommitCoordinator.ExecuteAsync(
                        () => _componentClassFieldValues.CommitEmbeddedFieldValue(
                            context,
                            field.Definition.Id,
                            value));
                    control.AcceptInheritedValueAsDefault();
                    activeFieldControls.RefreshPreviews();
                    _refreshPreview();
                    restored?.Invoke();
                    return;
                }

                await _fieldCommitCoordinator.CommitAsync(
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
                activeFieldControls.RefreshPreviews();
                _refreshPreview();
            }
            catch (Exception exception)
            {
                if (context.RuntimeSource is not null)
                {
                    var confirmed =
                        _componentClassFieldValues
                            .CreateEmbeddedFieldValue(
                                context,
                                field.Definition.Id);
                    control.SetValue(
                        confirmed.IsInherited
                            ? confirmed.Definition
                                .InheritedStorageValue
                            : confirmed.Value);
                    control.MarkCurrentValueCommitted();
                    activeFieldControls
                        .RefreshPreviews();
                }
                _messages.Error($"Embedded field {field.Definition.Id}", exception);
            }
        };
        return control;
    }

    private static string PreparedStoredValue(
        IReadOnlyDictionary<string, FieldValue> preparedFields,
        string fieldId,
        IReadOnlyDictionary<string, FieldValue>? dependencyFields = null)
    {
        if (!preparedFields.TryGetValue(fieldId, out var field)
            && (dependencyFields is null
                || !dependencyFields.TryGetValue(fieldId, out field)))
        {
            throw new InvalidOperationException(
                $"Field '{fieldId}' was not included in the prepared editor snapshot.");
        }
        return field.IsInherited
            ? field.Definition.InheritedStorageValue
            : field.Value;
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
        if (node.Kind != ProjectTreeNodeKind.ComponentVariant
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
        var selectedId = _sessionUiState.Selection(stateKey);
        var navigationWidth = _sessionUiState.NavigationWidth(
            stateKey,
            EditorInternalNavigation.DefaultNavigationWidth);
        return new EditorSubcardLayoutHost(
            sections,
            layout,
            selectedId,
            (nextId) => _sessionUiState.Select(stateKey, nextId),
            navigationWidth,
            (next) => _sessionUiState.SetNavigationWidth(stateKey, next));
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
