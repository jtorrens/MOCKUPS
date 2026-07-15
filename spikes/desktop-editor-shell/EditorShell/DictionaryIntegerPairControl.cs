using Avalonia.Controls;
using Avalonia.Layout;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryIntegerPairControl : Grid, IDictionaryValueControl
{
    private const double CompactThreshold = 320;
    private readonly TextBlock _firstLabel;
    private readonly TextBlock _secondLabel;
    private readonly TextBox _firstTextBox;
    private readonly TextBox _secondTextBox;
    private bool _isUpdating;

    public DictionaryIntegerPairControl(FieldDefinition definition, string value)
    {
        ColumnSpacing = 8;
        RowSpacing = 8;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        MinWidth = 0;

        var pair = DictionaryFieldPairText.Split(value);
        var labels = DictionaryFieldPairText.Labels(definition);

        _firstLabel = CreateLabel(labels.First);

        _firstTextBox = DictionaryTextBoxFactory.CreateCompactPair(pair.First);
        _firstTextBox.TextChanged += (_, _) => SetValueFromTextBoxes();
        EditorTextBoxBehavior.AttachDeferredCommit(_firstTextBox, CommitValue);
        _secondLabel = CreateLabel(labels.Second);

        _secondTextBox = DictionaryTextBoxFactory.CreateCompactPair(pair.Second);
        _secondTextBox.TextChanged += (_, _) => SetValueFromTextBoxes();
        EditorTextBoxBehavior.AttachDeferredCommit(_secondTextBox, CommitValue);
        Children.Add(_firstLabel);
        Children.Add(_firstTextBox);
        Children.Add(_secondLabel);
        Children.Add(_secondTextBox);
        SizeChanged += (_, args) => ApplyResponsiveLayout(args.NewSize.Width);
        ApplyResponsiveLayout(double.PositiveInfinity);
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

    private void ApplyResponsiveLayout(double width)
    {
        var compact = width > 0 && width < CompactThreshold;
        ColumnDefinitions = compact
            ? new ColumnDefinitions("57,*")
            : new ColumnDefinitions("57,*,57,*");
        RowDefinitions = compact
            ? new RowDefinitions("Auto,Auto")
            : new RowDefinitions("Auto");

        SetColumn(_firstLabel, 0);
        SetRow(_firstLabel, 0);
        SetColumn(_firstTextBox, 1);
        SetRow(_firstTextBox, 0);
        SetColumn(_secondLabel, compact ? 0 : 2);
        SetRow(_secondLabel, compact ? 1 : 0);
        SetColumn(_secondTextBox, compact ? 1 : 3);
        SetRow(_secondTextBox, compact ? 1 : 0);
    }
}
