using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryIconTokenListControl : Grid, IDictionaryValueControl, IDictionaryPreviewValueControl
{
    private const double TokenListMinimumWidth = 72;
    private readonly Func<string, bool, Task<string?>>? _showIconTokenPicker;
    private readonly Func<string, Control>? _createIconPreview;
    private readonly bool _isEditable;
    private readonly StackPanel _tokenPanel;
    private readonly Grid _actions;
    private readonly Button _pickButton;
    private readonly Button _clearButton;
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

        ColumnSpacing = 8;
        RowSpacing = 8;
        MinWidth = 0;

        _tokenPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Children.Add(_tokenPanel);

        _pickButton = new Button
        {
            Content = "Pick icons...",
            MinWidth = 96,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _isEditable && _showIconTokenPicker is not null,
        };
        EditorAccessibility.Describe(_pickButton, "Pick icon tokens from the active Icon Theme");
        _pickButton.Click += async (_, _) =>
        {
            if (_showIconTokenPicker is null) return;

            var selected = await _showIconTokenPicker(string.Join(",", Tokens()), true);
            if (string.IsNullOrWhiteSpace(selected)) return;

            SetTokens(selected
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where((token) => !string.IsNullOrWhiteSpace(token)));
        };
        _clearButton = new Button
        {
            Content = "Clear",
            MinWidth = 54,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _isEditable,
        };
        EditorAccessibility.Describe(_clearButton, "Clear all selected icon tokens");
        _clearButton.Click += (_, _) => SetTokens([]);

        _actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                _pickButton,
                _clearButton,
            },
        };
        SetColumn(_clearButton, 1);
        Children.Add(_actions);

        ActualThemeVariantChanged += (_, _) => RefreshPreview();
        SizeChanged += (_, args) => ApplyResponsiveLayout(args.NewSize.Width);
        ApplyResponsiveLayout(double.PositiveInfinity);
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
            var iconFrame = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(1),
                BorderBrush = BorderBrushForTheme(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = _createIconPreview?.Invoke(token),
            };

            _tokenPanel.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(4),
                BorderThickness = new Thickness(1),
                BorderBrush = BorderBrushForTheme(),
                Background = Brushes.Transparent,
                Child = iconFrame,
            });
        }
    }

    private IBrush BorderBrushForTheme()
    {
        return new SolidColorBrush(
            ActualThemeVariant == ThemeVariant.Light
                ? Color.Parse("#6F7A8C")
                : Color.Parse("#6E82A3"));
    }

    private void ApplyResponsiveLayout(double width)
    {
        var actionsMinimumWidth = _pickButton.MinWidth
            + _clearButton.MinWidth
            + _actions.ColumnSpacing;
        var stacked = DictionaryFieldLayoutRules.UsesStackedActions(
            width,
            TokenListMinimumWidth,
            actionsMinimumWidth,
            columnGapCount: 1,
            ColumnSpacing);
        ColumnDefinitions = stacked
            ? new ColumnDefinitions("*")
            : new ColumnDefinitions("*,Auto");
        RowDefinitions = stacked
            ? new RowDefinitions("Auto,Auto")
            : new RowDefinitions("Auto");

        SetColumn(_tokenPanel, 0);
        SetRow(_tokenPanel, 0);
        SetColumn(_actions, stacked ? 0 : 1);
        SetRow(_actions, stacked ? 1 : 0);
        _actions.ColumnDefinitions = stacked
            ? new ColumnDefinitions("*,*")
            : new ColumnDefinitions("Auto,Auto");
        _actions.HorizontalAlignment = stacked
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Right;
        _pickButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _clearButton.HorizontalAlignment = HorizontalAlignment.Stretch;
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
        return Parse(_value)
            .Select((item) => item!.GetValue<string>())
            .ToList();
    }

    private static string Normalize(string value)
    {
        return Parse(value).ToJsonString();
    }

    private static JsonArray Parse(string value)
    {
        return RuntimeInputValueKindContract.ParseValue(
            ValueKind.IconTokenList,
            value,
            "Icon token list dictionary value").AsArray();
    }

    private static string Serialize(IEnumerable<string> tokens)
    {
        return new JsonArray(tokens
            .Select((token) => JsonValue.Create(token))
            .ToArray<JsonNode?>()).ToJsonString();
    }
}
