using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryTextControl : Grid, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly TextBox _textBox;
    private bool _isUpdating;
    private string _value;

    public DictionaryTextControl(FieldDefinition definition, string value)
    {
        _definition = definition;
        _value = value;
        _textBox = DictionaryTextBoxFactory.Create(definition);
        _textBox.Text = value;
        _textBox.TextChanged += (_, _) =>
        {
            if (_isUpdating) return;

            SetLocalValue(_textBox.Text ?? "");
        };
        AttachDeferredCommit(_textBox);
        Children.Add(_textBox);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        if (_value == value) return;

        _value = value;
        _isUpdating = true;
        _textBox.Text = value;
        _isUpdating = false;
    }

    private void SetLocalValue(string value)
    {
        if (_value == value) return;

        _value = value;
        ValueChanged?.Invoke(this, _value);
    }

    private void AttachDeferredCommit(TextBox textBox)
    {
        textBox.LostFocus += (_, _) => CommitValue();
        textBox.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter || _definition.ValueKind == ValueKind.StringMultiline) return;

            CommitValue();
            args.Handled = true;
        };
    }

    private void CommitValue()
    {
        ValueCommitted?.Invoke(this, _value);
    }
}
