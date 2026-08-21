using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record PreparedRecordReferenceOverrides(
    ProjectTreeNode ReferenceNode,
    EditorLayout Layout,
    IReadOnlyDictionary<string, FieldValue> Fields,
    EditorDictionaryContextSnapshot DictionaryContext);

internal sealed record RecordReferenceOverrideSource(
    ProjectTreeNode OwnerNode,
    string ReferenceId,
    Func<IEnumerable<string>,
        IReadOnlyDictionary<string, FieldValue>> PrepareFields,
    Func<string, string> CurrentStoredValue,
    Action<string, string> Persist);

internal sealed class RecordReferenceOverrideSourceFactory
{
    private readonly IReadOnlyDictionary<string,
        Func<ProjectTreeNode, string,
            RecordReferenceOverrideSource>> _sources;

    public RecordReferenceOverrideSourceFactory(
        RecordClassFieldValueService fieldValues)
    {
        _sources = new Dictionary<string,
            Func<ProjectTreeNode, string,
                RecordReferenceOverrideSource>>(
            StringComparer.Ordinal)
        {
            ["shot.deviceOverrideId"] =
                (ownerNode, referenceId) => new(
                    ownerNode,
                    referenceId,
                    (fieldIds) => fieldValues
                        .CreateShotDeviceOverrideFields(
                            ownerNode,
                            referenceId,
                            fieldIds),
                    (fieldId) => fieldValues
                        .CurrentShotDeviceOverrideStoredValue(
                            ownerNode.Id,
                            fieldId),
                    (fieldId, value) => fieldValues
                        .CommitShotDeviceOverrideField(
                            ownerNode,
                            referenceId,
                            fieldId,
                            value)),
        };
    }

    public RecordReferenceOverrideSource Create(
        ProjectTreeNode ownerNode,
        FieldDefinition definition,
        string referenceId)
    {
        if (!_sources.TryGetValue(
                definition.Id,
                out var create))
        {
            throw new InvalidOperationException(
                $"Record reference '{definition.Id}' has no declared override document owner.");
        }
        return create(ownerNode, referenceId);
    }
}

internal sealed class RecordReferenceOverridesDialogController
{
    private readonly Window _owner;
    private readonly IEditorLayoutStore _layouts;
    private readonly EditorDictionaryFieldServices
        _dictionaryFields;
    private readonly EditorLayoutCardFactory _layoutCards;
    private readonly EditorOperationCoordinator _operations;
    private readonly Func<IReadOnlyList<ProjectTreeNode>>
        _treeRoots;

    public RecordReferenceOverridesDialogController(
        Window owner,
        IEditorLayoutStore layouts,
        EditorDictionaryFieldServices dictionaryFields,
        EditorLayoutCardFactory layoutCards,
        EditorOperationCoordinator operations,
        Func<IReadOnlyList<ProjectTreeNode>> treeRoots)
    {
        _owner = owner;
        _layouts = layouts;
        _dictionaryFields = dictionaryFields;
        _layoutCards = layoutCards;
        _operations = operations;
        _treeRoots = treeRoots;
    }

    public async Task Show(
        RecordReferenceOverrideSource source,
        FieldDefinition definition)
    {
        var recordClassId = definition.RecordReference?
            .OverrideRecordClassId ?? "";
        if (string.IsNullOrWhiteSpace(recordClassId))
        {
            throw new InvalidOperationException(
                $"Record reference '{definition.Id}' does not declare overrides.");
        }
        var referenceNode = EditorNodeSelectionState.FindNodeById(
            _treeRoots(),
            source.ReferenceId)
            ?? throw new InvalidOperationException(
                $"Record reference '{source.ReferenceId}' is not present in the current project tree.");
        if (!referenceNode.RecordClassId.Equals(
                recordClassId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Record reference '{source.ReferenceId}' has class '{referenceNode.RecordClassId}', expected '{recordClassId}'.");
        }

        var selectedThemeId = _dictionaryFields
            .CaptureSelectedThemeId();
        var prepared = await _operations.ExecuteAsync(() =>
        {
            var layout = _layouts.LoadEditorLayout(
                recordClassId);
            var fieldIds = layout.Cards
                .Where((card) => card.Visible)
                .SelectMany((card) => card.VisibleGroups)
                .SelectMany((group) => group.VisibleFields)
                .Select((field) => field.Id)
                .ToArray();
            var fields = source.PrepareFields(fieldIds);
            return new PreparedRecordReferenceOverrides(
                referenceNode,
                layout,
                fields,
                _dictionaryFields.PrepareContext(
                    source.OwnerNode,
                    selectedThemeId,
                    fields,
                    CancellationToken.None));
        });

        var activeFields = new EditorActiveFieldControls();
        var cards = prepared.Layout.Cards
            .Where((card) => card.Visible)
            .OrderBy((card) => card.Order)
            .ThenBy((card) => card.Label)
            .Where((card) => card.VisibleGroups
                .SelectMany((group) => group.VisibleFields)
                .Any((field) => prepared.Fields.ContainsKey(
                    field.Id)))
            .Select((card) => _layoutCards
                .CreateRecordReferenceOverrides(
                    source,
                    card,
                    prepared.DictionaryContext,
                    prepared.Fields,
                    activeFields))
            .ToList();

        var cardPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var cardHost = new EditorCardHostController(
            cardPanel,
            () => 650);
        cardHost.Replace(cards, resetExpansion: false);

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 96,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var dialog = new SukiWindow
        {
            Title = $"{prepared.ReferenceNode.Name} overrides",
            Width = 720,
            Height = 760,
            MinWidth = 620,
            MinHeight = 560,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(
            dialog,
            _owner);
        closeButton.Click += (_, _) => dialog.Close();
        var content = new Grid
        {
            RowDefinitions = new RowDefinitions(
                "Auto,*,Auto"),
            RowSpacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Values inherit from the selected record. Restore removes the owner-local override.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.72,
                },
                new ScrollViewer
                {
                    Content = cardPanel,
                },
                closeButton,
            },
        };
        Grid.SetRow(content.Children[1], 1);
        Grid.SetRow(closeButton, 2);
        dialog.Content = new Border
        {
            Padding = new Thickness(18),
            Child = content,
        };
        await dialog.ShowDialog(_owner);
    }
}
