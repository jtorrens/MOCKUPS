using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryIconTokenControl : Grid, IDictionaryValueControl, IDictionaryPreviewValueControl
{
    private const double TokenMinimumWidth = 72;
    private readonly Func<string, bool, Task<string?>>? _showIconTokenPicker;
    private readonly Func<string, Control>? _createIconPreview;
    private readonly bool _isEditable;
    private readonly Border _previewBox;
    private readonly TextBlock _tokenText;
    private readonly Grid _actions;
    private readonly Button _pickButton;
    private readonly Button _clearButton;
    private string _value;

    public DictionaryIconTokenControl(
        string value,
        bool isEditable,
        Func<string, bool, Task<string?>>? showIconTokenPicker,
        Func<string, Control>? createIconPreview)
    {
        _value = value;
        _isEditable = isEditable;
        _showIconTokenPicker = showIconTokenPicker;
        _createIconPreview = createIconPreview;

        ColumnSpacing = 8;
        RowSpacing = 8;
        MinWidth = 0;

        _previewBox = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(Color.Parse("#4B5B75")),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Children.Add(_previewBox);

        _tokenText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = string.IsNullOrWhiteSpace(value) ? 0.58 : 1,
        };
        Grid.SetColumn(_tokenText, 1);
        Children.Add(_tokenText);

        _pickButton = new Button
        {
            Content = "Pick icon...",
            MinWidth = 92,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _isEditable && _showIconTokenPicker is not null,
        };
        EditorAccessibility.Describe(_pickButton, "Pick an icon token from the active Icon Theme");
        _pickButton.Click += async (_, _) =>
        {
            if (_showIconTokenPicker is null) return;

            var selected = await _showIconTokenPicker(_value, false);
            if (string.IsNullOrWhiteSpace(selected) || selected == _value) return;

            _value = selected;
            RefreshPreview();
            ValueChanged?.Invoke(this, _value);
            ValueCommitted?.Invoke(this, _value);
        };
        _clearButton = new Button
        {
            Content = "Clear",
            MinWidth = 54,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _isEditable,
        };
        EditorAccessibility.Describe(_clearButton, "Clear the selected icon token");
        _clearButton.Click += (_, _) =>
        {
            if (!_isEditable || string.IsNullOrWhiteSpace(_value)) return;

            _value = "";
            RefreshPreview();
            ValueChanged?.Invoke(this, _value);
            ValueCommitted?.Invoke(this, _value);
        };
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

        SizeChanged += (_, args) => ApplyResponsiveLayout(args.NewSize.Width);
        ApplyResponsiveLayout(double.PositiveInfinity);
        RefreshPreview();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        if (_value == value) return;

        _value = value;
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        _previewBox.Child = string.IsNullOrWhiteSpace(_value)
            ? null
            : _createIconPreview?.Invoke(_value);
        _tokenText.Text = string.IsNullOrWhiteSpace(_value) ? "Select icon token..." : _value;
        _tokenText.Opacity = string.IsNullOrWhiteSpace(_value) ? 0.58 : 1;
    }

    private void ApplyResponsiveLayout(double width)
    {
        var actionsMinimumWidth = _pickButton.MinWidth
            + _clearButton.MinWidth
            + _actions.ColumnSpacing;
        var stacked = DictionaryFieldLayoutRules.UsesStackedActions(
            width,
            _previewBox.Width + TokenMinimumWidth,
            actionsMinimumWidth,
            columnGapCount: 2,
            ColumnSpacing);
        ColumnDefinitions = stacked
            ? new ColumnDefinitions("38,*")
            : new ColumnDefinitions("38,*,Auto");
        RowDefinitions = stacked
            ? new RowDefinitions("Auto,Auto")
            : new RowDefinitions("Auto");

        SetColumn(_previewBox, 0);
        SetRow(_previewBox, 0);
        SetColumn(_tokenText, 1);
        SetRow(_tokenText, 0);
        SetColumn(_actions, stacked ? 0 : 2);
        SetColumnSpan(_actions, stacked ? 2 : 1);
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
}
