using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum StructuredCollectionEditingContext
{
    VariantAuthoring,
    RuntimeTestValues,
    InstanceRuntime,
    RuntimeApi,
}

internal sealed record StructuredCollectionItemContent(
    Control Content,
    IReadOnlyList<EditorInternalNavigationSection> Subcards);

internal sealed record StructuredCollectionActions(
    Action AddFirst,
    Action<int> AddAfter,
    Action<int> Duplicate,
    Action<int, int> Move,
    Func<int, Task> Delete);

internal sealed class StructuredCollectionEditor
{
    private readonly StructuredCollectionEditingContext _context;
    private readonly string _scopeKey;
    private readonly string _itemLabel;
    private readonly IReadOnlyList<JsonObject> _items;
    private readonly Func<JsonObject, int, string> _itemId;
    private readonly Func<JsonObject, int, RuntimeCollectionItemPresentationResult> _presentation;
    private readonly Func<JsonObject, int, StructuredCollectionItemContent> _content;
    private readonly StructuredCollectionActions _actions;
    private readonly EditorSessionUiState _sessionUiState;
    private readonly bool _canEditStructure;

    public StructuredCollectionEditor(
        StructuredCollectionEditingContext context,
        string scopeKey,
        string itemLabel,
        IReadOnlyList<JsonObject> items,
        Func<JsonObject, int, string> itemId,
        Func<JsonObject, int, RuntimeCollectionItemPresentationResult> presentation,
        Func<JsonObject, int, StructuredCollectionItemContent> content,
        StructuredCollectionActions actions,
        EditorSessionUiState sessionUiState,
        bool canEditStructure = true)
    {
        _context = context;
        _scopeKey = scopeKey;
        _itemLabel = itemLabel;
        _items = items;
        _itemId = itemId;
        _presentation = presentation;
        _content = content;
        _actions = actions;
        _sessionUiState = sessionUiState;
        _canEditStructure = canEditStructure;
    }

    public StructuredCollectionEditingContext Context => _context;

    public Control Create()
    {
        var footer = EditorCollectionItemControls.CreateFooter(
            _itemLabel,
            _items.Count,
            _canEditStructure,
            _actions.AddFirst,
            _actions.AddAfter);

        var subcards = new List<EditorInternalNavigationSection>();
        for (var index = 0; index < _items.Count; index++)
        {
            var itemIndex = index;
            var item = _items[itemIndex];
            var itemId = _itemId(item, itemIndex);
            var expansionKey = $"{_scopeKey}:{itemId}:expanded";
            var navigationKey = $"{_scopeKey}:{itemId}:vertical-card";
            var itemContent = _content(item, itemIndex);
            var presentation = _presentation(item, itemIndex);
            subcards.Add(new EditorInternalNavigationSection(
                itemId,
                presentation.Title,
                presentation.Subtitle,
                presentation.Icon,
                itemContent.Content,
                _canEditStructure
                    ? EditorCollectionItemControls.CreateActions(
                        _itemLabel,
                        itemIndex,
                        _items.Count,
                        _actions.AddAfter,
                        _actions.Duplicate,
                        _actions.Move,
                        _actions.Delete)
                    : null,
                itemContent.Subcards,
                EditorSubcardLayout.VerticalCards,
                _sessionUiState.IsExpanded(expansionKey),
                (next) => _sessionUiState.SetExpanded(expansionKey, next),
                _sessionUiState.Selection(navigationKey),
                (next) => _sessionUiState.Select(navigationKey, next),
                _sessionUiState.NavigationWidth(navigationKey, EditorInternalNavigation.DefaultNavigationWidth),
                (next) => _sessionUiState.SetNavigationWidth(navigationKey, next),
                Reveal: _sessionUiState.ConsumeReveal(expansionKey)));
        }

        var result = new StackPanel { Spacing = EditorUiDensity.Card(8) };
        result.Children.Add(new EditorSubcardLayoutHost(subcards, EditorSubcardLayout.FlatStack));
        if (footer is Panel panel && panel.Children.Count > 0) result.Children.Add(footer);
        return result;
    }

    public void ActivateOnly(JsonObject item, int fallbackIndex)
    {
        var activeKey = $"{_scopeKey}:{_itemId(item, fallbackIndex)}:expanded";
        var keys = new List<string>();
        for (var index = 0; index < _items.Count; index++)
        {
            keys.Add($"{_scopeKey}:{_itemId(_items[index], index)}:expanded");
        }
        keys.Add(activeKey);
        _sessionUiState.SetOnlyExpanded(keys, activeKey);
        _sessionUiState.RequestReveal(activeKey);
    }

}
