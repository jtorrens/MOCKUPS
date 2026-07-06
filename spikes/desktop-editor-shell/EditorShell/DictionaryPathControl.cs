using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryPathControl : Grid, IDictionaryValueControl
{
    private readonly TextBox _textBox;
    private readonly DictionaryPathBrowseButton _browseButton;
    private bool _isUpdating;
    private string _value;

    public DictionaryPathControl(
        FieldDefinition definition,
        string value,
        Func<string, ValueKind, Task<string?>>? browsePath)
    {
        _value = value;

        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        ColumnSpacing = 10;

        _textBox = DictionaryTextBoxFactory.Create(definition);
        _textBox.Text = value;
        _textBox.TextChanged += (_, _) =>
        {
            if (_isUpdating) return;

            SetLocalValue(_textBox.Text ?? "");
        };
        AttachDeferredCommit(_textBox);
        Children.Add(_textBox);

        _browseButton = new DictionaryPathBrowseButton(definition.ValueKind, value, definition.IsEditable, browsePath);
        _browseButton.ValueCommitted += (_, selectedPath) =>
        {
            SetLocalValue(selectedPath, updateTextBox: true);
            CommitValue();
        };
        SetColumn(_browseButton, 1);
        Children.Add(_browseButton);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        if (_value == value) return;

        _value = value;
        _browseButton.SetValue(value);
        _isUpdating = true;
        _textBox.Text = value;
        _isUpdating = false;
    }

    private void SetLocalValue(string value, bool updateTextBox = false)
    {
        if (_value == value) return;

        _value = value;
        _browseButton.SetValue(value);
        if (updateTextBox)
        {
            _isUpdating = true;
            _textBox.Text = value;
            _isUpdating = false;
        }

        ValueChanged?.Invoke(this, _value);
    }

    private void AttachDeferredCommit(TextBox textBox)
    {
        textBox.LostFocus += (_, _) => CommitValue();
        textBox.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter) return;

            CommitValue();
            args.Handled = true;
        };
    }

    private void CommitValue()
    {
        ValueCommitted?.Invoke(this, _value);
    }
}
