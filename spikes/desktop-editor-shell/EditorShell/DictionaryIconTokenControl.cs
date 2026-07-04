using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryIconTokenControl : Grid, IDictionaryValueControl, IDictionaryPreviewValueControl
{
    private readonly Func<string, bool, Task<string?>>? _showIconTokenPicker;
    private readonly Func<string, Control>? _createIconPreview;
    private readonly bool _isEditable;
    private readonly Border _previewBox;
    private readonly TextBlock _tokenText;
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

        ColumnDefinitions = new ColumnDefinitions("38,*,Auto");
        ColumnSpacing = 8;

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

            var selected = await _showIconTokenPicker(_value, false);
            if (string.IsNullOrWhiteSpace(selected) || selected == _value) return;

            _value = selected;
            RefreshPreview();
            ValueChanged?.Invoke(this, _value);
            ValueCommitted?.Invoke(this, _value);
        };
        Grid.SetColumn(pickButton, 2);
        Children.Add(pickButton);

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
}

