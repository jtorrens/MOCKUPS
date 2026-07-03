using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryIntegerPairControl : Grid
{
    private readonly TextBox _firstTextBox;
    private readonly TextBox _secondTextBox;
    private bool _isUpdating;

    public DictionaryIntegerPairControl(FieldDefinition definition, string value)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,90,Auto,90");
        ColumnSpacing = 8;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Left;

        var pair = DictionaryFieldPairText.Split(value);
        var labels = DictionaryFieldPairText.Labels(definition);

        var firstLabel = CreateLabel(labels.First);
        SetColumn(firstLabel, 0);

        _firstTextBox = DictionaryTextBoxFactory.CreateCompactPair(pair.First);
        _firstTextBox.TextChanged += (_, _) => SetValueFromTextBoxes();
        AttachDeferredCommit(_firstTextBox);
        SetColumn(_firstTextBox, 1);

        var secondLabel = CreateLabel(labels.Second);
        SetColumn(secondLabel, 2);

        _secondTextBox = DictionaryTextBoxFactory.CreateCompactPair(pair.Second);
        _secondTextBox.TextChanged += (_, _) => SetValueFromTextBoxes();
        AttachDeferredCommit(_secondTextBox);
        SetColumn(_secondTextBox, 3);

        Children.Add(firstLabel);
        Children.Add(_firstTextBox);
        Children.Add(secondLabel);
        Children.Add(_secondTextBox);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        var pair = DictionaryFieldPairText.Split(value);
        _isUpdating = true;
        if (_firstTextBox.Text != pair.First)
        {
            _firstTextBox.Text = pair.First;
        }

        if (_secondTextBox.Text != pair.Second)
        {
            _secondTextBox.Text = pair.Second;
        }

        _isUpdating = false;
    }

    private void SetValueFromTextBoxes()
    {
        if (_isUpdating) return;

        ValueChanged?.Invoke(this, DictionaryFieldPairText.Join(
            _firstTextBox.Text ?? "",
            _secondTextBox.Text ?? ""));
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
        ValueCommitted?.Invoke(this, DictionaryFieldPairText.Join(
            _firstTextBox.Text ?? "",
            _secondTextBox.Text ?? ""));
    }

    private static TextBlock CreateLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            MinWidth = 57,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };
    }
}
