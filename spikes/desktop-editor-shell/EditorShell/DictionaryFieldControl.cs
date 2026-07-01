using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryFieldControl : Grid
{
    private readonly FieldDefinition _definition;
    private readonly TextBlock _label;
    private readonly TextBox _textBox;
    private readonly Button _restoreButton;
    private readonly Func<string, Task<string?>>? _browseDirectory;
    private string _defaultValue;
    private string _value;

    public DictionaryFieldControl(
        FieldValue fieldValue,
        Func<string, Task<string?>>? browseDirectory = null)
    {
        _definition = fieldValue.Definition;
        _defaultValue = fieldValue.Definition.DefaultValue;
        _value = fieldValue.Value;
        _browseDirectory = browseDirectory;

        ColumnDefinitions = _definition.ValueKind == ValueKind.DirectoryPath
            ? new ColumnDefinitions("180,*,Auto,Auto")
            : new ColumnDefinitions("180,*,Auto");
        ColumnSpacing = 12;
        MinHeight = _definition.ValueKind == ValueKind.StringMultiline ? 96 : 40;

        _label = new TextBlock
        {
            Text = _definition.Label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = _definition.ValueKind == ValueKind.StringMultiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
            Margin = _definition.ValueKind == ValueKind.StringMultiline
                ? new Avalonia.Thickness(0, 7, 0, 0)
                : new Avalonia.Thickness(0),
        };
        SetColumn(_label, 0);

        _textBox = CreateTextBox();
        _textBox.Text = _value;
        _textBox.TextChanged += (_, _) =>
        {
            _value = _textBox.Text ?? "";
            UpdateState();
            ValueChanged?.Invoke(this, _value);
        };
        SetColumn(_textBox, 1);

        if (_definition.ValueKind == ValueKind.DirectoryPath)
        {
            var browseButton = new Button
            {
                Content = "Browse",
                MinWidth = 86,
                MinHeight = 36,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = _definition.IsEditable && _browseDirectory is not null,
            };
            browseButton.Click += async (_, _) =>
            {
                if (_browseDirectory is null) return;

                var selectedPath = await _browseDirectory(_value);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    SetValue(selectedPath);
                }
            };
            SetColumn(browseButton, 2);
            Children.Add(browseButton);
        }

        _restoreButton = new Button
        {
            Content = "↺",
            Width = 32,
            Height = 32,
            Padding = new Avalonia.Thickness(0),
            VerticalAlignment = _definition.ValueKind == ValueKind.StringMultiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
            IsVisible = !IsDefault && _definition.IsEditable,
        };
        _restoreButton.Click += (_, _) =>
        {
            SetValue(_defaultValue);
        };
        SetColumn(_restoreButton, _definition.ValueKind == ValueKind.DirectoryPath ? 3 : 2);

        Children.Add(_label);
        Children.Add(_textBox);
        Children.Add(_restoreButton);
        UpdateState();
    }

    public event EventHandler<string>? ValueChanged;

    public bool IsDefault => _value == _defaultValue;

    public void AcceptCurrentValueAsDefault()
    {
        _defaultValue = _value;
        UpdateState();
    }

    public void SetValue(string value)
    {
        if (_textBox.Text == value) return;
        _textBox.Text = value;
        _value = value;
        UpdateState();
        ValueChanged?.Invoke(this, _value);
    }

    private TextBox CreateTextBox()
    {
        var textBox = new TextBox
        {
            IsReadOnly = !_definition.IsEditable || _definition.ValueKind == ValueKind.StringReadOnly,
            AcceptsReturn = _definition.ValueKind == ValueKind.StringMultiline,
            TextWrapping = _definition.ValueKind == ValueKind.StringMultiline
                ? TextWrapping.Wrap
                : TextWrapping.NoWrap,
            MinHeight = _definition.ValueKind == ValueKind.StringMultiline ? 88 : 36,
            PlaceholderText = _definition.ValueKind == ValueKind.DirectoryPath ? "Select folder…" : null,
            VerticalContentAlignment = _definition.ValueKind == ValueKind.StringMultiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
        };
        return textBox;
    }

    private void UpdateState()
    {
        var isDefault = IsDefault;
        _restoreButton.IsVisible = !isDefault && _definition.IsEditable;
        if (isDefault)
        {
            _label.ClearValue(TextBlock.ForegroundProperty);
        }
        else
        {
            _label.Foreground = new SolidColorBrush(Color.Parse("#D6A638"));
        }

        PseudoClasses.Set(":changed", !isDefault);
    }
}
