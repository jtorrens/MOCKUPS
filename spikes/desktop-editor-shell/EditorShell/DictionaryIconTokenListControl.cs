using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryIconTokenListControl : Grid, IDictionaryValueControl, IDictionaryPreviewValueControl
{
    private readonly Func<string, bool, Task<string?>>? _showIconTokenPicker;
    private readonly Func<string, Control>? _createIconPreview;
    private readonly bool _isEditable;
    private readonly StackPanel _tokenPanel;
    private string _value;

    public DictionaryIconTokenListControl(
        string value,
        bool isEditable,
        Func<string, bool, Task<string?>>? showIconTokenPicker,
        Func<string, Control>? createIconPreview)
    {
        _value = Normalize(value);
        _isEditable = isEditable;
        _showIconTokenPicker = showIconTokenPicker;
        _createIconPreview = createIconPreview;

        ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto");
        ColumnSpacing = 8;

        _tokenPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Children.Add(_tokenPanel);

        var pickButton = new Button
        {
            Content = "Pick...",
            MinWidth = 72,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _isEditable && _showIconTokenPicker is not null,
        };
        pickButton.Click += async (_, _) =>
        {
            if (_showIconTokenPicker is null) return;

            var selected = await _showIconTokenPicker(string.Join(",", Tokens()), true);
            if (string.IsNullOrWhiteSpace(selected)) return;

            SetTokens(selected
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where((token) => !string.IsNullOrWhiteSpace(token)));
        };
        SetColumn(pickButton, 1);
        Children.Add(pickButton);

        var clearButton = new Button
        {
            Content = "Clear",
            MinWidth = 54,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _isEditable,
        };
        clearButton.Click += (_, _) => SetTokens([]);
        SetColumn(clearButton, 2);
        Children.Add(clearButton);

        RefreshPreview();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        var normalized = Normalize(value);
        if (_value == normalized) return;

        _value = normalized;
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        _tokenPanel.Children.Clear();
        var tokens = Tokens().ToList();
        if (tokens.Count == 0)
        {
            _tokenPanel.Children.Add(new TextBlock
            {
                Text = "No icons",
                Opacity = 0.58,
                VerticalAlignment = VerticalAlignment.Center,
            });
            return;
        }

        foreach (var token in tokens)
        {
            _tokenPanel.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 3),
                Background = new SolidColorBrush(Color.Parse("#20314D")),
                Child = _createIconPreview?.Invoke(token) ?? new TextBlock { Text = token },
            });
        }
    }

    private void SetTokens(IEnumerable<string> tokens)
    {
        if (!_isEditable) return;

        var next = Serialize(tokens
            .Where((token) => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal));
        if (_value == next) return;

        _value = next;
        RefreshPreview();
        ValueChanged?.Invoke(this, _value);
        ValueCommitted?.Invoke(this, _value);
    }

    private IReadOnlyList<string> Tokens()
    {
        try
        {
            var node = JsonNode.Parse(_value);
            return node is JsonArray array
                ? array
                    .Select((item) => item?.GetValue<string>() ?? "")
                    .Where((token) => !string.IsNullOrWhiteSpace(token))
                    .ToList()
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Normalize(string value)
    {
        try
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value);
            return node is JsonArray array
                ? Serialize(array
                    .Select((item) => item?.GetValue<string>() ?? "")
                    .Where((token) => !string.IsNullOrWhiteSpace(token)))
                : "[]";
        }
        catch (JsonException)
        {
            return "[]";
        }
    }

    private static string Serialize(IEnumerable<string> tokens)
    {
        return new JsonArray(tokens
            .Select((token) => JsonValue.Create(token))
            .ToArray<JsonNode?>()).ToJsonString();
    }
}
